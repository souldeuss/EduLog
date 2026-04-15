namespace EduLog.Models
{
    public class TemplateSubject
    {
        public int TemplateId { get; set; }
        public ClassTemplate Template { get; set; }

        public int SubjectId { get; set; }
        public Subject Subject { get; set; }
    }
}
