namespace EduLog.Models
{
    public class TeacherScheduleSlotView
    {
        public int DayOfWeek { get; set; }
        public int LessonNumber { get; set; }
        public string SubjectName { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public string? Room { get; set; }
    }
}
