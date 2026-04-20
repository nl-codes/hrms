using System.ComponentModel.DataAnnotations;

namespace HRMS.Models;

public class WorkShift
{
    public int Id { get; set; }

    [Required, MaxLength(450)]
    public string EmployeeUserId { get; set; } = string.Empty;

    [Required]
    public DateTime WeekStartDate { get; set; }

    [Required]
    public DateTime StartTimeUtc { get; set; }

    [Required]
    public DateTime EndTimeUtc { get; set; }

    [Range(0, 1000)]
    public decimal HourlyRate { get; set; }
}
