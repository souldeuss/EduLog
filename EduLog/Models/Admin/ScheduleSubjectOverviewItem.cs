namespace EduLog.Models.Admin
{
    public class ScheduleSubjectOverviewItem
    {
        public int SubjectId { get; set; }
        public string SubjectName { get; set; } = string.Empty;
        public int RequiredLessons { get; set; }
        public int AssignedLessons { get; set; }
        public string? ColorHex { get; set; }
    }
}
