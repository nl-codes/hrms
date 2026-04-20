using HRMS.Constants;
using HRMS.Data;
using HRMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Controllers;

[Authorize]
public class ApplicationsController(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager) : Controller
{
    private readonly ApplicationDbContext _dbContext = dbContext;
    private readonly UserManager<ApplicationUser> _userManager = userManager;

    [Authorize(Roles = Roles.Applicant)]
    [HttpGet]
    public async Task<IActionResult> Apply(int id)
    {
        var job = await _dbContext.JobPostings.FirstOrDefaultAsync(j => j.Id == id && j.IsActive);
        if (job is null)
        {
            return NotFound("Job posting not found or inactive.");
        }

        return View(new ApplyForJobViewModel(job));
    }

    [Authorize(Roles = Roles.Applicant)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Apply(ApplyForJobViewModel model)
    {
        var job = await _dbContext.JobPostings.FirstOrDefaultAsync(j => j.Id == model.JobPostingId && j.IsActive);
        if (job is null)
        {
            return NotFound("Job posting not found or inactive.");
        }

        if (!ModelState.IsValid)
        {
            model.Title = job.Title;
            model.Description = job.Description;
            model.RequiredExperienceYears = job.RequiredExperienceYears;
            model.RequiredSkillsCsv = job.RequiredSkillsCsv;
            return View(model);
        }

        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var application = new JobApplication
        {
            JobPostingId = model.JobPostingId,
            ApplicantUserId = userId,
            ApplicantExperienceYears = model.ExperienceYears,
            ApplicantSkillsCsv = model.SkillsCsv,
            Status = ApplicationStatus.Applied,
            AppliedAtUtc = DateTime.UtcNow
        };

        RunAutomatedScreening(application, job);

        _dbContext.JobApplications.Add(application);
        await _dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(ApplyConfirmation), new { id = application.Id });
    }

    [Authorize(Roles = Roles.Applicant)]
    [HttpPost]
    [Consumes("application/json")]
    public async Task<IActionResult> Apply([FromBody] ApplyForJobRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var job = await _dbContext.JobPostings.FirstOrDefaultAsync(j => j.Id == request.JobPostingId && j.IsActive);
        if (job is null)
        {
            return NotFound("Job posting not found or inactive.");
        }

        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var application = new JobApplication
        {
            JobPostingId = request.JobPostingId,
            ApplicantUserId = userId,
            ApplicantExperienceYears = request.ExperienceYears,
            ApplicantSkillsCsv = request.SkillsCsv,
            Status = ApplicationStatus.Applied,
            AppliedAtUtc = DateTime.UtcNow
        };

        RunAutomatedScreening(application, job);

        _dbContext.JobApplications.Add(application);
        await _dbContext.SaveChangesAsync();

        return Ok(new
        {
            application.Id,
            application.Status,
            application.AppliedAtUtc,
            application.ScreenedAtUtc
        });
    }

    [Authorize(Roles = Roles.Applicant)]
    [HttpGet]
    public async Task<IActionResult> ApplyConfirmation(int id)
    {
        var application = await _dbContext.JobApplications
            .Include(a => a.JobPosting)
            .FirstOrDefaultAsync(a => a.Id == id);

        return application is null ? NotFound() : View(application);
    }

    [Authorize(Roles = Roles.Applicant)]
    [HttpGet]
    public async Task<IActionResult> MyApplications()
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var applications = await _dbContext.JobApplications
            .Where(a => a.ApplicantUserId == userId)
            .Include(a => a.JobPosting)
            .OrderByDescending(a => a.AppliedAtUtc)
            .Select(a => new ApplicantApplicationItemViewModel
            {
                Id = a.Id,
                JobTitle = a.JobPosting != null ? a.JobPosting.Title : "Unknown",
                Status = a.Status,
                AppliedAtUtc = a.AppliedAtUtc,
                ScreenedAtUtc = a.ScreenedAtUtc,
                HiredAtUtc = a.HiredAtUtc
            })
            .ToListAsync();

        return View(applications);
    }

    [Authorize(Roles = Roles.HiringManager)]
    [HttpGet]
    public async Task<IActionResult> Index(string tab = "all")
    {
        var normalizedTab = NormalizeTab(tab);

        var query = _dbContext.JobApplications
            .Include(a => a.JobPosting)
            .AsQueryable();

        query = normalizedTab switch
        {
            "screened" => query.Where(a => a.Status == ApplicationStatus.Screened),
            "rejected" => query.Where(a => a.Status == ApplicationStatus.Rejected),
            "hired" => query.Where(a => a.Status == ApplicationStatus.Hired),
            _ => query
        };

        var applications = await query
            .OrderByDescending(a => a.AppliedAtUtc)
            .Select(a => new ApplicationReviewItemViewModel
            {
                Id = a.Id,
                ApplicantUserId = a.ApplicantUserId,
                JobTitle = a.JobPosting != null ? a.JobPosting.Title : "Unknown",
                ApplicantExperienceYears = a.ApplicantExperienceYears,
                ApplicantSkillsCsv = a.ApplicantSkillsCsv,
                Status = a.Status,
                AppliedAtUtc = a.AppliedAtUtc,
                ScreenedAtUtc = a.ScreenedAtUtc,
                HiredAtUtc = a.HiredAtUtc
            })
            .ToListAsync();

        var userIds = applications.Select(a => a.ApplicantUserId).Distinct().ToList();
        var emailByUserId = await _userManager.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Email ?? u.UserName ?? "Unknown");

        foreach (var item in applications)
        {
            item.ApplicantEmail = emailByUserId.TryGetValue(item.ApplicantUserId, out var email)
                ? email
                : item.ApplicantUserId;
        }

        var viewModel = new ApplicationsReviewIndexViewModel
        {
            ActiveTab = normalizedTab,
            AllCount = await _dbContext.JobApplications.CountAsync(),
            ScreenedCount = await _dbContext.JobApplications.CountAsync(a => a.Status == ApplicationStatus.Screened),
            RejectedCount = await _dbContext.JobApplications.CountAsync(a => a.Status == ApplicationStatus.Rejected),
            HiredCount = await _dbContext.JobApplications.CountAsync(a => a.Status == ApplicationStatus.Hired),
            Applications = applications
        };

        return View(viewModel);
    }

    [Authorize(Roles = Roles.HiringManager)]
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var application = await _dbContext.JobApplications
            .Include(a => a.JobPosting)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (application is null)
        {
            return NotFound("Application not found.");
        }

        var applicant = await _userManager.FindByIdAsync(application.ApplicantUserId);

        var viewModel = new ApplicationDetailsViewModel
        {
            Id = application.Id,
            ApplicantUserId = application.ApplicantUserId,
            ApplicantEmail = applicant?.Email ?? applicant?.UserName ?? application.ApplicantUserId,
            JobTitle = application.JobPosting?.Title ?? "Unknown",
            JobDescription = application.JobPosting?.Description ?? string.Empty,
            ApplicantExperienceYears = application.ApplicantExperienceYears,
            ApplicantSkillsCsv = application.ApplicantSkillsCsv,
            Status = application.Status,
            AppliedAtUtc = application.AppliedAtUtc,
            ScreenedAtUtc = application.ScreenedAtUtc,
            HiredAtUtc = application.HiredAtUtc,
            CanHire = application.Status != ApplicationStatus.Hired && application.Status != ApplicationStatus.Rejected
        };

        return View(viewModel);
    }

    [Authorize(Roles = Roles.HiringManager)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Hire(int applicationId)
    {
        var application = await _dbContext.JobApplications.FirstOrDefaultAsync(a => a.Id == applicationId);
        if (application is null)
        {
            return NotFound("Application not found.");
        }

        if (application.Status == ApplicationStatus.Rejected)
        {
            return BadRequest("Rejected applicants cannot be hired.");
        }

        var applicant = await _userManager.FindByIdAsync(application.ApplicantUserId);
        if (applicant is null)
        {
            return NotFound("Applicant user account not found.");
        }

        if (!await _userManager.IsInRoleAsync(applicant, Roles.Employee))
        {
            await _userManager.AddToRoleAsync(applicant, Roles.Employee);
        }

        if (await _userManager.IsInRoleAsync(applicant, Roles.Applicant))
        {
            await _userManager.RemoveFromRoleAsync(applicant, Roles.Applicant);
        }

        application.Status = ApplicationStatus.Hired;
        application.HiredAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Application #{application.Id} hired and role changed to {Roles.Employee}.";
        return RedirectToAction(nameof(Details), new { id = application.Id });
    }

    private static string NormalizeTab(string? tab)
    {
        var value = (tab ?? "all").Trim().ToLowerInvariant();
        return value is "all" or "screened" or "rejected" or "hired" ? value : "all";
    }

    private static void RunAutomatedScreening(JobApplication application, JobPosting jobPosting)
    {
        var requiredSkills = ParseCsv(jobPosting.RequiredSkillsCsv);
        var applicantSkills = ParseCsv(application.ApplicantSkillsCsv);

        var hasMinimumExperience = application.ApplicantExperienceYears >= jobPosting.RequiredExperienceYears;
        var hasAllRequiredSkills = requiredSkills.All(skill => applicantSkills.Contains(skill));

        application.Status = hasMinimumExperience && hasAllRequiredSkills
            ? ApplicationStatus.Screened
            : ApplicationStatus.Rejected;
        application.ScreenedAtUtc = DateTime.UtcNow;
    }

    private static HashSet<string> ParseCsv(string csv)
    {
        return csv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.ToLowerInvariant())
            .ToHashSet(StringComparer.Ordinal);
    }
}

public class ApplyForJobRequest
{
    public int JobPostingId { get; set; }
    public int ExperienceYears { get; set; }
    public string SkillsCsv { get; set; } = string.Empty;
}
