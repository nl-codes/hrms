using HRMS.Constants;
using HRMS.Data;
using HRMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Controllers;

[Authorize]
public class SchedulingController(ApplicationDbContext dbContext) : Controller
{
    private static readonly TimeSpan ShiftDuration = TimeSpan.FromHours(6);
    private readonly ApplicationDbContext _dbContext = dbContext;

    [Authorize(Roles = Roles.Employee + "," + Roles.ProductionManager)]
    [HttpGet]
    public async Task<IActionResult> Index(DateTime? weekStart)
    {
        var currentMondayUtc = GetWeekStartMonday(DateTime.UtcNow.Date);
        var isProductionManagerOnly = User.IsInRole(Roles.ProductionManager) && !User.IsInRole(Roles.Employee);

        if (isProductionManagerOnly)
        {
            var weekOptions = await _dbContext.WorkShifts
                .Select(s => s.WeekStartDate.Date)
                .Distinct()
                .OrderByDescending(d => d)
                .ToListAsync();

            var selectedWeek = weekStart.HasValue ? GetWeekStartMonday(weekStart.Value.Date) : weekOptions.FirstOrDefault();

            var rows = new List<ProductionWeekScheduleItemViewModel>();
            if (selectedWeek != default)
            {
                rows = await _dbContext.WorkShifts
                    .Where(s => s.WeekStartDate.Date == selectedWeek)
                    .GroupBy(s => s.EmployeeUserId)
                    .Select(g => new
                    {
                        EmployeeUserId = g.Key,
                        ShiftCount = g.Count()
                    })
                    .Join(_dbContext.Users,
                        g => g.EmployeeUserId,
                        u => u.Id,
                        (g, u) => new ProductionWeekScheduleItemViewModel
                        {
                            EmployeeUserId = g.EmployeeUserId,
                            EmployeeName = u.DisplayName ?? u.Email ?? u.UserName ?? g.EmployeeUserId,
                            EmployeeEmail = u.Email ?? u.UserName ?? string.Empty,
                            TotalHours = g.ShiftCount * ShiftDuration.TotalHours,
                            Status = g.ShiftCount == 6 ? "Complete" : "Pending"
                        })
                    .OrderBy(r => r.EmployeeName)
                    .ToListAsync();
            }

            return View(new ScheduleIndexViewModel
            {
                IsProductionManagerView = true,
                CurrentMondayUtc = currentMondayUtc,
                SelectedWeekStartUtc = selectedWeek == default ? null : selectedWeek,
                AvailableWeeks = weekOptions,
                ProductionWeekSchedules = rows
            });
        }

        var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return Unauthorized();
        }

        var weeklySummaries = await _dbContext.WorkShifts
            .Where(s => s.EmployeeUserId == currentUserId)
            .GroupBy(s => s.WeekStartDate.Date)
            .Select(g => new ScheduleWeekSummaryViewModel
            {
                WeekStartUtc = g.Key,
                TotalHours = g.Count() * ShiftDuration.TotalHours
            })
            .OrderByDescending(w => w.WeekStartUtc)
            .ToListAsync();

        foreach (var summary in weeklySummaries)
        {
            summary.Status = Math.Abs(summary.TotalHours - 36) < 0.001 ? "Complete" : "Pending";
            summary.CanEdit = summary.WeekStartUtc >= currentMondayUtc;
        }

        return View(new ScheduleIndexViewModel
        {
            IsProductionManagerView = false,
            CurrentMondayUtc = currentMondayUtc,
            ScheduledWeeks = weeklySummaries
        });
    }

    [Authorize(Roles = Roles.Employee)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ScheduleNewWeek(DateTime weekStartUtc)
    {
        if (weekStartUtc.DayOfWeek != DayOfWeek.Monday)
        {
            TempData["ErrorMessage"] = "Please select a Monday as the week start date.";
            return RedirectToAction(nameof(Index));
        }

        var normalizedWeekStart = GetWeekStartMonday(weekStartUtc.Date);
        var currentMondayUtc = GetWeekStartMonday(DateTime.UtcNow.Date);

        if (normalizedWeekStart <= currentMondayUtc)
        {
            TempData["ErrorMessage"] = "Please select a future Monday to schedule a new week.";
            return RedirectToAction(nameof(Index));
        }

        var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return Unauthorized();
        }

        var alreadyScheduled = await _dbContext.WorkShifts.AnyAsync(s =>
            s.EmployeeUserId == currentUserId && s.WeekStartDate.Date == normalizedWeekStart);

        if (alreadyScheduled)
        {
            TempData["ErrorMessage"] = "That week is already scheduled.";
            return RedirectToAction(nameof(Index));
        }

        return RedirectToAction(nameof(Edit), new { weekStart = normalizedWeekStart.ToString("yyyy-MM-dd") });
    }

    [Authorize(Roles = Roles.Employee + "," + Roles.ProductionManager)]
    [HttpGet]
    public async Task<IActionResult> Edit(DateTime weekStart, string? employeeUserId = null)
    {
        var weekStartUtc = GetWeekStartMonday(weekStart.Date);
        var currentMondayUtc = GetWeekStartMonday(DateTime.UtcNow.Date);
        var isProductionManagerOnly = User.IsInRole(Roles.ProductionManager) && !User.IsInRole(Roles.Employee);

        string targetEmployeeUserId;
        if (isProductionManagerOnly)
        {
            targetEmployeeUserId = employeeUserId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(targetEmployeeUserId))
            {
                TempData["ErrorMessage"] = "Select an employee week from the list to view details.";
                return RedirectToAction(nameof(Index), new { weekStart = weekStartUtc.ToString("yyyy-MM-dd") });
            }
        }
        else
        {
            targetEmployeeUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(targetEmployeeUserId))
            {
                return Unauthorized();
            }
        }

        var existingShifts = await _dbContext.WorkShifts
            .Where(s => s.EmployeeUserId == targetEmployeeUserId && s.WeekStartDate.Date == weekStartUtc)
            .OrderBy(s => s.StartTimeUtc)
            .ToListAsync();

        var selectedKeys = existingShifts
            .Select(s => $"{s.StartTimeUtc:yyyyMMddHH}")
            .ToHashSet(StringComparer.Ordinal);

        var isPastWeek = weekStartUtc < currentMondayUtc;
        var isReadOnly = isProductionManagerOnly || isPastWeek;
        if (!isProductionManagerOnly && isPastWeek && existingShifts.Count == 0)
        {
            TempData["ErrorMessage"] = "Past weeks cannot be scheduled.";
            return RedirectToAction(nameof(Index));
        }

        var model = new EmployeeScheduleViewModel(weekStartUtc, BuildWeeklySlots(weekStartUtc, selectedKeys))
        {
            IsReadOnly = isReadOnly,
            IsPastWeek = isPastWeek,
            EmployeeUserId = targetEmployeeUserId,
            PageTitle = isReadOnly ? "Weekly Schedule Details" : "Edit Weekly Schedule"
        };

        return View(model);
    }

    [Authorize(Roles = Roles.Employee)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitMySchedule(EmployeeScheduleViewModel model)
    {
        var weekStartUtc = GetWeekStartMonday(model.WeekStartUtc.Date);
        model.WeekStartUtc = weekStartUtc;
        var currentMondayUtc = GetWeekStartMonday(DateTime.UtcNow.Date);

        if (weekStartUtc < currentMondayUtc)
        {
            TempData["ErrorMessage"] = "Past weeks are read-only and cannot be edited.";
            return RedirectToAction(nameof(Index));
        }

        if (!ModelState.IsValid)
        {
            model.Slots = BuildWeeklySlots(weekStartUtc, model.Slots.Where(slot => slot.IsSelected).Select(slot => slot.Key).ToHashSet(StringComparer.Ordinal));
            model.IsReadOnly = false;
            model.IsPastWeek = false;
            model.PageTitle = "Edit Weekly Schedule";
            return View("Edit", model);
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
                WeekStartDate = weekStartUtc,
                StartTimeUtc = slot.StartTimeUtc,
                EndTimeUtc = slot.EndTimeUtc,
                HourlyRate = slot.HourlyRate
            })
            .ToList();

        var validationErrors = ValidateSixThirtySixRule(shifts, weekStartUtc, weekStartUtc.AddDays(7));
        if (validationErrors.Count > 0)
        {
            model.Slots = BuildWeeklySlots(weekStartUtc, selectedSlots.Select(slot => slot.Key).ToHashSet(StringComparer.Ordinal));
            model.IsReadOnly = false;
            model.IsPastWeek = false;
            model.PageTitle = "Edit Weekly Schedule";
            ViewBag.ValidationErrors = validationErrors;
            return View("Edit", model);
        }

        var existingWeekShifts = await _dbContext.WorkShifts
            .Where(s => s.EmployeeUserId == currentUserId && s.WeekStartDate.Date == weekStartUtc)
            .ToListAsync();

        if (existingWeekShifts.Count > 0)
        {
            _dbContext.WorkShifts.RemoveRange(existingWeekShifts);
        }

        _dbContext.WorkShifts.AddRange(shifts);
        await _dbContext.SaveChangesAsync();

        TempData["SuccessMessage"] = "Weekly schedule saved successfully.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = Roles.Employee + "," + Roles.ProductionManager)]
    [HttpGet]
    public IActionResult MySchedule()
    {
        return RedirectToAction(nameof(Index));
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
                WeekStartDate = weekStartUtc,
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

        for (var dayOffset = 0; dayOffset < 7; dayOffset++)
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

    private static DateTime GetWeekStartMonday(DateTime dateUtc)
    {
        var date = dateUtc.Date;
        var diff = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-diff);
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

public class EmployeeScheduleViewModel
{
    public DateTime WeekStartUtc { get; set; }
    public List<EmployeeShiftSlot> Slots { get; set; } = [];
    public bool IsReadOnly { get; set; }
    public bool IsPastWeek { get; set; }
    public string? EmployeeUserId { get; set; }
    public string PageTitle { get; set; } = "Weekly Schedule";

    public EmployeeScheduleViewModel()
    {
    }

    public EmployeeScheduleViewModel(DateTime weekStartUtc, List<EmployeeShiftSlot> slots)
    {
        WeekStartUtc = weekStartUtc;
        Slots = slots;
    }
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

public class ScheduleIndexViewModel
{
    public bool IsProductionManagerView { get; set; }
    public DateTime CurrentMondayUtc { get; set; }
    public DateTime? SelectedWeekStartUtc { get; set; }
    public List<DateTime> AvailableWeeks { get; set; } = [];
    public List<ScheduleWeekSummaryViewModel> ScheduledWeeks { get; set; } = [];
    public List<ProductionWeekScheduleItemViewModel> ProductionWeekSchedules { get; set; } = [];
}

public class ScheduleWeekSummaryViewModel
{
    public DateTime WeekStartUtc { get; set; }
    public double TotalHours { get; set; }
    public string Status { get; set; } = "Pending";
    public bool CanEdit { get; set; }
}

public class ProductionWeekScheduleItemViewModel
{
    public string EmployeeUserId { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeEmail { get; set; } = string.Empty;
    public double TotalHours { get; set; }
    public string Status { get; set; } = "Pending";
}
