namespace EduLog.Services
{
    public enum SchedulerMode
    {
        Balanced = 0,
        Dense = 1,
        Append = 2
    }

    public sealed class SchedulerApiOptions
    {
        public const string SectionName = "SchedulerApi";

        public string BaseUrl { get; set; } = "http://localhost:5001";
        public int TimeoutSeconds { get; set; } = 120;
        public int RetryCount { get; set; } = 3;
    }

    public sealed class SchedulerConfigOptions
    {
        public int MaxLessonsPerDay { get; set; } = 7;
        public bool AllowDuplicates { get; set; }
        public int MaxDuplicatesPerDay { get; set; } = 2;
        public bool AllowGaps { get; set; }
        public int PlanningPeriodWeeks { get; set; } = 1;
    }

    public sealed class ScheduleSlotDto
    {
        public int SchoolId { get; set; }
        public int AcademicYearId { get; set; }
        public int ClassId { get; set; }
        public int SubjectId { get; set; }
        public int TeacherId { get; set; }
        public int DayOfWeek { get; set; }
        public int LessonNumber { get; set; }
        public string? Room { get; set; }
    }

    public sealed class ScheduleStatisticsDto
    {
        public int TotalSlots { get; set; }
        public int ScheduledSlots { get; set; }
        public double OccupancyRate { get; set; }
        public double OverallScore { get; set; }
        public int HardViolations { get; set; }
        public int SoftViolations { get; set; }
    }

    public sealed class ScheduleGenerationResult
    {
        public bool Success { get; set; }
        public List<ScheduleSlotDto> Slots { get; set; } = new();
        public List<string> Conflicts { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public ScheduleStatisticsDto Statistics { get; set; } = new();
    }

    public sealed class SchedulerModeInfo
    {
        public SchedulerMode Mode { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    internal sealed class SchedulerValidationSummary
    {
        public bool IsValid { get; set; }
        public List<string> Messages { get; set; } = new();
    }
}