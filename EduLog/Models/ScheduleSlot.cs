using System.ComponentModel.DataAnnotations;

namespace EduLog.Models
{
    public class ScheduleSlot : ISchoolEntity
    {
        [Key]
        public int Id { get; set; }
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }
        public AcademicYear AcademicYear { get; set; } = null!;

        [Required]
        [Range(1, 5, ErrorMessage = "День тижня від 1 (Пн) до 5 (Пт)")]
        [Display(Name = "День тижня")]
        public int DayOfWeek { get; set; }

        [Required]
        [Range(1, 8, ErrorMessage = "Номер уроку від 1 до 8")]
        [Display(Name = "Урок №")]
        public int LessonNumber { get; set; }

        public int ClassId { get; set; }
        public Class Class { get; set; } = null!;

        public int SubjectId { get; set; }
        public Subject Subject { get; set; } = null!;

        public int TeacherId { get; set; }
        public Teacher Teacher { get; set; } = null!;

        [Display(Name = "Кабінет")]
        [StringLength(20)]
        public string? Room { get; set; }
    }
}
