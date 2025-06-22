using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;

namespace EduLog.Models
{
    public class ApplicationUser : IdentityUser
    {
        public int? TeacherId { get; set; }
        public int? SchoolId { get; set; }

        [ForeignKey(nameof(TeacherId))]
        public Teacher? Teacher { get; set; }

        [ForeignKey(nameof(SchoolId))]
        public School? School { get; set; }
    }
}
