using System.ComponentModel.DataAnnotations;

namespace EduLog.Models
{
    public class GradeScale : ISchoolEntity
    {
        [Key]
        public int Id { get; set; }
        public int SchoolId { get; set; }

        public int ClassId { get; set; }
        public int SubjectId { get; set; }

        public ScaleType ScaleType { get; set; }

        public int? MinValue { get; set; }
        public int? MaxValue { get; set; }

        public string? VerbalOptions { get; set; }
    }
}
