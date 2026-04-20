using System.ComponentModel.DataAnnotations;
using HRMS.Data;

namespace HRMS.Models;

public class Instructor
{
    public int Id { get; set; }

    [Required, MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string Domain { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }
}
