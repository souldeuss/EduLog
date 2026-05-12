using System.ComponentModel.DataAnnotations;

namespace EduLog.Models
{
    public class ScheduleSlotOverride : ISchoolEntity
    {
        [Key]
        public int Id { get; set; }
        public int SchoolId { get; set; }

        public int ScheduleSlotId { get; set; }
        public ScheduleSlot ScheduleSlot { get; set; } = null!;

        [Required]
        public DateTime Date { get; set; }

        public int? SubstituteTeacherId { get; set; }
        public Teacher? SubstituteTeacher { get; set; }

        public int? AbsenceId { get; set; }
        public TeacherAbsence? Absence { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
