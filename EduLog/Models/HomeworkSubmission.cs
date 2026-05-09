using System.ComponentModel.DataAnnotations;

namespace EduLog.Models
{
    public enum SubmissionStatus
    {
        NotSubmitted = 0,
        Submitted = 1,
        Reviewed = 2
    }

    public class HomeworkSubmission : ISchoolEntity
    {
        [Key]
        public int Id { get; set; }
        public int SchoolId { get; set; }

        public int LessonMaterialId { get; set; }
        public int StudentId { get; set; }

        public string? TextAnswer { get; set; }

        // Optional file attached by the student
        public string? AttachmentPath { get; set; }
        public string? AttachmentFileName { get; set; }

        public SubmissionStatus Status { get; set; } = SubmissionStatus.NotSubmitted;
        public DateTime? SubmittedAt { get; set; }
        public string? TeacherComment { get; set; }
        public int? ReviewScore { get; set; }

        public LessonMaterial? LessonMaterial { get; set; }
        public Student? Student { get; set; }
    }
}
