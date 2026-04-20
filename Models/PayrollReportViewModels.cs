namespace HRMS.Models;

public class PayrollReportViewModel
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime CurrentMonthStart { get; set; }
    public DateTime CurrentMonthEnd { get; set; }
    public IReadOnlyList<PayrollReportItemViewModel> Rows { get; set; } = [];
}

public class PayrollReportItemViewModel
{
    public string EmployeeId { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public decimal TotalHoursWorked { get; set; }
    public int TrainingsCompleted { get; set; }
    public decimal HourlyRate { get; set; }
    public decimal GrossPay { get; set; }
    public decimal TrainingBonus { get; set; }
    public string WeeklyRequirementWarning { get; set; } = string.Empty;
}
