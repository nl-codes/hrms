using System.Globalization;
using System.Text;
using HRMS.Constants;
using HRMS.Data;
using HRMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Controllers;

[Authorize(Roles = Roles.HiringManager)]
public class ReportsController(ApplicationDbContext dbContext) : Controller
{
    private const decimal FallbackHourlyRate = 20m;
    private const decimal TrainingCompletionBonus = 50m;

    private readonly ApplicationDbContext _dbContext = dbContext;

    [HttpGet]
    public async Task<IActionResult> Payroll(DateTime? startDate, DateTime? endDate)
    {
        var nowUtc = DateTime.UtcNow;
        var currentMonthStart = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var currentMonthEnd = currentMonthStart.AddMonths(1).AddDays(-1);

        var effectiveStartDate = startDate?.Date ?? currentMonthStart.Date;
        var effectiveEndDate = endDate?.Date ?? currentMonthEnd.Date;

        if (effectiveEndDate < effectiveStartDate)
        {
            ModelState.AddModelError(string.Empty, "End date must be on or after start date.");
            effectiveEndDate = effectiveStartDate;
        }

        var rows = await BuildPayrollRowsAsync(effectiveStartDate, effectiveEndDate);

        return View(new PayrollReportViewModel
        {
            StartDate = effectiveStartDate,
            EndDate = effectiveEndDate,
            CurrentMonthStart = currentMonthStart.Date,
            CurrentMonthEnd = currentMonthEnd.Date,
            Rows = rows
        });
    }

    [HttpGet]
    public async Task<IActionResult> ExportPayrollToCsv(DateTime month)
    {
        var monthDate = month == default ? DateTime.UtcNow.Date : month.Date;
        var monthStart = new DateTime(monthDate.Year, monthDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        var rows = await BuildPayrollRowsAsync(monthStart.Date, monthEnd.Date);
        var exportDate = DateTime.UtcNow;

        var csvBuilder = new StringBuilder();
        csvBuilder.AppendLine("EmployeeId,EmployeeName,TotalHours,HourlyRate,GrossPay,TrainingBonus,ExportDate");

        foreach (var row in rows.OrderBy(r => r.EmployeeName))
        {
            csvBuilder.AppendLine(string.Join(",",
                EscapeCsv(row.EmployeeId),
                EscapeCsv(row.EmployeeName),
                row.TotalHoursWorked.ToString("0.##", CultureInfo.InvariantCulture),
                row.HourlyRate.ToString("0.##", CultureInfo.InvariantCulture),
                row.GrossPay.ToString("0.##", CultureInfo.InvariantCulture),
                row.TrainingBonus.ToString("0.##", CultureInfo.InvariantCulture),
                exportDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        }

        var fileName = $"fis-payroll-{monthStart:yyyy-MM}.csv";
        return File(Encoding.UTF8.GetBytes(csvBuilder.ToString()), "text/csv", fileName);
    }

    private async Task<IReadOnlyList<PayrollReportItemViewModel>> BuildPayrollRowsAsync(DateTime startDate, DateTime endDate)
    {
        var rangeStartUtc = DateTime.SpecifyKind(startDate.Date, DateTimeKind.Utc);
        var rangeEndExclusiveUtc = DateTime.SpecifyKind(endDate.Date.AddDays(1), DateTimeKind.Utc);

        var overlappingShifts = await _dbContext.WorkShifts
            .Where(shift => shift.StartTimeUtc < rangeEndExclusiveUtc && shift.EndTimeUtc > rangeStartUtc)
            .Select(shift => new
            {
                shift.EmployeeUserId,
                shift.WeekStartDate,
                shift.StartTimeUtc,
                shift.EndTimeUtc
            })
            .ToListAsync();

        if (overlappingShifts.Count == 0)
        {
            return [];
        }

        var employeeIds = overlappingShifts
            .Select(shift => shift.EmployeeUserId)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var users = await _dbContext.Users
            .Where(user => employeeIds.Contains(user.Id))
            .Select(user => new
            {
                user.Id,
                user.DisplayName,
                user.Email,
                user.UserName,
                user.DefaultHourlyRate
            })
            .ToDictionaryAsync(user => user.Id);

        var completedTrainingCounts = await _dbContext.TrainingEnrollments
            .Where(enrollment =>
                employeeIds.Contains(enrollment.EmployeeUserId)
                && enrollment.Status == EnrollmentStatus.Completed
                && enrollment.TrainingSession != null
                && enrollment.TrainingSession.SessionDateUtc >= rangeStartUtc
                && enrollment.TrainingSession.SessionDateUtc < rangeEndExclusiveUtc)
            .GroupBy(enrollment => enrollment.EmployeeUserId)
            .Select(group => new
            {
                EmployeeUserId = group.Key,
                Count = group.Count()
            })
            .ToDictionaryAsync(item => item.EmployeeUserId, item => item.Count);

        var weekStartsInRange = GetWeekStartsInRange(startDate, endDate).Select(d => d.Date).ToList();

        var weeklyShifts = await _dbContext.WorkShifts
            .Where(shift => employeeIds.Contains(shift.EmployeeUserId) && weekStartsInRange.Contains(shift.WeekStartDate.Date))
            .Select(shift => new
            {
                shift.EmployeeUserId,
                WeekStartDate = shift.WeekStartDate.Date,
                shift.StartTimeUtc,
                shift.EndTimeUtc
            })
            .ToListAsync();

        var weeklyHoursByEmployee = weeklyShifts
            .GroupBy(shift => new { shift.EmployeeUserId, shift.WeekStartDate })
            .Select(group => new
            {
                group.Key.EmployeeUserId,
                group.Key.WeekStartDate,
                TotalHours = group.Sum(shift => (shift.EndTimeUtc - shift.StartTimeUtc).TotalHours)
            })
            .ToList();

        var weeklyHoursLookup = weeklyHoursByEmployee
            .GroupBy(item => item.EmployeeUserId)
            .ToDictionary(
                group => group.Key,
                group => group.ToDictionary(item => item.WeekStartDate, item => item.TotalHours));

        var hoursByEmployee = overlappingShifts
            .GroupBy(shift => shift.EmployeeUserId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(shift => CalculateOverlapHours(shift.StartTimeUtc, shift.EndTimeUtc, rangeStartUtc, rangeEndExclusiveUtc)));

        var rows = new List<PayrollReportItemViewModel>();

        foreach (var employeeId in employeeIds)
        {
            var user = users.GetValueOrDefault(employeeId);
            var employeeName = user?.DisplayName ?? user?.Email ?? user?.UserName ?? employeeId;
            var hourlyRate = user is null || user.DefaultHourlyRate <= 0m ? FallbackHourlyRate : user.DefaultHourlyRate;
            var totalHours = hoursByEmployee.GetValueOrDefault(employeeId);
            var trainingsCompleted = completedTrainingCounts.GetValueOrDefault(employeeId);
            var trainingBonus = trainingsCompleted > 0 ? TrainingCompletionBonus : 0m;
            var totalHoursDecimal = (decimal)totalHours;
            var grossPay = (totalHoursDecimal * hourlyRate) + trainingBonus;

            var warning = BuildWeeklyRequirementWarning(employeeId, weekStartsInRange, weeklyHoursLookup);

            rows.Add(new PayrollReportItemViewModel
            {
                EmployeeId = employeeId,
                EmployeeName = employeeName,
                TotalHoursWorked = decimal.Round(totalHoursDecimal, 2),
                TrainingsCompleted = trainingsCompleted,
                HourlyRate = hourlyRate,
                GrossPay = decimal.Round(grossPay, 2),
                TrainingBonus = trainingBonus,
                WeeklyRequirementWarning = warning
            });
        }

        return rows.OrderBy(r => r.EmployeeName).ToList();
    }

    private static double CalculateOverlapHours(DateTime shiftStartUtc, DateTime shiftEndUtc, DateTime rangeStartUtc, DateTime rangeEndExclusiveUtc)
    {
        var effectiveStart = shiftStartUtc > rangeStartUtc ? shiftStartUtc : rangeStartUtc;
        var effectiveEnd = shiftEndUtc < rangeEndExclusiveUtc ? shiftEndUtc : rangeEndExclusiveUtc;

        if (effectiveEnd <= effectiveStart)
        {
            return 0d;
        }

        return (effectiveEnd - effectiveStart).TotalHours;
    }

    private static List<DateTime> GetWeekStartsInRange(DateTime startDate, DateTime endDate)
    {
        var weekStarts = new List<DateTime>();
        var startWeek = GetWeekStartMonday(startDate.Date);
        var endWeek = GetWeekStartMonday(endDate.Date);

        for (var week = startWeek; week <= endWeek; week = week.AddDays(7))
        {
            weekStarts.Add(week);
        }

        return weekStarts;
    }

    private static DateTime GetWeekStartMonday(DateTime date)
    {
        var diff = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-diff).Date;
    }

    private static string BuildWeeklyRequirementWarning(
        string employeeId,
        IReadOnlyList<DateTime> weekStartsInRange,
        IReadOnlyDictionary<string, Dictionary<DateTime, double>> weeklyHoursLookup)
    {
        if (!weeklyHoursLookup.TryGetValue(employeeId, out var employeeWeeklyHours))
        {
            return "Weekly requirement not met for selected range.";
        }

        var underTargetWeeks = weekStartsInRange
            .Where(week => employeeWeeklyHours.TryGetValue(week, out var hours) && hours < 36d)
            .Select(week => week.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            .ToList();

        return underTargetWeeks.Count == 0
            ? string.Empty
            : $"Weekly requirement not met: {string.Join(", ", underTargetWeeks)}";
    }

    private static string EscapeCsv(string value)
    {
        var escaped = (value ?? string.Empty).Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }
}
