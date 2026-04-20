using HRMS.Constants;
using HRMS.Data;
using HRMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Controllers;

[Authorize]
public class TrainingController(ApplicationDbContext dbContext) : Controller
{
    private readonly ApplicationDbContext _dbContext = dbContext;

    [Authorize(Roles = Roles.Instructor + "," + Roles.HiringManager)]
    [HttpGet]
    public async Task<IActionResult> InstructorIndex()
    {
        var query = _dbContext.TrainingSessions
            .Include(s => s.Instructor)
            .ThenInclude(i => i!.User)
            .Include(s => s.Enrollments)
            .AsQueryable();

        if (User.IsInRole(Roles.Instructor) && !User.IsInRole(Roles.HiringManager))
        {
            var instructor = await GetCurrentInstructorAsync();
            if (instructor is null)
            {
                TempData["ErrorMessage"] = "Instructor profile not found. Contact a Hiring Manager.";
                return View(new List<TrainingInstructorListItemViewModel>());
            }

            query = query.Where(s => s.InstructorId == instructor.Id);
        }

        var sessions = await query
            .OrderByDescending(s => s.SessionDateUtc)
            .Select(s => new TrainingInstructorListItemViewModel
            {
                Id = s.Id,
                Title = s.Title,
                SessionDateUtc = s.SessionDateUtc,
                MaxEnrollment = s.MaxEnrollment,
                EnrollmentCount = s.Enrollments.Count,
                IsActive = s.IsActive,
                InstructorName = s.Instructor!.User!.DisplayName ?? s.Instructor.User.Email ?? s.Instructor.User.UserName ?? "Unknown"
            })
            .ToListAsync();

        return View(sessions);
    }

    [Authorize(Roles = Roles.Instructor)]
    [HttpGet]
    public IActionResult Create()
    {
        return View(new TrainingSessionEditViewModel
        {
            SessionDateUtc = DateTime.UtcNow.AddDays(1).Date.AddHours(9),
            MaxEnrollment = 10,
            IsActive = true
        });
    }

    [Authorize(Roles = Roles.Instructor)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TrainingSessionEditViewModel model)
    {
        var instructor = await GetCurrentInstructorAsync();
        if (instructor is null)
        {
            ModelState.AddModelError(string.Empty, "Instructor profile not found. Contact a Hiring Manager.");
            return View(model);
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var session = new TrainingSession
        {
            Title = model.Title.Trim(),
            Description = model.Description.Trim(),
            SessionDateUtc = DateTime.SpecifyKind(model.SessionDateUtc, DateTimeKind.Utc),
            MaxEnrollment = model.MaxEnrollment,
            IsActive = model.IsActive,
            InstructorId = instructor.Id
        };

        _dbContext.TrainingSessions.Add(session);
        await _dbContext.SaveChangesAsync();

        TempData["SuccessMessage"] = "Training session created successfully.";
        return RedirectToAction(nameof(InstructorIndex));
    }

    [Authorize(Roles = Roles.Instructor)]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var session = await GetOwnedSessionAsync(id);
        if (session is null)
        {
            return NotFound();
        }

        return View(new TrainingSessionEditViewModel
        {
            Id = session.Id,
            Title = session.Title,
            Description = session.Description,
            SessionDateUtc = session.SessionDateUtc,
            MaxEnrollment = session.MaxEnrollment,
            IsActive = session.IsActive
        });
    }

    [Authorize(Roles = Roles.Instructor)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, TrainingSessionEditViewModel model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        var session = await GetOwnedSessionAsync(id);
        if (session is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        session.Title = model.Title.Trim();
        session.Description = model.Description.Trim();
        session.SessionDateUtc = DateTime.SpecifyKind(model.SessionDateUtc, DateTimeKind.Utc);
        session.MaxEnrollment = model.MaxEnrollment;
        session.IsActive = model.IsActive;

        await _dbContext.SaveChangesAsync();
        TempData["SuccessMessage"] = "Training session updated successfully.";
        return RedirectToAction(nameof(InstructorIndex));
    }

    [Authorize(Roles = Roles.Instructor)]
    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var session = await GetOwnedSessionAsync(id);
        if (session is null)
        {
            return NotFound();
        }

        return View(new TrainingDeleteViewModel
        {
            Id = session.Id,
            Title = session.Title,
            SessionDateUtc = session.SessionDateUtc,
            MaxEnrollment = session.MaxEnrollment,
            EnrollmentCount = await _dbContext.TrainingEnrollments.CountAsync(e => e.TrainingSessionId == session.Id)
        });
    }

    [Authorize(Roles = Roles.Instructor)]
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var session = await GetOwnedSessionAsync(id);
        if (session is null)
        {
            return NotFound();
        }

        _dbContext.TrainingSessions.Remove(session);
        await _dbContext.SaveChangesAsync();
        TempData["SuccessMessage"] = "Training session deleted.";
        return RedirectToAction(nameof(InstructorIndex));
    }

    [Authorize(Roles = Roles.Instructor + "," + Roles.HiringManager)]
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var sessionQuery = _dbContext.TrainingSessions
            .Include(s => s.Instructor)
            .ThenInclude(i => i!.User)
            .Include(s => s.Enrollments)
            .ThenInclude(e => e.EmployeeUser)
            .AsQueryable();

        var session = await sessionQuery.FirstOrDefaultAsync(s => s.Id == id);
        if (session is null)
        {
            return NotFound();
        }

        if (User.IsInRole(Roles.Instructor) && !User.IsInRole(Roles.HiringManager))
        {
            var instructor = await GetCurrentInstructorAsync();
            if (instructor is null || session.InstructorId != instructor.Id)
            {
                return Forbid();
            }
        }

        var viewModel = new TrainingDetailsViewModel
        {
            Id = session.Id,
            Title = session.Title,
            Description = session.Description,
            SessionDateUtc = session.SessionDateUtc,
            MaxEnrollment = session.MaxEnrollment,
            IsActive = session.IsActive,
            InstructorName = session.Instructor?.User?.DisplayName ?? session.Instructor?.User?.Email ?? "Unknown",
            CanManageAttendance = User.IsInRole(Roles.Instructor),
            Enrollments = session.Enrollments
                .OrderBy(e => e.EmployeeUser!.DisplayName ?? e.EmployeeUser!.Email)
                .Select(e => new TrainingEnrollmentItemViewModel
                {
                    EmployeeUserId = e.EmployeeUserId,
                    EmployeeName = e.EmployeeUser!.DisplayName ?? e.EmployeeUser.Email ?? e.EmployeeUser.UserName ?? e.EmployeeUserId,
                    EmployeeEmail = e.EmployeeUser!.Email ?? e.EmployeeUser.UserName ?? string.Empty,
                    Status = e.Status
                })
                .ToList()
        };

        return View(viewModel);
    }

    [Authorize(Roles = Roles.Instructor)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateEnrollmentStatus(int sessionId, string employeeUserId, EnrollmentStatus status)
    {
        var session = await GetOwnedSessionAsync(sessionId);
        if (session is null)
        {
            return NotFound();
        }

        var enrollment = await _dbContext.TrainingEnrollments
            .FirstOrDefaultAsync(e => e.TrainingSessionId == sessionId && e.EmployeeUserId == employeeUserId);

        if (enrollment is null)
        {
            return NotFound();
        }

        enrollment.Status = status;
        await _dbContext.SaveChangesAsync();

        TempData["SuccessMessage"] = "Attendance status updated.";
        return RedirectToAction(nameof(Details), new { id = sessionId });
    }

    [Authorize(Roles = Roles.Employee)]
    [HttpGet]
    public async Task<IActionResult> AllSessions()
    {
        var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return Unauthorized();
        }

        var sessions = await _dbContext.TrainingSessions
            .Include(s => s.Instructor)
            .ThenInclude(i => i!.User)
            .Include(s => s.Enrollments)
            .Where(s => s.IsActive)
            .OrderBy(s => s.SessionDateUtc)
            .Select(s => new EmployeeTrainingSessionListItemViewModel
            {
                Id = s.Id,
                Title = s.Title,
                Description = s.Description,
                SessionDateUtc = s.SessionDateUtc,
                MaxEnrollment = s.MaxEnrollment,
                EnrollmentCount = s.Enrollments.Count,
                InstructorName = s.Instructor!.User!.DisplayName ?? s.Instructor.User.Email ?? "Unknown",
                IsEnrolled = s.Enrollments.Any(e => e.EmployeeUserId == currentUserId),
                IsFull = s.Enrollments.Count >= s.MaxEnrollment
            })
            .ToListAsync();

        return View(sessions);
    }

    [Authorize(Roles = Roles.Employee)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Enroll(int sessionId)
    {
        var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return Unauthorized();
        }

        var session = await _dbContext.TrainingSessions
            .Include(s => s.Enrollments)
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.IsActive);

        if (session is null)
        {
            TempData["ErrorMessage"] = "Training session not found or inactive.";
            return RedirectToAction(nameof(AllSessions));
        }

        var alreadyEnrolled = session.Enrollments.Any(e => e.EmployeeUserId == currentUserId);
        if (alreadyEnrolled)
        {
            TempData["ErrorMessage"] = "You are already enrolled in this session.";
            return RedirectToAction(nameof(AllSessions));
        }

        if (session.Enrollments.Count >= session.MaxEnrollment)
        {
            TempData["ErrorMessage"] = "This session has reached maximum enrollment.";
            return RedirectToAction(nameof(AllSessions));
        }

        _dbContext.TrainingEnrollments.Add(new TrainingEnrollment
        {
            TrainingSessionId = sessionId,
            EmployeeUserId = currentUserId,
            Status = EnrollmentStatus.Enrolled,
            EnrolledAtUtc = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync();
        TempData["SuccessMessage"] = "You have been enrolled successfully.";
        return RedirectToAction(nameof(AllSessions));
    }

    [Authorize(Roles = Roles.Employee)]
    [HttpGet]
    public async Task<IActionResult> MySessions()
    {
        var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return Unauthorized();
        }

        var sessions = await _dbContext.TrainingEnrollments
            .Where(e => e.EmployeeUserId == currentUserId)
            .Include(e => e.TrainingSession)
            .ThenInclude(s => s!.Instructor)
            .ThenInclude(i => i!.User)
            .OrderByDescending(e => e.TrainingSession!.SessionDateUtc)
            .Select(e => new MyTrainingSessionItemViewModel
            {
                SessionId = e.TrainingSessionId,
                Title = e.TrainingSession!.Title,
                SessionDateUtc = e.TrainingSession.SessionDateUtc,
                InstructorName = e.TrainingSession.Instructor!.User!.DisplayName ?? e.TrainingSession.Instructor.User.Email ?? "Unknown",
                Status = e.Status
            })
            .ToListAsync();

        return View(sessions);
    }

    [Authorize(Roles = Roles.HiringManager)]
    [HttpGet]
    public async Task<IActionResult> AdminIndex()
    {
        var sessions = await _dbContext.TrainingSessions
            .Include(s => s.Instructor)
            .ThenInclude(i => i!.User)
            .Include(s => s.Enrollments)
            .OrderByDescending(s => s.SessionDateUtc)
            .Select(s => new TrainingInstructorListItemViewModel
            {
                Id = s.Id,
                Title = s.Title,
                SessionDateUtc = s.SessionDateUtc,
                MaxEnrollment = s.MaxEnrollment,
                EnrollmentCount = s.Enrollments.Count,
                IsActive = s.IsActive,
                InstructorName = s.Instructor!.User!.DisplayName ?? s.Instructor.User.Email ?? "Unknown"
            })
            .ToListAsync();

        return View(sessions);
    }

    private async Task<Instructor?> GetCurrentInstructorAsync()
    {
        var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return null;
        }

        return await _dbContext.Instructors.FirstOrDefaultAsync(i => i.UserId == currentUserId);
    }

    private async Task<TrainingSession?> GetOwnedSessionAsync(int sessionId)
    {
        var instructor = await GetCurrentInstructorAsync();
        if (instructor is null)
        {
            return null;
        }

        return await _dbContext.TrainingSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.InstructorId == instructor.Id);
    }
}

public class TrainingSessionEditViewModel
{
    public int Id { get; set; }

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(160)]
    public string Title { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Display(Name = "Session Date (UTC)")]
    public DateTime SessionDateUtc { get; set; }

    [System.ComponentModel.DataAnnotations.Range(1, 500)]
    [System.ComponentModel.DataAnnotations.Display(Name = "Max Enrollment")]
    public int MaxEnrollment { get; set; }

    public bool IsActive { get; set; }
}

public class TrainingInstructorListItemViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime SessionDateUtc { get; set; }
    public int MaxEnrollment { get; set; }
    public int EnrollmentCount { get; set; }
    public bool IsActive { get; set; }
    public string InstructorName { get; set; } = string.Empty;
}

public class TrainingEnrollmentItemViewModel
{
    public string EmployeeUserId { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeEmail { get; set; } = string.Empty;
    public EnrollmentStatus Status { get; set; }
}

public class TrainingDetailsViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime SessionDateUtc { get; set; }
    public int MaxEnrollment { get; set; }
    public bool IsActive { get; set; }
    public string InstructorName { get; set; } = string.Empty;
    public bool CanManageAttendance { get; set; }
    public List<TrainingEnrollmentItemViewModel> Enrollments { get; set; } = [];
}

public class EmployeeTrainingSessionListItemViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime SessionDateUtc { get; set; }
    public int MaxEnrollment { get; set; }
    public int EnrollmentCount { get; set; }
    public string InstructorName { get; set; } = string.Empty;
    public bool IsEnrolled { get; set; }
    public bool IsFull { get; set; }
}

public class MyTrainingSessionItemViewModel
{
    public int SessionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime SessionDateUtc { get; set; }
    public string InstructorName { get; set; } = string.Empty;
    public EnrollmentStatus Status { get; set; }
}

public class TrainingDeleteViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime SessionDateUtc { get; set; }
    public int MaxEnrollment { get; set; }
    public int EnrollmentCount { get; set; }
}
