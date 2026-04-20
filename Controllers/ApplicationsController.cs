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
        var job = await _dbContext.JobPostings.FirstOrDefaultAsync(j => j.Id == id);
        if (job is null)
        {
            return NotFound("Job posting not found.");
        }

        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var latestApplication = await GetLatestApplicationForUserAndJobAsync(userId, job.Id);
        var applicationCount = await GetApplicationCountForUserAndJobAsync(userId, job.Id);

        if (latestApplication is not null)
        {
            if (latestApplication.Status == ApplicationStatus.Rejected)
            {
                if (applicationCount >= 3)
                {
                    TempData["ErrorMessage"] = "You have reached the reapply limit for this job posting.";
                    return RedirectToAction(nameof(Details), new { id = latestApplication.Id });
                }

                if (!job.IsActive)
                {
                    return View("HiringCompleted", new HiringCompletedViewModel
                    {
                        JobPostingId = job.Id,
                        Title = job.Title,
                        OpenPositions = NormalizeOpenPositions(job),
                        HiredCount = await GetHiredCountForJobAsync(job.Id)
                    });
                }

                return View(BuildApplyViewModel(job, latestApplication, isEditing: false, isReapply: true));
            }

            TempData["ErrorMessage"] = "You already have an active application for this job. You can reapply only if it is rejected.";
            return RedirectToAction(nameof(Details), new { id = latestApplication.Id });
        }

        var openPositions = NormalizeOpenPositions(job);
        var hiredCount = await GetHiredCountForJobAsync(job.Id);
        if (!job.IsActive || hiredCount >= openPositions)
        {
            if (job.IsActive && hiredCount >= openPositions)
            {
                job.IsActive = false;
                await _dbContext.SaveChangesAsync();
            }

            return View("HiringCompleted", new HiringCompletedViewModel
            {
                JobPostingId = job.Id,
                Title = job.Title,
                OpenPositions = openPositions,
                HiredCount = hiredCount
            });
        }

        return View(new ApplyForJobViewModel(job));
    }

    [Authorize(Roles = Roles.Applicant)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Apply(ApplyForJobViewModel model)
    {
        var job = await _dbContext.JobPostings.FirstOrDefaultAsync(j => j.Id == model.JobPostingId);
        if (job is null)
        {
            return NotFound("Job posting not found.");
        }

        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var latestApplication = await GetLatestApplicationForUserAndJobAsync(userId, job.Id);
        var applicationCount = await GetApplicationCountForUserAndJobAsync(userId, job.Id);

        if (latestApplication is not null && latestApplication.Status is ApplicationStatus.Applied or ApplicationStatus.Screened or ApplicationStatus.Hired)
        {
            TempData["ErrorMessage"] = "You already have an active application for this job. You can reapply only if it is rejected.";
            return RedirectToAction(nameof(Details), new { id = latestApplication.Id });
        }

        if (latestApplication is not null && latestApplication.Status == ApplicationStatus.Rejected && applicationCount >= 3)
        {
            TempData["ErrorMessage"] = "You have reached the reapply limit for this job posting.";
            return RedirectToAction(nameof(Details), new { id = latestApplication.Id });
        }

        var openPositions = NormalizeOpenPositions(job);
        var hiredCount = await GetHiredCountForJobAsync(job.Id);
        if (!job.IsActive || hiredCount >= openPositions)
        {
            if (job.IsActive && hiredCount >= openPositions)
            {
                job.IsActive = false;
                await _dbContext.SaveChangesAsync();
            }

            return View("HiringCompleted", new HiringCompletedViewModel
            {
                JobPostingId = job.Id,
                Title = job.Title,
                OpenPositions = openPositions,
                HiredCount = hiredCount
            });
        }

        if (!ModelState.IsValid)
        {
            PopulateApplyViewModel(model, job);
            model.IsReapply = latestApplication?.Status == ApplicationStatus.Rejected;
            model.IsEditing = false;
            return View(model);
        }

        var application = new JobApplication
        {
            JobPostingId = model.JobPostingId,
            ApplicantUserId = userId,
            ApplicantExperienceYears = model.ExperienceYears,
            HighestDegree = model.HighestDegree,
            ApplicantPhone = model.ApplicantPhone,
            CoverLetter = model.CoverLetter,
            ApplicantSkillsCsv = model.SkillsCsv,
            AttemptNumber = applicationCount + 1,
            Status = ApplicationStatus.Applied,
            AppliedAtUtc = DateTime.UtcNow,
            SubmittedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
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

        var job = await _dbContext.JobPostings.FirstOrDefaultAsync(j => j.Id == request.JobPostingId);
        if (job is null)
        {
            return NotFound("Job posting not found.");
        }

        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var latestApplication = await GetLatestApplicationForUserAndJobAsync(userId, job.Id);
        var applicationCount = await GetApplicationCountForUserAndJobAsync(userId, job.Id);

        if (latestApplication is not null && latestApplication.Status is ApplicationStatus.Applied or ApplicationStatus.Screened or ApplicationStatus.Hired)
        {
            return BadRequest(new { message = "You already have an active application for this job. You can reapply only if it is rejected." });
        }

        if (latestApplication is not null && latestApplication.Status == ApplicationStatus.Rejected && applicationCount >= 3)
        {
            return BadRequest(new { message = "You have reached the reapply limit for this job posting." });
        }

        var openPositions = NormalizeOpenPositions(job);
        var hiredCount = await GetHiredCountForJobAsync(job.Id);
        if (!job.IsActive || hiredCount >= openPositions)
        {
            if (job.IsActive && hiredCount >= openPositions)
            {
                job.IsActive = false;
                await _dbContext.SaveChangesAsync();
            }

            return BadRequest(new { message = "Hiring is completed for this job posting." });
        }

        var application = new JobApplication
        {
            JobPostingId = request.JobPostingId,
            ApplicantUserId = userId,
            ApplicantExperienceYears = request.ExperienceYears,
            HighestDegree = request.HighestDegree,
            ApplicantPhone = request.ApplicantPhone,
            CoverLetter = request.CoverLetter,
            ApplicantSkillsCsv = request.SkillsCsv,
            AttemptNumber = applicationCount + 1,
            Status = ApplicationStatus.Applied,
            AppliedAtUtc = DateTime.UtcNow,
            SubmittedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        RunAutomatedScreening(application, job);

        _dbContext.JobApplications.Add(application);
        await _dbContext.SaveChangesAsync();

        return Ok(new
        {
            application.Id,
            application.Status,
            application.AppliedAtUtc,
            application.ScreenedAtUtc,
            application.AttemptNumber
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
            .ToListAsync();

        var latestApplicationByJob = applications
            .GroupBy(a => a.JobPostingId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(a => a.AppliedAtUtc).ThenByDescending(a => a.Id).First());

        var applicationCountByJob = applications
            .GroupBy(a => a.JobPostingId)
            .ToDictionary(group => group.Key, group => group.Count());

        var viewModels = applications
            .Select(a =>
            {
                var latest = latestApplicationByJob[a.JobPostingId];
                var attemptCount = applicationCountByJob[a.JobPostingId];

                return new ApplicantApplicationItemViewModel
                {
                    Id = a.Id,
                    JobPostingId = a.JobPostingId,
                    JobTitle = a.JobPosting != null ? a.JobPosting.Title : "Unknown",
                    Status = a.Status,
                    AppliedAtUtc = a.AppliedAtUtc,
                    ScreenedAtUtc = a.ScreenedAtUtc,
                    HiredAtUtc = a.HiredAtUtc,
                    CanEdit = false,
                    CanReapply = latest.Id == a.Id && a.Status == ApplicationStatus.Rejected && attemptCount < 3
                };
            })
            .ToList();

        return View(viewModels);
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
                HighestDegree = a.HighestDegree,
                ApplicantPhone = a.ApplicantPhone,
                ApplicantSkillsCsv = a.ApplicantSkillsCsv,
                CoverLetter = a.CoverLetter,
                RejectionReason = a.RejectionReason,
                Status = a.Status,
                AppliedAtUtc = a.AppliedAtUtc,
                SubmittedAt = a.SubmittedAt,
                UpdatedAt = a.UpdatedAt,
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

    [Authorize]
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
        var currentUserId = _userManager.GetUserId(User);
        var isApplicantOwner = !string.IsNullOrWhiteSpace(currentUserId) && currentUserId == application.ApplicantUserId;

        if (!User.IsInRole(Roles.HiringManager) && !isApplicantOwner)
        {
            return Forbid();
        }

        var applicationCountForJob = await _dbContext.JobApplications
            .CountAsync(a => a.ApplicantUserId == application.ApplicantUserId && a.JobPostingId == application.JobPostingId);

        var latestApplication = await GetLatestApplicationForUserAndJobAsync(application.ApplicantUserId, application.JobPostingId);
        var canEditOwnApplication = false;
        var canReapplyOwnApplication = isApplicantOwner && latestApplication?.Id == application.Id && application.Status == ApplicationStatus.Rejected && applicationCountForJob < 3;

        var viewModel = new ApplicationDetailsViewModel
        {
            Id = application.Id,
            JobPostingId = application.JobPostingId,
            ApplicantUserId = application.ApplicantUserId,
            ApplicantEmail = applicant?.Email ?? applicant?.UserName ?? application.ApplicantUserId,
            JobTitle = application.JobPosting?.Title ?? "Unknown",
            JobDescription = application.JobPosting?.Description ?? string.Empty,
            ApplicantExperienceYears = application.ApplicantExperienceYears,
            HighestDegree = application.HighestDegree,
            ApplicantPhone = application.ApplicantPhone,
            CoverLetter = application.CoverLetter,
            ApplicantSkillsCsv = application.ApplicantSkillsCsv,
            RejectionReason = application.RejectionReason,
            AttemptNumber = application.AttemptNumber,
            Status = application.Status,
            AppliedAtUtc = application.AppliedAtUtc,
            SubmittedAt = application.SubmittedAt,
            UpdatedAt = application.UpdatedAt,
            ScreenedAtUtc = application.ScreenedAtUtc,
            HiredAtUtc = application.HiredAtUtc,
            CanEdit = canEditOwnApplication,
            CanReapply = canReapplyOwnApplication,
            CanScreen = User.IsInRole(Roles.HiringManager) && application.Status == ApplicationStatus.Applied,
            CanReject = User.IsInRole(Roles.HiringManager) && (application.Status == ApplicationStatus.Applied || application.Status == ApplicationStatus.Screened),
            CanHire = User.IsInRole(Roles.HiringManager) && application.Status == ApplicationStatus.Screened
        };

        return View(viewModel);
    }

    [Authorize(Roles = Roles.HiringManager)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Screen(int applicationId)
    {
        var application = await _dbContext.JobApplications.FirstOrDefaultAsync(a => a.Id == applicationId);
        if (application is null)
        {
            return NotFound("Application not found.");
        }

        if (application.Status == ApplicationStatus.Hired)
        {
            return BadRequest("Hired applicants cannot be screened.");
        }

        if (application.Status == ApplicationStatus.Rejected)
        {
            return BadRequest("Rejected applications cannot be screened.");
        }

        application.Status = ApplicationStatus.Screened;
        application.ScreenedAtUtc = DateTime.UtcNow;
        application.RejectionReason = null;
        application.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Application #{application.Id} screened successfully.";
        return RedirectToAction(nameof(Details), new { id = application.Id });
    }

    [Authorize(Roles = Roles.HiringManager)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int applicationId)
    {
        var application = await _dbContext.JobApplications.FirstOrDefaultAsync(a => a.Id == applicationId);
        if (application is null)
        {
            return NotFound("Application not found.");
        }

        if (application.Status == ApplicationStatus.Hired)
        {
            return BadRequest("Hired applicants cannot be rejected.");
        }

        application.Status = ApplicationStatus.Rejected;
        application.ScreenedAtUtc = DateTime.UtcNow;
        application.RejectionReason = "Rejected after manager review.";
        application.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Application #{application.Id} rejected successfully.";
        return RedirectToAction(nameof(Details), new { id = application.Id });
    }

    [Authorize(Roles = Roles.HiringManager)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Hire(int applicationId)
    {
        var application = await _dbContext.JobApplications
            .Include(a => a.JobPosting)
            .FirstOrDefaultAsync(a => a.Id == applicationId);
        if (application is null)
        {
            return NotFound("Application not found.");
        }

        if (application.JobPosting is null)
        {
            return NotFound("Job posting not found for this application.");
        }

        var openPositions = NormalizeOpenPositions(application.JobPosting);
        var hiredCountBefore = await GetHiredCountForJobAsync(application.JobPostingId);
        if (hiredCountBefore >= openPositions)
        {
            application.JobPosting.IsActive = false;
            await _dbContext.SaveChangesAsync();

            TempData["SuccessMessage"] = "Hiring is completed for this job posting.";
            return RedirectToAction(nameof(Details), new { id = application.Id });
        }

        if (application.Status == ApplicationStatus.Rejected)
        {
            return BadRequest("Rejected applicants cannot be hired.");
        }

        if (application.Status != ApplicationStatus.Screened)
        {
            return BadRequest("Applicants must be screened before they can be hired.");
        }

        if (application.Status == ApplicationStatus.Hired)
        {
            TempData["SuccessMessage"] = "This applicant is already hired.";
            return RedirectToAction(nameof(Details), new { id = application.Id });
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
        application.RejectionReason = null;
        application.UpdatedAt = DateTime.UtcNow;

        var hiredCountAfter = hiredCountBefore + 1;
        if (hiredCountAfter >= openPositions)
        {
            application.JobPosting.IsActive = false;
        }

        await _dbContext.SaveChangesAsync();

        TempData["SuccessMessage"] = hiredCountAfter >= openPositions
            ? $"Application #{application.Id} hired. Hiring is completed for this posting."
            : $"Application #{application.Id} hired and role changed to {Roles.Employee}.";
        return RedirectToAction(nameof(Details), new { id = application.Id });
    }

    private ApplyForJobViewModel BuildApplyViewModel(JobPosting jobPosting, JobApplication application, bool isEditing, bool isReapply)
    {
        return new ApplyForJobViewModel(jobPosting)
        {
            ApplicationId = isEditing ? application.Id : null,
            ExperienceYears = application.ApplicantExperienceYears,
            HighestDegree = application.HighestDegree,
            ApplicantPhone = application.ApplicantPhone,
            CoverLetter = application.CoverLetter,
            SkillsCsv = application.ApplicantSkillsCsv,
            IsEditing = isEditing,
            IsReapply = isReapply
        };
    }

    private static void PopulateApplyViewModel(ApplyForJobViewModel model, JobPosting jobPosting)
    {
        model.Title = jobPosting.Title;
        model.Description = jobPosting.Description;
        model.RequiredExperienceYears = jobPosting.RequiredExperienceYears;
        model.RequiredDegree = jobPosting.RequiredDegree;
        model.RequiredSkillsCsv = jobPosting.RequiredSkillsCsv;
        model.OpenPositions = jobPosting.OpenPositions;
    }

    private async Task<JobApplication?> GetLatestApplicationForUserAndJobAsync(string userId, int jobPostingId)
    {
        return await _dbContext.JobApplications
            .Where(a => a.ApplicantUserId == userId && a.JobPostingId == jobPostingId)
            .OrderByDescending(a => a.AppliedAtUtc)
            .ThenByDescending(a => a.Id)
            .FirstOrDefaultAsync();
    }

    private Task<int> GetApplicationCountForUserAndJobAsync(string userId, int jobPostingId)
    {
        return _dbContext.JobApplications.CountAsync(a => a.ApplicantUserId == userId && a.JobPostingId == jobPostingId);
    }

    private static int NormalizeOpenPositions(JobPosting jobPosting)
    {
        return jobPosting.OpenPositions > 0 ? jobPosting.OpenPositions : 1;
    }

    private static void RunAutomatedScreening(JobApplication application, JobPosting jobPosting)
    {
        if (application.ApplicantExperienceYears < jobPosting.RequiredExperienceYears)
        {
            application.Status = ApplicationStatus.Rejected;
            application.RejectionReason = "Insufficient experience.";
            application.ScreenedAtUtc = DateTime.UtcNow;
            application.UpdatedAt = DateTime.UtcNow;
            return;
        }

        var applicantDegreeRank = GetDegreeRank(application.HighestDegree);
        var requiredDegreeRank = GetDegreeRank(jobPosting.RequiredDegree);
        if (applicantDegreeRank < requiredDegreeRank)
        {
            application.Status = ApplicationStatus.Rejected;
            application.RejectionReason = "Degree requirement not met.";
            application.ScreenedAtUtc = DateTime.UtcNow;
            application.UpdatedAt = DateTime.UtcNow;
            return;
        }

        application.Status = ApplicationStatus.Screened;
        application.RejectionReason = null;
        application.ScreenedAtUtc = DateTime.UtcNow;
        application.UpdatedAt = DateTime.UtcNow;
    }

    private static int GetDegreeRank(string? degree)
    {
        var normalized = (degree ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "post-graduate" => 4,
            "postgraduate" => 4,
            "master" => 4,
            "doctorate" => 5,
            "phd" => 5,
            "undergraduate" => 3,
            "under-graduate" => 3,
            "bachelor" => 3,
            "high school" => 2,
            "highschool" => 2,
            "uneducated" => 1,
            _ => 0
        };
    }

    private Task<int> GetHiredCountForJobAsync(int jobPostingId)
    {
        return _dbContext.JobApplications.CountAsync(a => a.JobPostingId == jobPostingId && a.Status == ApplicationStatus.Hired);
    }

    private static string NormalizeTab(string? tab)
    {
        var value = (tab ?? "all").Trim().ToLowerInvariant();
        return value is "all" or "screened" or "rejected" or "hired" ? value : "all";
    }

}

public class ApplyForJobRequest
{
    public int JobPostingId { get; set; }
    public int ExperienceYears { get; set; }
    public string HighestDegree { get; set; } = string.Empty;
    public string ApplicantPhone { get; set; } = string.Empty;
    public string CoverLetter { get; set; } = string.Empty;
    public string SkillsCsv { get; set; } = string.Empty;
}
