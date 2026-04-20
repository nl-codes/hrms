namespace HRMS.Models;

public class ReportsDashboardViewModel
{
    public int ActiveEmployeeCount { get; set; }
    public int OpenJobPostingCount { get; set; }
    public int CompletedTrainingSessionsThisMonth { get; set; }
    public int TotalDecidedApplicants { get; set; }
    public int HiredApplicantCount { get; set; }
    public int RejectedApplicantCount { get; set; }
    public decimal HiredPercentage { get; set; }
    public decimal RejectedPercentage { get; set; }
}

public class WorkHoursReportViewModel
{
    public string PeriodType { get; set; } = "week";
    public DateTime ReferenceDate { get; set; }
    public DateTime RangeStartDate { get; set; }
    public DateTime RangeEndDate { get; set; }
    public decimal WeeklyTargetHours { get; set; } = 36m;
    public IReadOnlyList<WorkHoursReportRowViewModel> Rows { get; set; } = [];
}

public class WorkHoursReportRowViewModel
{
    public string EmployeeId { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public DateTime RangeStartDate { get; set; }
    public DateTime RangeEndDate { get; set; }
    public decimal TotalHoursWorked { get; set; }
    public bool IsBelowWeeklyTarget { get; set; }
}

public class TrainingCompletionReportViewModel
{
    public bool ShowIncompleteOnly { get; set; }
    public IReadOnlyList<TrainingCompletionEmployeeRowViewModel> Rows { get; set; } = [];
    public IReadOnlyList<TrainingIncompleteRecordViewModel> IncompleteRecords { get; set; } = [];
}

public class TrainingCompletionEmployeeRowViewModel
{
    public string EmployeeId { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public int TotalSessionsEnrolled { get; set; }
    public int SessionsCompleted { get; set; }
    public int IncompleteOrAbsentCount { get; set; }
    public decimal CompletionRate { get; set; }
}

public class TrainingIncompleteRecordViewModel
{
    public string EmployeeName { get; set; } = string.Empty;
    public string SessionTitle { get; set; } = string.Empty;
    public DateTime SessionDateUtc { get; set; }
    public EnrollmentStatus Status { get; set; }
}

public class HiringFunnelReportViewModel
{
    public int TotalApplications { get; set; }
    public int ScreenedCount { get; set; }
    public int RejectedCount { get; set; }
    public int AutomatedRejectedCount { get; set; }
    public int HiredCount { get; set; }
    public decimal AcceptanceRate { get; set; }
}
