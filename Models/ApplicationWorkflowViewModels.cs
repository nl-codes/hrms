namespace HRMS.Models;

public class ApplicantApplicationItemViewModel
{
    public int Id { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public ApplicationStatus Status { get; set; }
    public DateTime AppliedAtUtc { get; set; }
    public DateTime? ScreenedAtUtc { get; set; }
    public DateTime? HiredAtUtc { get; set; }
}

public class ApplicationReviewItemViewModel
{
    public int Id { get; set; }
    public string ApplicantUserId { get; set; } = string.Empty;
    public string ApplicantEmail { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public int ApplicantExperienceYears { get; set; }
    public string ApplicantSkillsCsv { get; set; } = string.Empty;
    public ApplicationStatus Status { get; set; }
    public DateTime AppliedAtUtc { get; set; }
    public DateTime? ScreenedAtUtc { get; set; }
    public DateTime? HiredAtUtc { get; set; }
}

public class ApplicationsReviewIndexViewModel
{
    public string ActiveTab { get; set; } = "all";
    public int AllCount { get; set; }
    public int ScreenedCount { get; set; }
    public int RejectedCount { get; set; }
    public int HiredCount { get; set; }
    public IReadOnlyList<ApplicationReviewItemViewModel> Applications { get; set; } = [];
}

public class ApplicationDetailsViewModel
{
    public int Id { get; set; }
    public string ApplicantUserId { get; set; } = string.Empty;
    public string ApplicantEmail { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public string JobDescription { get; set; } = string.Empty;
    public int ApplicantExperienceYears { get; set; }
    public string ApplicantSkillsCsv { get; set; } = string.Empty;
    public ApplicationStatus Status { get; set; }
    public DateTime AppliedAtUtc { get; set; }
    public DateTime? ScreenedAtUtc { get; set; }
    public DateTime? HiredAtUtc { get; set; }
    public bool CanHire { get; set; }
}
