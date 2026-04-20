using System.ComponentModel.DataAnnotations;

namespace HRMS.Models;

public class InstructorListItemViewModel
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
}

public class InstructorCreateViewModel
{
    [Required, StringLength(120)]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 8)]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Required, StringLength(120)]
    [Display(Name = "Domain")]
    public string Domain { get; set; } = string.Empty;
}
