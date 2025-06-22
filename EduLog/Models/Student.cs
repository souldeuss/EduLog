using System.ComponentModel.DataAnnotations;

namespace EduLog.Models
{
    public class Student : ISchoolEntity
    {
        [Key]
        public int Id { get; set; }
        public int SchoolId { get; set; }
        [Required]
        public string Name { get; set; }
        public string Surname { get; set; }
        public string Patronymic { get; set; }
        public int ClassId { get; set; } 
        public Class Class { get; set; }
        public ICollection<Grade> Grades { get; set; }
        public ICollection<Absence> Absences { get; set; }
    }
}
