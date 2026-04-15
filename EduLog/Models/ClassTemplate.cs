using System.ComponentModel.DataAnnotations;

namespace EduLog.Models
{
    public class ClassTemplate : ISchoolEntity
    {
        [Key]
        public int Id { get; set; }
        public int SchoolId { get; set; }
        [Required]
        public string Name { get; set; }

        public ICollection<TemplateSubject> TemplateSubjects { get; set; }
    }
}
