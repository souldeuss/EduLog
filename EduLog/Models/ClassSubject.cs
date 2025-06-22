namespace EduLog.Models
{
    public class ClassSubject : ISchoolEntity
    {
        public int SchoolId { get; set; }
        public int ClassId { get; set; }
        public Class Class { get; set; }

        public int SubjectId { get; set; }
        public Subject Subject { get; set; }
    }
}