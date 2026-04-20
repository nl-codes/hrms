using System.ComponentModel.DataAnnotations;

namespace HRMS.Models;

public class TrainingSession
{
    public int Id { get; set; }

    [Required, MaxLength(160)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public DateTime SessionDateUtc { get; set; }

    [Range(1, 500)]
    public int MaxEnrollment { get; set; }

    public bool IsActive { get; set; } = true;

    [Required]
    public int InstructorId { get; set; }

    public Instructor? Instructor { get; set; }
    public ICollection<TrainingEnrollment> Enrollments { get; set; } = [];
}
