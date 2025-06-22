using System.ComponentModel.DataAnnotations;

namespace EduLog.Models
{
    public class Subject
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string Name { get; set; }
        public int TeacherId { get; set; }
        public int ClassId { get; set; }
        public Teacher Teacher { get; set; }

        public ICollection<ClassSubject> ClassSubjects { get; set; }
    }
}