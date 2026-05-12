using System.ComponentModel.DataAnnotations;

namespace EduLog.Models
{
    public enum TeacherAbsenceType
    {
        SickLeave = 1,
        Vacation = 2,
        Other = 3
    }

    public class TeacherAbsence : ISchoolEntity
    {
        [Key]
        public int Id { get; set; }
        public int SchoolId { get; set; }

        public int TeacherId { get; set; }
        public Teacher Teacher { get; set; } = null!;

        [Required]
        public TeacherAbsenceType Type { get; set; } = TeacherAbsenceType.SickLeave;

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [StringLength(500)]
        public string? Note { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<ScheduleSlotOverride> Overrides { get; set; } = new List<ScheduleSlotOverride>();
    }
}
