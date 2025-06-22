using System.ComponentModel.DataAnnotations;

namespace EduLog.Models
{
    public class Absence : ISchoolEntity
    {
        [Key]
        public int Id { get; set; }
        public int SchoolId { get; set; }
        [Required]
        public int StudentId { get; set; }
        public int SubjectId { get; set; }
        public DateTime Date { get; set; }
        public string Reason { get; set; }
    }
}