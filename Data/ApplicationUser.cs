using Microsoft.AspNetCore.Identity;
using HRMS.Models;

namespace HRMS.Data;

public class ApplicationUser : IdentityUser
{
    public string? DisplayName { get; set; }
    public ICollection<JobApplication> JobApplications { get; set; } = [];
}
