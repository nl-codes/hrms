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
    private const decimal WeeklyHourRequirement = 36m;

    private readonly ApplicationDbContext _dbContext = dbContext;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var nowUtc = DateTime.UtcNow;
        var monthStart = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEndExclusive = monthStart.AddMonths(1);

        var activeEmployees = await GetActiveEmployeesAsync(nowUtc);
        var openJobPostingCount = await _dbContext.JobPostings
            .CountAsync(job => job.IsActive && job.OpenPositions > 0);

        var completedTrainingSessionsThisMonth = await _dbContext.TrainingSessions
            .CountAsync(session =>
                session.SessionDateUtc >= monthStart
                && session.SessionDateUtc < monthEndExclusive
                && session.SessionDateUtc <= nowUtc);

        var hiredApplicantCount = await _dbContext.JobApplications
            .CountAsync(application => application.Status == ApplicationStatus.Hired);

        var rejectedApplicantCount = await _dbContext.JobApplications
            .CountAsync(application => application.Status == ApplicationStatus.Rejected);

        var totalDecidedApplicants = hiredApplicantCount + rejectedApplicantCount;
        var hiredPercentage = totalDecidedApplicants == 0
            ? 0m
            : decimal.Round((decimal)hiredApplicantCount * 100m / totalDecidedApplicants, 2);
        var rejectedPercentage = totalDecidedApplicants == 0
            ? 0m
            : decimal.Round((decimal)rejectedApplicantCount * 100m / totalDecidedApplicants, 2);

        return View(new ReportsDashboardViewModel
        {
            ActiveEmployeeCount = activeEmployees.Count,
            OpenJobPostingCount = openJobPostingCount,
            CompletedTrainingSessionsThisMonth = completedTrainingSessionsThisMonth,
            TotalDecidedApplicants = totalDecidedApplicants,
            HiredApplicantCount = hiredApplicantCount,
            RejectedApplicantCount = rejectedApplicantCount,
            HiredPercentage = hiredPercentage,
            RejectedPercentage = rejectedPercentage
        });
    }

    [HttpGet]
    public async Task<IActionResult> WorkHours(string periodType = "week", DateTime? referenceDate = null)
    {
        var normalizedPeriodType = NormalizePeriodType(periodType);
        var effectiveReferenceDate = (referenceDate ?? DateTime.UtcNow).Date;
        var (rangeStartDate, rangeEndDate) = ResolveRange(normalizedPeriodType, effectiveReferenceDate);

        var rangeStartUtc = DateTime.SpecifyKind(rangeStartDate.Date, DateTimeKind.Utc);
        var rangeEndExclusiveUtc = DateTime.SpecifyKind(rangeEndDate.Date.AddDays(1), DateTimeKind.Utc);

        var activeEmployees = await GetActiveEmployeesAsync(DateTime.UtcNow);
        var employeeIds = activeEmployees.Select(employee => employee.Id).ToList();

        var overlappingShifts = employeeIds.Count == 0
            ? []
            : await _dbContext.WorkShifts
                .Where(shift =>
                    employeeIds.Contains(shift.EmployeeUserId)
                    && shift.StartTimeUtc < rangeEndExclusiveUtc
                    && shift.EndTimeUtc > rangeStartUtc)
                .Select(shift => new
                {
                    shift.EmployeeUserId,
                    shift.StartTimeUtc,
                    shift.EndTimeUtc
                })
                .ToListAsync();

        var totalHoursByEmployee = overlappingShifts
            .GroupBy(shift => shift.EmployeeUserId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(shift => CalculateOverlapHours(shift.StartTimeUtc, shift.EndTimeUtc, rangeStartUtc, rangeEndExclusiveUtc)));

        var rows = activeEmployees
            .Select(employee =>
            {
                var totalHours = totalHoursByEmployee.GetValueOrDefault(employee.Id);
                var totalHoursDecimal = decimal.Round((decimal)totalHours, 2);

                return new WorkHoursReportRowViewModel
                {
                    EmployeeId = employee.Id,
                    EmployeeName = employee.Name,
                    RangeStartDate = rangeStartDate,
                    RangeEndDate = rangeEndDate,
                    TotalHoursWorked = totalHoursDecimal,
                    IsBelowWeeklyTarget = normalizedPeriodType == "week" && totalHoursDecimal < WeeklyHourRequirement
                };
            })
            .OrderBy(row => row.EmployeeName)
            .ToList();

        return View(new WorkHoursReportViewModel
        {
            PeriodType = normalizedPeriodType,
            ReferenceDate = effectiveReferenceDate,
            RangeStartDate = rangeStartDate,
            RangeEndDate = rangeEndDate,
            WeeklyTargetHours = WeeklyHourRequirement,
            Rows = rows
        });
    }

    [HttpGet]
    public async Task<IActionResult> TrainingCompletion(bool showIncompleteOnly = false)
    {
        var activeEmployees = await GetActiveEmployeesAsync(DateTime.UtcNow);
        var employeeIds = activeEmployees.Select(employee => employee.Id).ToList();

        var enrollments = employeeIds.Count == 0
            ? []
            : await _dbContext.TrainingEnrollments
                .Where(enrollment => employeeIds.Contains(enrollment.EmployeeUserId))
                .Include(enrollment => enrollment.TrainingSession)
                .Select(enrollment => new
                {
                    enrollment.EmployeeUserId,
                    enrollment.Status,
                    SessionTitle = enrollment.TrainingSession != null ? enrollment.TrainingSession.Title : "Unknown",
                    SessionDateUtc = enrollment.TrainingSession != null ? enrollment.TrainingSession.SessionDateUtc : DateTime.MinValue
                })
                .ToListAsync();

        var enrollmentLookup = enrollments
            .GroupBy(enrollment => enrollment.EmployeeUserId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var rows = activeEmployees
            .Select(employee =>
            {
                var employeeEnrollments = enrollmentLookup.GetValueOrDefault(employee.Id) ?? [];
                var totalSessionsEnrolled = employeeEnrollments.Count;
                var sessionsCompleted = employeeEnrollments.Count(enrollment => enrollment.Status == EnrollmentStatus.Completed);
                var incompleteOrAbsentCount = employeeEnrollments.Count(enrollment => enrollment.Status != EnrollmentStatus.Completed);
                var completionRate = totalSessionsEnrolled == 0
                    ? 0m
                    : decimal.Round((decimal)sessionsCompleted * 100m / totalSessionsEnrolled, 2);

                return new TrainingCompletionEmployeeRowViewModel
                {
                    EmployeeId = employee.Id,
                    EmployeeName = employee.Name,
                    TotalSessionsEnrolled = totalSessionsEnrolled,
                    SessionsCompleted = sessionsCompleted,
                    IncompleteOrAbsentCount = incompleteOrAbsentCount,
                    CompletionRate = completionRate
                };
            })
            .OrderBy(row => row.EmployeeName)
            .ToList();

        if (showIncompleteOnly)
        {
            rows = rows.Where(row => row.IncompleteOrAbsentCount > 0).ToList();
        }

        var employeeNameById = activeEmployees.ToDictionary(employee => employee.Id, employee => employee.Name);

        var incompleteRecords = showIncompleteOnly
            ? enrollments
                .Where(enrollment => enrollment.Status != EnrollmentStatus.Completed)
                .Select(enrollment => new TrainingIncompleteRecordViewModel
                {
                    EmployeeName = employeeNameById.GetValueOrDefault(enrollment.EmployeeUserId, enrollment.EmployeeUserId),
                    SessionTitle = enrollment.SessionTitle,
                    SessionDateUtc = enrollment.SessionDateUtc,
                    Status = enrollment.Status
                })
                .OrderBy(record => record.EmployeeName)
                .ThenBy(record => record.SessionDateUtc)
                .ToList()
            : [];

        return View(new TrainingCompletionReportViewModel
        {
            ShowIncompleteOnly = showIncompleteOnly,
            Rows = rows,
            IncompleteRecords = incompleteRecords
        });
    }

    [HttpGet]
    public async Task<IActionResult> HiringFunnel()
    {
        var totalApplications = await _dbContext.JobApplications.CountAsync();
        var screenedCount = await _dbContext.JobApplications.CountAsync(application => application.Status == ApplicationStatus.Screened);
        var rejectedCount = await _dbContext.JobApplications.CountAsync(application => application.Status == ApplicationStatus.Rejected);
        var hiredCount = await _dbContext.JobApplications.CountAsync(application => application.Status == ApplicationStatus.Hired);

        var automatedRejectionReasons = new[]
        {
            "Insufficient experience.",
            "Degree requirement not met."
        };

        var automatedRejectedCount = await _dbContext.JobApplications
            .CountAsync(application =>
                application.Status == ApplicationStatus.Rejected
                && application.RejectionReason != null
                && automatedRejectionReasons.Contains(application.RejectionReason));

        var acceptanceRate = totalApplications == 0
            ? 0m
            : decimal.Round((decimal)hiredCount * 100m / totalApplications, 2);

        return View(new HiringFunnelReportViewModel
        {
            TotalApplications = totalApplications,
            ScreenedCount = screenedCount,
            RejectedCount = rejectedCount,
            AutomatedRejectedCount = automatedRejectedCount,
            HiredCount = hiredCount,
            AcceptanceRate = acceptanceRate
        });
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

    private static DateTime GetWeekStartMonday(DateTime date)
    {
        var diff = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-diff).Date;
    }

    private async Task<List<ReportEmployeeIdentity>> GetActiveEmployeesAsync(DateTime nowUtc)
    {
        var employeeRoleId = await _dbContext.Roles
            .Where(role => role.Name == Roles.Employee)
            .Select(role => role.Id)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(employeeRoleId))
        {
            return [];
        }

        return await (
                from user in _dbContext.Users
                join userRole in _dbContext.UserRoles on user.Id equals userRole.UserId
                where userRole.RoleId == employeeRoleId && (user.LockoutEnd == null || user.LockoutEnd <= nowUtc)
                select new ReportEmployeeIdentity
                {
                    Id = user.Id,
                    Name = user.DisplayName ?? user.Email ?? user.UserName ?? user.Id
                })
            .Distinct()
            .OrderBy(employee => employee.Name)
            .ToListAsync();
    }

    private static string NormalizePeriodType(string? periodType)
    {
        // Always return "week" - monthly reports have been removed
        return "week";
    }

    private static (DateTime StartDate, DateTime EndDate) ResolveRange(string periodType, DateTime referenceDate)
    {
        // Only support weekly ranges
        var weekStart = GetWeekStartMonday(referenceDate.Date);
        return (weekStart, weekStart.AddDays(6));
    }

    private sealed class ReportEmployeeIdentity
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
