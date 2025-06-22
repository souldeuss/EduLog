namespace EduLog.Models.Admin
{
    public class BulkSubjectItem
    {
        public string Name { get; set; } = "";
        public int TeacherId { get; set; }
    }

    public class ImportSubjectRow
    {
        public string Name { get; set; } = "";
        public string TeacherName { get; set; } = "";
        public string ClassName { get; set; } = "";
    }

    public class AddClassRequest
    {
        public int SubjectId { get; set; }
        public int ClassId { get; set; }
    }

    public class RemoveClassRequest
    {
        public int SubjectId { get; set; }
        public int ClassId { get; set; }
    }

    public class ChangeTeacherRequest
    {
        public int SubjectId { get; set; }
        public int TeacherId { get; set; }
    }
}
