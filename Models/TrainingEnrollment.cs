using System.ComponentModel.DataAnnotations;
using HRMS.Data;

namespace HRMS.Models;

public enum EnrollmentStatus
{
    Enrolled = 1,
    Completed = 2,
    Absent = 3
}

public class TrainingEnrollment
{
    public int Id { get; set; }

    [Required]
    public int TrainingSessionId { get; set; }

    [Required, MaxLength(450)]
    public string EmployeeUserId { get; set; } = string.Empty;

    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Enrolled;

    public DateTime EnrolledAtUtc { get; set; } = DateTime.UtcNow;

    public TrainingSession? TrainingSession { get; set; }
    public ApplicationUser? EmployeeUser { get; set; }
}
