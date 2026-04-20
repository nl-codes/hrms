using HRMS.Constants;
using HRMS.Data;
using HRMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Controllers;

public class ApplicationsController(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager) : Controller
{
    private readonly ApplicationDbContext _dbContext = dbContext;
    private readonly UserManager<ApplicationUser> _userManager = userManager;

    [Authorize(Roles = Roles.Applicant)]
    [HttpPost]
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

    [Authorize(Roles = Roles.HiringManager)]
    [HttpPost]
    public async Task<IActionResult> Hire([FromBody] HireApplicantRequest request)
    {
        var application = await _dbContext.JobApplications.FirstOrDefaultAsync(a => a.Id == request.ApplicationId);
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

        return Ok(new
        {
            application.Id,
            application.Status,
            application.HiredAtUtc,
            RolePromotion = $"{Roles.Applicant} -> {Roles.Employee}"
        });
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

public class HireApplicantRequest
{
    public int ApplicationId { get; set; }
}
