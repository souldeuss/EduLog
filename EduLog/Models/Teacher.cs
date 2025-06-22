using System.ComponentModel.DataAnnotations;

namespace EduLog.Models
{
    public class Teacher : ISchoolEntity
    {
        [Key]
        public int Id { get; set; }
        public int SchoolId { get; set; }
        [Required]
        public string Name { get; set; }
        public string Surname { get; set; }
        public string Patronymic { get; set; }
        public string PhotoPath { get; set; }
    }
}
