using System.ComponentModel.DataAnnotations;

namespace EduLog.Models
{
    public class Subject : ISchoolEntity
    {
        [Key]
        public int Id { get; set; }
        public int SchoolId { get; set; }
        [Required]
        public string Name { get; set; }
        public int TeacherId { get; set; }
        public int? ClassId { get; set; }
        public Teacher Teacher { get; set; }

        public ICollection<ClassSubject> ClassSubjects { get; set; }
    }
}