using System.ComponentModel.DataAnnotations;

namespace EduLog.Models
{
    public class Class : ISchoolEntity
    {
        [Key]
        public int Id { get; set; }
        public int SchoolId { get; set; }
        [Required]
        public string Name { get; set; } // Наприклад: "10-А", "9-Б"
        public int? TeacherId { get; set; }
        public int? RoomId { get; set; }
        public Room? Room { get; set; }

        public ICollection<ClassSubject> ClassSubjects { get; set; }
    }
}
