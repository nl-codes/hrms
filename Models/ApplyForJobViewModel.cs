namespace HRMS.Models;

public class ApplyForJobViewModel
{
    public int JobPostingId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int RequiredExperienceYears { get; set; }
    public string RequiredSkillsCsv { get; set; } = string.Empty;
    public int OpenPositions { get; set; }
    public int ExperienceYears { get; set; }
    public string SkillsCsv { get; set; } = string.Empty;

    public ApplyForJobViewModel()
    {
    }

    public ApplyForJobViewModel(JobPosting jobPosting)
    {
        JobPostingId = jobPosting.Id;
        Title = jobPosting.Title;
        Description = jobPosting.Description;
        RequiredExperienceYears = jobPosting.RequiredExperienceYears;
        RequiredSkillsCsv = jobPosting.RequiredSkillsCsv;
        OpenPositions = jobPosting.OpenPositions;
    }
}

public class HiringCompletedViewModel
{
    public int JobPostingId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int OpenPositions { get; set; }
    public int HiredCount { get; set; }
}
