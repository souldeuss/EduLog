using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EduLog.Models
{
    public class Student : ISchoolEntity
    {
        [Key]
        public int Id { get; set; }
        public int SchoolId { get; set; }
        [Required]
        public string Name { get; set; }
        public string Surname { get; set; }
        public string Patronymic { get; set; }
        public int ClassId { get; set; }
        public Class Class { get; set; }
        public ICollection<Grade> Grades { get; set; }
        public ICollection<Absence> Absences { get; set; }

        // Identity link
        public string? ApplicationUserId { get; set; }
        [ForeignKey(nameof(ApplicationUserId))]
        public ApplicationUser? ApplicationUser { get; set; }

        // Avatar (relative path under wwwroot, e.g. "/uploads/avatars/abc.png")
        public string? AvatarPath { get; set; }

        // Gamification
        public int EduCoins { get; set; } = 0;
        public int AttendanceStreak { get; set; } = 0;
        public DateTime? LastAttendanceDate { get; set; }
        public int ExperiencePoints { get; set; } = 0;
        public int Level { get; set; } = 1;

        public ICollection<HomeworkSubmission> HomeworkSubmissions { get; set; } = new List<HomeworkSubmission>();
        public ICollection<CoinTransaction> CoinTransactions { get; set; } = new List<CoinTransaction>();
    }
}
