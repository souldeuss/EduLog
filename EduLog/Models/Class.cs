using System.ComponentModel.DataAnnotations;

namespace EduLog.Models
{
    public class Class
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string Name { get; set; } // Наприклад: "10-А", "9-Б"
        public int? TeacherId { get; set; }

        public ICollection<ClassSubject> ClassSubjects { get; set; }
    }
}
