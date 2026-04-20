using System.ComponentModel.DataAnnotations;

namespace HRMS.Models;

public class JobApplication
{
    public int Id { get; set; }

    [Required]
    public int JobPostingId { get; set; }

    [Required, MaxLength(450)]
    public string ApplicantUserId { get; set; } = string.Empty;

    [Range(0, 50)]
    public int ApplicantExperienceYears { get; set; }

    [Required, MaxLength(1000)]
    public string ApplicantSkillsCsv { get; set; } = string.Empty;

    public ApplicationStatus Status { get; set; } = ApplicationStatus.Applied;
    public DateTime AppliedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ScreenedAtUtc { get; set; }
    public DateTime? HiredAtUtc { get; set; }

    public JobPosting? JobPosting { get; set; }
}
