using HRMS.Constants;
using HRMS.Data;
using HRMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Controllers;

[Authorize(Roles = Roles.HiringManager)]
public class InstructorController(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager) : Controller
{
    private readonly ApplicationDbContext _dbContext = dbContext;
    private readonly UserManager<ApplicationUser> _userManager = userManager;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var instructors = await _dbContext.Instructors
            .Include(i => i.User)
            .OrderBy(i => i.User!.DisplayName)
            .ThenBy(i => i.User!.Email)
            .Select(i => new InstructorListItemViewModel
            {
                Id = i.Id,
                FullName = i.User!.DisplayName ?? i.User.Email ?? i.User.UserName ?? i.UserId,
                Email = i.User!.Email ?? i.User.UserName ?? string.Empty,
                Domain = i.Domain
            })
            .ToListAsync();

        return View(instructors);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new InstructorCreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(InstructorCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var existingUser = await _userManager.FindByEmailAsync(model.Email);
        if (existingUser is not null)
        {
            ModelState.AddModelError(nameof(model.Email), "A user with this email already exists.");
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            EmailConfirmed = true,
            DisplayName = model.FullName
        };

        var createUserResult = await _userManager.CreateAsync(user, model.Password);
        if (!createUserResult.Succeeded)
        {
            foreach (var error in createUserResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        var addRoleResult = await _userManager.AddToRoleAsync(user, Roles.Instructor);
        if (!addRoleResult.Succeeded)
        {
            foreach (var error in addRoleResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            await _userManager.DeleteAsync(user);
            return View(model);
        }

        var instructor = new Instructor
        {
            UserId = user.Id,
            Domain = model.Domain.Trim()
        };

        _dbContext.Instructors.Add(instructor);
        await _dbContext.SaveChangesAsync();

        TempData["SuccessMessage"] = "Instructor account created successfully.";
        return RedirectToAction(nameof(Index));
    }
}
