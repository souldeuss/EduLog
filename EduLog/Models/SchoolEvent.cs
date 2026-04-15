using System.ComponentModel.DataAnnotations;

namespace EduLog.Models
{
    public class SchoolEvent : ISchoolEntity
    {
        [Key]
        public int Id { get; set; }

        public int SchoolId { get; set; }

        [Required]
        public string Title { get; set; } = string.Empty;

        public DateTime Date { get; set; }

        public string? Color { get; set; }
    }
}
