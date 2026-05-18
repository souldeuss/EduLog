using System.ComponentModel.DataAnnotations;

namespace EduLog.Models
{
    public class Grade : ISchoolEntity
    {
        [Key]
        public int Id { get; set; }
        public int SchoolId { get; set; }
        [Required]
        public int StudentId { get; set; }
        public int SubjectId { get; set; }
        public DateTime Date { get; set; }
        public int? Value { get; set; }
        public string? VerbalValue { get; set; }

        public int? CustomGradeColumnId { get; set; }
    }
}
