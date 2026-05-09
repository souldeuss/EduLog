using System.ComponentModel.DataAnnotations;

namespace EduLog.Models
{
    public class Invitation : ISchoolEntity
    {
        [Key]
        public int Id { get; set; }

        public int SchoolId { get; set; }
        public School School { get; set; } = null!;

        [Required]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Token { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsUsed { get; set; }

        // For multi-role invitations: "Teacher" (default) or "Student"
        [Required]
        public string Role { get; set; } = "Teacher";

        // For Student invitations — links to existing Student record
        public int? StudentId { get; set; }
        public Student? Student { get; set; }
    }
}
