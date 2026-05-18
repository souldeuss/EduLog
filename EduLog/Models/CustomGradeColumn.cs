using System.ComponentModel.DataAnnotations;

namespace EduLog.Models
{
    public class CustomGradeColumn : ISchoolEntity
    {
        [Key]
        public int Id { get; set; }
        public int SchoolId { get; set; }

        public int ClassId { get; set; }
        public int SubjectId { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        public DateTime Date { get; set; }

        public int CreatedByTeacherId { get; set; }
    }
}
