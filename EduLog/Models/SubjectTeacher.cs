namespace EduLog.Models
{
    public class SubjectTeacher : ISchoolEntity
    {
        public int SchoolId { get; set; }

        public int SubjectId { get; set; }
        public Subject Subject { get; set; } = null!;

        public int TeacherId { get; set; }
        public Teacher Teacher { get; set; } = null!;
    }
}
