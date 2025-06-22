using System.ComponentModel.DataAnnotations;

namespace EduLog.Models
{
    public class Grade
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public int StudentId { get; set; }
        public int SubjectId { get; set; }
        public DateTime Date { get; set; }
        public int Value { get; set; } // 1-12
    }
}
