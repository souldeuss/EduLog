using System.ComponentModel.DataAnnotations;

namespace EduLog.Models
{
    public class Absence
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public int StudentId { get; set; }
        public int SubjectId { get; set; }
        public DateTime Date { get; set; }
        public string Reason { get; set; }
    }
}