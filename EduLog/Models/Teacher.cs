using System.ComponentModel.DataAnnotations;

namespace EduLog.Models
{
    public class Teacher
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string Name { get; set; }
        public string Surname { get; set; }
        public string Patronymic { get; set; }
        public string PhotoPath { get; set; }
    }
}
