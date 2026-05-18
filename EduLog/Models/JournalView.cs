using System.ComponentModel.DataAnnotations;
using System.Text.Json;

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
        public List<CustomGradeColumn> CustomColumns { get; set; } = new();
        public GradeScale? Scale { get; set; }

        public List<string> VerbalOptions
        {
            get
            {
                if (Scale?.VerbalOptions == null) return new List<string>();
                try
                {
                    return JsonSerializer.Deserialize<List<string>>(Scale.VerbalOptions) ?? new List<string>();
                }
                catch
                {
                    return new List<string>();
                }
            }
        }

        public string? GetCellValue(int studentId, DateTime date, int subjectId)
        {
            var absence = Absences?
                .Any(a => a.StudentId == studentId && a.Date.Date == date.Date && a.SubjectId == subjectId) ?? false;
            if (absence) return "Н";

            var grade = Grades?
                .FirstOrDefault(g => g.StudentId == studentId
                    && g.Date.Date == date.Date
                    && g.SubjectId == subjectId
                    && g.CustomGradeColumnId == null);
            if (grade == null) return null;
            return !string.IsNullOrEmpty(grade.VerbalValue) ? grade.VerbalValue : grade.Value?.ToString();
        }

        public string? GetCustomCellValue(int studentId, int customColumnId)
        {
            var grade = Grades?
                .FirstOrDefault(g => g.StudentId == studentId && g.CustomGradeColumnId == customColumnId);
            if (grade == null) return null;
            return !string.IsNullOrEmpty(grade.VerbalValue) ? grade.VerbalValue : grade.Value?.ToString();
        }

        public int? GetGrade(int studentId, DateTime date, int subjectId)
        {
            var grade = Grades?
                .FirstOrDefault(g => g.StudentId == studentId
                    && g.Date.Date == date.Date
                    && g.SubjectId == subjectId
                    && g.CustomGradeColumnId == null);
            return grade?.Value;
        }

        public bool GetAbsence(int studentId, DateTime date, int subjectId)
        {
            return Absences?
                .Any(a => a.StudentId == studentId && a.Date.Date == date.Date && a.SubjectId == subjectId) ?? false;
        }

        // Average / mode for the "Сер." column, scale-aware.
        public string GetAverageDisplay(int studentId)
        {
            var studentGrades = Grades?
                .Where(g => g.StudentId == studentId)
                .ToList() ?? new List<Grade>();
            if (studentGrades.Count == 0) return "—";

            if (Scale?.ScaleType == ScaleType.Verbal)
            {
                var verbal = studentGrades
                    .Where(g => !string.IsNullOrEmpty(g.VerbalValue))
                    .GroupBy(g => g.VerbalValue!)
                    .OrderByDescending(grp => grp.Count())
                    .ThenBy(grp => grp.Key)
                    .FirstOrDefault();
                return verbal?.Key ?? "—";
            }

            var numeric = studentGrades.Where(g => g.Value.HasValue && g.Value > 0).ToList();
            if (numeric.Count == 0) return "—";
            return numeric.Average(g => g.Value ?? 0).ToString("0.0");
        }
    }

}
