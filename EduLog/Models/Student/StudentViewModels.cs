using EduLog.Models;
using EduLog.Services;

namespace EduLog.Models.StudentArea
{
    public class StudentSummary
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Initials { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public string SchoolName { get; set; } = string.Empty;

        public int Level { get; set; }
        public string LevelTitle { get; set; } = string.Empty;
        public int ExperiencePoints { get; set; }
        public int XpForCurrentLevel { get; set; }
        public int XpForNextLevel { get; set; }
        public int XpProgressPercent { get; set; }

        public int EduCoins { get; set; }
        public int AttendanceStreak { get; set; }
        public double GradeAverage { get; set; }
        public string GradeAverageStatus { get; set; } = string.Empty;
    }

    public class ScheduleSlotItem
    {
        public int LessonNumber { get; set; }
        public string SubjectName { get; set; } = string.Empty;
        public string TeacherName { get; set; } = string.Empty;
        public string? Room { get; set; }
        public DateTime Date { get; set; }
        public bool IsCurrent { get; set; }
        public bool HasAbsence { get; set; }
    }

    public class UpcomingHomeworkItem
    {
        public int MaterialId { get; set; }
        public int SubjectId { get; set; }
        public string SubjectName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime? Deadline { get; set; }
        public SubmissionStatus Status { get; set; }
    }

    public class GradeListItem
    {
        public int SubjectId { get; set; }
        public string SubjectName { get; set; } = string.Empty;
        public int Value { get; set; }
        public DateTime Date { get; set; }
    }

    public class StudentDashboardViewModel
    {
        public StudentSummary Summary { get; set; } = new();
        public List<ScheduleSlotItem> TodaySchedule { get; set; } = new();
        public ScheduleSlotItem? NextLesson { get; set; }
        public List<UpcomingHomeworkItem> UpcomingHomework { get; set; } = new();
        public List<GradeListItem> RecentGrades { get; set; } = new();
    }

    public class AchievementItem
    {
        public string Icon { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool Unlocked { get; set; }
        public string? UnlockedAt { get; set; }
    }

    public class StudentProfileViewModel
    {
        public StudentSummary Summary { get; set; } = new();
        public int TotalXp { get; set; }
        public int HomeworkSubmittedCount { get; set; }
        public int HomeworkAssignedCount { get; set; }
        public int HomeworkPercent { get; set; }
        public int AttendancePercent { get; set; }
        public List<AchievementItem> Achievements { get; set; } = new();

        // Grade history grouped by subject for the chart (last 30 days)
        public List<string> ChartLabels { get; set; } = new(); // dates as ISO strings
        public Dictionary<string, List<int?>> ChartSeries { get; set; } = new(); // subjectName → grades by date
    }

    public class WeekScheduleCell
    {
        public string SubjectName { get; set; } = string.Empty;
        public string TeacherName { get; set; } = string.Empty;
        public string? Room { get; set; }
        public bool IsCurrent { get; set; }
        public bool HasAbsenceThisWeek { get; set; }
    }

    public class StudentScheduleViewModel
    {
        public StudentSummary Summary { get; set; } = new();
        public DateTime WeekStart { get; set; }
        public DateTime WeekEnd { get; set; }
        public DateTime PreviousWeek { get; set; }
        public DateTime NextWeek { get; set; }

        // Map[(dayOfWeek 1..6, lessonNumber)] -> cell
        public Dictionary<(int Day, int Lesson), WeekScheduleCell> Cells { get; set; } = new();
        public int MaxLessonNumber { get; set; }
    }

    public class MaterialItem
    {
        public int Id { get; set; }
        public int SubjectId { get; set; }
        public string SubjectName { get; set; } = string.Empty;
        public MaterialType Type { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime Date { get; set; }
        public DateTime? Deadline { get; set; }
        public string TeacherName { get; set; } = string.Empty;
        public string? AttachmentPath { get; set; }
        public string? AttachmentFileName { get; set; }

        public SubmissionStatus SubmissionStatus { get; set; } = SubmissionStatus.NotSubmitted;
        public int? SubmissionId { get; set; }
        public string? SubmissionTextAnswer { get; set; }
        public string? SubmissionAttachmentPath { get; set; }
        public string? SubmissionAttachmentFileName { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public string? TeacherComment { get; set; }
        public int? ReviewScore { get; set; }
    }

    public class SubjectSidebarItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int PendingHomeworkCount { get; set; }
    }

    public class StudentMaterialsViewModel
    {
        public StudentSummary Summary { get; set; } = new();
        public List<SubjectSidebarItem> Subjects { get; set; } = new();
        public int? SelectedSubjectId { get; set; }
        public string? Filter { get; set; } // "all" / "homework" / "notes"
        public List<MaterialItem> Materials { get; set; } = new();
    }

    public class ClassmateRow
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Initials { get; set; } = string.Empty;
        public int Level { get; set; }
        public string LevelTitle { get; set; } = string.Empty;
        public int EduCoins { get; set; }
        public int AttendanceStreak { get; set; }
        public double GradeAverage { get; set; }
        public bool IsCurrent { get; set; }
    }

    public class StudentClassmatesViewModel
    {
        public StudentSummary Summary { get; set; } = new();
        public List<ClassmateRow> Rows { get; set; } = new();
    }

    public class StudentCoinsViewModel
    {
        public StudentSummary Summary { get; set; } = new();
        public List<CoinTransaction> Transactions { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }
}
