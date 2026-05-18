using System.ComponentModel.DataAnnotations;

namespace EduLog.Models
{
    // Окрема відповідь учня на питання в межах сесії.
    // Ланцюжок таких записів формує лог для перерахунку BKT та аналітики.
    public class AdaptiveAnswer : ISchoolEntity
    {
        [Key]
        public int Id { get; set; }
        public int SchoolId { get; set; }

        public int SessionId { get; set; }
        public int QuestionId { get; set; }

        public bool IsCorrect { get; set; }
        public DateTime AnsweredAt { get; set; } = DateTime.UtcNow;

        public AdaptiveSession? Session { get; set; }
        public QuestionItem? Question { get; set; }
    }
}
