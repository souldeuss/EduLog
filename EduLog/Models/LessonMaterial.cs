using System.ComponentModel.DataAnnotations;

namespace EduLog.Models
{
    public enum MaterialType
    {
        Homework = 0,
        LessonNote = 1,
        Resource = 2
    }

    public class LessonMaterial : ISchoolEntity
    {
        [Key]
        public int Id { get; set; }
        public int SchoolId { get; set; }

        public int ClassSubjectClassId { get; set; }
        public int ClassSubjectSubjectId { get; set; }

        public int TeacherId { get; set; }

        public DateTime Date { get; set; }

        [Required]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public MaterialType Type { get; set; } = MaterialType.Homework;

        public DateTime? Deadline { get; set; }

        // Optional attached file (path relative to wwwroot, e.g. "/uploads/materials/abc.pdf")
        public string? AttachmentPath { get; set; }
        public string? AttachmentFileName { get; set; }

        // EduCoin reward for confirmed homework submission (0..50). Default 3 matches legacy behavior.
        public int EduCoinReward { get; set; } = 3;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ClassSubject? ClassSubject { get; set; }
        public Teacher? Teacher { get; set; }
        public ICollection<HomeworkSubmission> Submissions { get; set; } = new List<HomeworkSubmission>();
    }
}
