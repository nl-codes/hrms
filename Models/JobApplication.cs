using System.ComponentModel.DataAnnotations;
using HRMS.Data;

namespace HRMS.Models;

public class JobApplication
{
    public int Id { get; set; }

    [Required]
    public int JobPostingId { get; set; }

    [Required, MaxLength(450)]
    public string ApplicantUserId { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string HighestDegree { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    public string ApplicantPhone { get; set; } = string.Empty;

    [Required, MaxLength(4000)]
    public string CoverLetter { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? RejectionReason { get; set; }

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Range(0, 50)]
    public int ApplicantExperienceYears { get; set; }

    [Required, MaxLength(1000)]
    public string ApplicantSkillsCsv { get; set; } = string.Empty;

    public int AttemptNumber { get; set; } = 1;

    public ApplicationStatus Status { get; set; } = ApplicationStatus.Applied;
    public DateTime AppliedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ScreenedAtUtc { get; set; }
    public DateTime? HiredAtUtc { get; set; }

    public JobPosting? JobPosting { get; set; }
    public ApplicationUser? ApplicantUser { get; set; }
}
