using System.ComponentModel.DataAnnotations;

namespace HRMS.Models;

public class JobPosting
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(4000)]
    public string Description { get; set; } = string.Empty;

    [Range(0, 50)]
    public int RequiredExperienceYears { get; set; }

    [Required, MaxLength(50)]
    public string RequiredDegree { get; set; } = "High School";

    [Required, MaxLength(1000)]
    public string RequiredSkillsCsv { get; set; } = string.Empty;

    [Range(1, 1000)]
    public int OpenPositions { get; set; } = 1;

    public bool IsActive { get; set; } = true;
    public decimal BaseHourlyRate { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<JobApplication> Applications { get; set; } = [];
}
