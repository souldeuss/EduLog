using System.ComponentModel.DataAnnotations;

namespace EduLog.Models
{
    public class Room : ISchoolEntity
    {
        [Key]
        public int Id { get; set; }
        public int SchoolId { get; set; }
        [Required]
        [StringLength(50)]
        public string Number { get; set; }
        public int? Capacity { get; set; }

        public ICollection<Class> Classes { get; set; }
        public ICollection<Subject> ProfileSubjects { get; set; } = new List<Subject>();
    }
}
