using HRMS.Constants;
using Microsoft.AspNetCore.Identity;

namespace HRMS.Data;

public static class IdentitySeed
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var roles = new[]
        {
            Roles.HiringManager,
            Roles.Applicant,
            Roles.Employee,
            Roles.Instructor,
            Roles.ProductionManager
        };

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        const string adminEmail = "hiring.manager@wbs.local";
        const string adminPassword = "hiring.manager@wbs.local";

        var admin = await userManager.FindByEmailAsync(adminEmail);
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                DisplayName = "WBS Hiring Manager"
            };

            var createResult = await userManager.CreateAsync(admin, adminPassword);
            if (!createResult.Succeeded)
            {
                var error = string.Join("; ", createResult.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Unable to seed admin user: {error}");
            }
        }

        if (!await userManager.IsInRoleAsync(admin, Roles.HiringManager))
        {
            await userManager.AddToRoleAsync(admin, Roles.HiringManager);
        }
    }
}
