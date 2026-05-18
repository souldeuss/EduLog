using System.ComponentModel.DataAnnotations;

namespace EduLog.Models
{
    // Питання банку для адаптивного домашнього завдання.
    // Параметри IRT 3PL: a (дискримінація), b (складність), c (вгадування).
    public class QuestionItem : ISchoolEntity
    {
        [Key]
        public int Id { get; set; }
        public int SchoolId { get; set; }

        [Required]
        public string Text { get; set; } = string.Empty;

        public int SubjectId { get; set; }

        // Тема в межах предмета (наприклад, "Дроби", "Квадратні рівняння").
        // Тримаємо як рядок для гнучкості — таксономія тем може еволюціонувати.
        [Required]
        public string TopicTag { get; set; } = string.Empty;

        // IRT 3PL parameters
        public double IrtA { get; set; } = 1.0;   // дискримінація (зазвичай 0.5..2.5)
        public double IrtB { get; set; } = 0.0;   // складність   [-3..+3]
        public double IrtC { get; set; } = 0.2;   // вгадування   [0..0.35]

        // Текст підказки, що показується слабкому учневі (pL < 0.4).
        public string? HintText { get; set; }

        public Subject? Subject { get; set; }
    }
}
