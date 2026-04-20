using HRMS.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<JobPosting> JobPostings => Set<JobPosting>();
    public DbSet<JobApplication> JobApplications => Set<JobApplication>();
    public DbSet<Instructor> Instructors => Set<Instructor>();
    public DbSet<WorkShift> WorkShifts => Set<WorkShift>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<JobPosting>()
            .HasMany(j => j.Applications)
            .WithOne(a => a.JobPosting)
            .HasForeignKey(a => a.JobPostingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<JobApplication>()
            .Property(a => a.AttemptNumber)
            .HasDefaultValue(1);

        builder.Entity<Instructor>()
            .HasOne(i => i.User)
            .WithMany()
            .HasForeignKey(i => i.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Instructor>()
            .HasIndex(i => i.UserId)
            .IsUnique();

        builder.Entity<WorkShift>()
            .HasIndex(s => new { s.EmployeeUserId, s.WeekStartDate, s.StartTimeUtc, s.EndTimeUtc });
    }
}
