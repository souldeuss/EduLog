using System.ComponentModel.DataAnnotations;

namespace EduLog.Models
{
    internal class JournalView
    {
        public int Month { get; set; }
        public int Year { get; set; }
        public Class Class { get; set; }
        public List<DateTime> Days { get; set; }
        public List<Student> Students { get; set; }
        public Subject Subject { get; set; }
        public List<Grade> Grades { get; set; }
        public List<Absence> Absences { get; set; }
        public List <string> CustomColumns { get; set; }

        public int? GetGrade(int studentId, DateTime date, int subjectId)
        {
            var grade = Grades?
                .FirstOrDefault(g => g.StudentId == studentId && g.Date.Date == date.Date && g.SubjectId == subjectId);
            return grade?.Value;
        }
        public bool GetAbsence(int studentId, DateTime date, int subjectId)
        {
            return Absences?
                .Any(a => a.StudentId == studentId && a.Date.Date == date.Date && a.SubjectId == subjectId) ?? false;
        }
    }
    
}
