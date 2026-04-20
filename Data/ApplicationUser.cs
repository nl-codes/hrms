using Microsoft.AspNetCore.Identity;

namespace HRMS.Data;

public class ApplicationUser : IdentityUser
{
    public string? DisplayName { get; set; }
}
