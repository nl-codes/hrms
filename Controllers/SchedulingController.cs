using HRMS.Constants;
using HRMS.Data;
using HRMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.Controllers;

[Authorize(Roles = Roles.HiringManager)]
public class SchedulingController(ApplicationDbContext dbContext) : Controller
{
    private static readonly TimeSpan ShiftDuration = TimeSpan.FromHours(6);
    private readonly ApplicationDbContext _dbContext = dbContext;

    [HttpPost]
    public async Task<IActionResult> CreateWeeklySchedule([FromBody] CreateWeeklyScheduleRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (request.Shifts.Count == 0)
        {
            return BadRequest("At least one shift is required.");
        }

        var weekStartUtc = request.WeekStartUtc.Date;
        var weekEndUtc = weekStartUtc.AddDays(7);

        var shifts = request.Shifts
            .Select(s => new WorkShift
            {
                EmployeeUserId = s.EmployeeUserId,
                StartTimeUtc = DateTime.SpecifyKind(s.StartTimeUtc, DateTimeKind.Utc),
                EndTimeUtc = DateTime.SpecifyKind(s.EndTimeUtc, DateTimeKind.Utc),
                HourlyRate = s.HourlyRate
            })
            .ToList();

        var validationErrors = ValidateSixThirtySixRule(shifts, weekStartUtc, weekEndUtc);
        if (validationErrors.Count > 0)
        {
            return BadRequest(new { Errors = validationErrors });
        }

        _dbContext.WorkShifts.AddRange(shifts);
        await _dbContext.SaveChangesAsync();

        return Ok(new
        {
            Message = "Weekly schedule created successfully.",
            WeekStartUtc = weekStartUtc,
            WeekEndUtc = weekEndUtc,
            ShiftCount = shifts.Count
        });
    }

    private static List<string> ValidateSixThirtySixRule(IReadOnlyCollection<WorkShift> shifts, DateTime weekStartUtc, DateTime weekEndUtc)
    {
        var errors = new List<string>();

        foreach (var shift in shifts)
        {
            if (shift.StartTimeUtc >= shift.EndTimeUtc)
            {
                errors.Add($"Invalid shift range for employee {shift.EmployeeUserId}.");
                continue;
            }

            if (shift.StartTimeUtc < weekStartUtc || shift.EndTimeUtc > weekEndUtc)
            {
                errors.Add($"Shift for employee {shift.EmployeeUserId} must stay within the selected week.");
            }

            if (shift.EndTimeUtc - shift.StartTimeUtc != ShiftDuration)
            {
                errors.Add($"Shift for employee {shift.EmployeeUserId} must be exactly 6 hours.");
            }
        }

        foreach (var employeeGroup in shifts.GroupBy(s => s.EmployeeUserId))
        {
            var ordered = employeeGroup.OrderBy(s => s.StartTimeUtc).ToList();

            for (var i = 1; i < ordered.Count; i++)
            {
                if (ordered[i].StartTimeUtc < ordered[i - 1].EndTimeUtc)
                {
                    errors.Add($"Overlapping shifts detected for employee {employeeGroup.Key}.");
                }
            }

            var weeklyHours = ordered.Sum(s => (s.EndTimeUtc - s.StartTimeUtc).TotalHours);
            if (Math.Abs(weeklyHours - 36) > 0.001)
            {
                errors.Add($"Employee {employeeGroup.Key} must be scheduled for exactly 36 hours per week.");
            }

            var shiftsByDay = ordered.GroupBy(s => s.StartTimeUtc.Date);
            foreach (var day in shiftsByDay)
            {
                var dayShifts = day.OrderBy(s => s.StartTimeUtc).ToList();
                var dayHours = dayShifts.Sum(s => (s.EndTimeUtc - s.StartTimeUtc).TotalHours);
                if (dayHours > 12)
                {
                    errors.Add($"Employee {employeeGroup.Key} cannot exceed 12 hours in a day.");
                }

                var consecutiveCount = 1;
                for (var i = 1; i < dayShifts.Count; i++)
                {
                    var isConsecutive = dayShifts[i].StartTimeUtc == dayShifts[i - 1].EndTimeUtc;
                    if (isConsecutive)
                    {
                        consecutiveCount++;
                        if (consecutiveCount > 2)
                        {
                            errors.Add($"Employee {employeeGroup.Key} cannot work more than two consecutive shifts.");
                            break;
                        }
                    }
                    else
                    {
                        consecutiveCount = 1;
                    }
                }
            }
        }

        return errors;
    }
}

public class CreateWeeklyScheduleRequest
{
    public DateTime WeekStartUtc { get; set; }
    public List<CreateShiftRequest> Shifts { get; set; } = [];
}

public class CreateShiftRequest
{
    public string EmployeeUserId { get; set; } = string.Empty;
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public decimal HourlyRate { get; set; }
}
