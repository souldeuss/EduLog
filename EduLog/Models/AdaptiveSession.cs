using System.ComponentModel.DataAnnotations;

namespace EduLog.Models
{
    // Сесія виконання адаптивного домашнього завдання.
    // Прив'язується до LessonMaterial (типу Homework) і конкретного учня.
    public class AdaptiveSession : ISchoolEntity
    {
        [Key]
        public int Id { get; set; }
        public int SchoolId { get; set; }

        public int StudentId { get; set; }
        public int LessonMaterialId { get; set; }

        // Питання, що зараз показане учневі; null до старту або після завершення.
        public int? CurrentQuestionId { get; set; }

        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }

        public Student? Student { get; set; }
        public LessonMaterial? LessonMaterial { get; set; }
        public QuestionItem? CurrentQuestion { get; set; }

        public ICollection<AdaptiveAnswer> Answers { get; set; } = new List<AdaptiveAnswer>();
    }
}
