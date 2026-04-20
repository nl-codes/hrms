using HRMS.Constants;
using HRMS.Data;
using HRMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.Controllers;

[Authorize]
public class SchedulingController(ApplicationDbContext dbContext) : Controller
{
    private static readonly TimeSpan ShiftDuration = TimeSpan.FromHours(6);
    private readonly ApplicationDbContext _dbContext = dbContext;

    [Authorize(Roles = Roles.Employee)]
    [HttpGet]
    public IActionResult Index()
    {
        return RedirectToAction(nameof(MySchedule));
    }

    [Authorize(Roles = Roles.Employee + "," + Roles.ProductionManager)]
    [HttpGet]
    public IActionResult MySchedule()
    {
        var weekStartUtc = DateTime.UtcNow.Date;
        return View(new EmployeeScheduleViewModel(weekStartUtc, BuildWeeklySlots(weekStartUtc)));
    }

    [Authorize(Roles = Roles.Employee)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitMySchedule(EmployeeScheduleViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.Slots = BuildWeeklySlots(model.WeekStartUtc.Date, model.Slots.Where(slot => slot.IsSelected).Select(slot => slot.Key).ToHashSet(StringComparer.Ordinal));
            return View("MySchedule", model);
        }

        var selectedSlots = model.Slots.Where(slot => slot.IsSelected).ToList();
        var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return Unauthorized();
        }

        var shifts = selectedSlots
            .Select(slot => new WorkShift
            {
                EmployeeUserId = currentUserId,
                StartTimeUtc = slot.StartTimeUtc,
                EndTimeUtc = slot.EndTimeUtc,
                HourlyRate = slot.HourlyRate
            })
            .ToList();

        var validationErrors = ValidateSixThirtySixRule(shifts, model.WeekStartUtc.Date, model.WeekStartUtc.Date.AddDays(7));
        if (validationErrors.Count > 0)
        {
            model.Slots = BuildWeeklySlots(model.WeekStartUtc.Date, selectedSlots.Select(slot => slot.Key).ToHashSet(StringComparer.Ordinal));
            ViewBag.ValidationErrors = validationErrors;
            return View("MySchedule", model);
        }

        _dbContext.WorkShifts.AddRange(shifts);
        await _dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(MySchedule));
    }

    [HttpPost]
    [Authorize(Roles = Roles.HiringManager)]
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

    private static List<EmployeeShiftSlot> BuildWeeklySlots(DateTime weekStartUtc, HashSet<string>? selectedKeys = null)
    {
        var slots = new List<EmployeeShiftSlot>();

        for (var dayOffset = 0; dayOffset < 6; dayOffset++)
        {
            var dayStart = weekStartUtc.AddDays(dayOffset);

            for (var block = 0; block < 2; block++)
            {
                var start = dayStart.AddHours(8 + (block * 6));
                var end = start.AddHours(6);
                var key = $"{start:yyyyMMddHH}";

                slots.Add(new EmployeeShiftSlot
                {
                    Key = key,
                    DayLabel = start.ToString("ddd, MMM d"),
                    StartLabel = start.ToString("HH:mm"),
                    EndLabel = end.ToString("HH:mm"),
                    StartTimeUtc = DateTime.SpecifyKind(start, DateTimeKind.Utc),
                    EndTimeUtc = DateTime.SpecifyKind(end, DateTimeKind.Utc),
                    HourlyRate = 0,
                    IsSelected = selectedKeys?.Contains(key) == true
                });
            }
        }

        return slots;
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

public class EmployeeScheduleViewModel(DateTime weekStartUtc, List<EmployeeShiftSlot> slots)
{
    public DateTime WeekStartUtc { get; set; } = weekStartUtc;
    public List<EmployeeShiftSlot> Slots { get; set; } = slots;
}

public class EmployeeShiftSlot
{
    public string Key { get; set; } = string.Empty;
    public string DayLabel { get; set; } = string.Empty;
    public string StartLabel { get; set; } = string.Empty;
    public string EndLabel { get; set; } = string.Empty;
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public decimal HourlyRate { get; set; }
    public bool IsSelected { get; set; }
}
