using System.ComponentModel.DataAnnotations;

namespace EduLog.Models
{
    public class CoinTransaction : ISchoolEntity
    {
        [Key]
        public int Id { get; set; }
        public int SchoolId { get; set; }

        public int StudentId { get; set; }
        public int Amount { get; set; }

        [Required]
        public string Reason { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Student? Student { get; set; }
    }
}
