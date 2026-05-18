using System.ComponentModel.DataAnnotations;

namespace EduLog.Models
{
    // BKT-стан знань учня по конкретній темі предмета.
    // Один запис на трійку (StudentId, SubjectId, TopicTag).
    public class StudentKnowledgeState : ISchoolEntity
    {
        [Key]
        public int Id { get; set; }
        public int SchoolId { get; set; }

        public int StudentId { get; set; }
        public int SubjectId { get; set; }

        [Required]
        public string TopicTag { get; set; } = string.Empty;

        // P(L) — поточна ймовірність того, що тема засвоєна. Початкове значення зазвичай 0.1..0.3.
        public double ProbabilityLearned { get; set; } = 0.2;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public Student? Student { get; set; }
        public Subject? Subject { get; set; }
    }
}
