using HRMS.Constants;
using HRMS.Data;
using HRMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Controllers;

[Authorize(Roles = Roles.HiringManager)]
public class JobPostingsController : Controller
{
    private readonly ApplicationDbContext _dbContext;

    public JobPostingsController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [AllowAnonymous]
    public async Task<IActionResult> Index()
    {
        var jobPostings = await _dbContext.JobPostings
            .OrderByDescending(job => job.CreatedAtUtc)
            .ToListAsync();

        return View(jobPostings);
    }

    [AllowAnonymous]
    public IActionResult Apply(int id)
    {
        return RedirectToAction("Apply", "Applications", new { id });
    }

    public IActionResult Details(int id)
    {
        var jobPosting = _dbContext.JobPostings.FirstOrDefault(job => job.Id == id);
        return jobPosting is null ? NotFound() : View(jobPosting);
    }

    public IActionResult Create()
    {
        return View(new JobPosting());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(JobPosting jobPosting)
    {
        if (!ModelState.IsValid)
        {
            return View(jobPosting);
        }

        jobPosting.CreatedAtUtc = DateTime.UtcNow;
        _dbContext.JobPostings.Add(jobPosting);
        await _dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    public IActionResult Edit(int id)
    {
        var jobPosting = _dbContext.JobPostings.FirstOrDefault(job => job.Id == id);
        return jobPosting is null ? NotFound() : View(jobPosting);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, JobPosting jobPosting)
    {
        if (id != jobPosting.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(jobPosting);
        }

        _dbContext.JobPostings.Update(jobPosting);
        await _dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    public IActionResult Delete(int id)
    {
        var jobPosting = _dbContext.JobPostings.FirstOrDefault(job => job.Id == id);
        return jobPosting is null ? NotFound() : View(jobPosting);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var jobPosting = await _dbContext.JobPostings.FindAsync(id);
        if (jobPosting is null)
        {
            return NotFound();
        }

        _dbContext.JobPostings.Remove(jobPosting);
        await _dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }
}
