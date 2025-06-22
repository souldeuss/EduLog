using ClosedXML.Excel;
using EduLog.Data;
using EduLog.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduLog.Controllers
{
    [Authorize]
    public class StatisticsController : Controller
    {
        private readonly EduLogContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public StatisticsController(EduLogContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private async Task<Teacher?> GetCurrentTeacherAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.TeacherId == null) return null;
            return _context.Teacher.FirstOrDefault(t => t.Id == user.TeacherId);
        }

        public async Task<IActionResult> Index(int? classId)
        {
            var teacher = await GetCurrentTeacherAsync();
            if (teacher == null) return RedirectToAction("Index", "Profile");

            var classes = _context.Class
                .Where(c => c.ClassSubjects.Any(cs => cs.Subject.TeacherId == teacher.Id))
                .OrderBy(c => c.Name)
                .ToList();

            var selectedClass = classId.HasValue
                ? classes.FirstOrDefault(c => c.Id == classId)
                : classes.FirstOrDefault();

            ViewBag.Classes = classes;
            ViewBag.SelectedClass = selectedClass;

            if (selectedClass == null)
                return View();

            var students = _context.Student
                .Where(s => s.ClassId == selectedClass.Id)
                .OrderBy(s => s.Surname)
                .ToList();

            var studentIds = students.Select(s => s.Id).ToList();

            var subjects = _context.ClassSubject
                .Where(cs => cs.ClassId == selectedClass.Id && cs.Subject.TeacherId == teacher.Id)
                .Select(cs => cs.Subject)
                .ToList();

            // Average grade per subject for this class
            var gradesBySubject = new Dictionary<string, double>();
            foreach (var subj in subjects)
            {
                var grades = _context.Grade
                    .Where(g => g.SubjectId == subj.Id && studentIds.Contains(g.StudentId) && g.Value > 0)
                    .ToList();
                gradesBySubject[subj.Name] = grades.Any() ? grades.Average(g => g.Value) : 0;
            }

            // Attendance percentage per subject
            var now = DateTime.Now;
            var yearStart = new DateTime(now.Month >= 9 ? now.Year : now.Year - 1, 9, 1);
            var attendanceBySubject = new Dictionary<string, double>();
            foreach (var subj in subjects)
            {
                var absenceCount = _context.Absence
                    .Count(a => a.SubjectId == subj.Id && studentIds.Contains(a.StudentId) && a.Date >= yearStart);

                // Rough estimate: school days * student count
                int schoolDays = (int)(now - yearStart).TotalDays / 7 * 5;
                if (schoolDays <= 0) schoolDays = 1;
                int totalSlots = schoolDays * students.Count;
                double pct = totalSlots > 0 ? Math.Round((1.0 - (double)absenceCount / totalSlots) * 100, 1) : 100;
                attendanceBySubject[subj.Name] = pct;
            }

            // Per-student average across all subjects
            var studentAverages = new Dictionary<string, double>();
            foreach (var st in students)
            {
                var allGrades = _context.Grade
                    .Where(g => g.StudentId == st.Id && subjects.Select(s => s.Id).Contains(g.SubjectId) && g.Value > 0)
                    .ToList();
                studentAverages[$"{st.Surname} {st.Name}"] = allGrades.Any() ? Math.Round(allGrades.Average(g => g.Value), 1) : 0;
            }

            ViewBag.Students = students;
            ViewBag.Subjects = subjects;
            ViewBag.GradesBySubject = gradesBySubject;
            ViewBag.AttendanceBySubject = attendanceBySubject;
            ViewBag.StudentAverages = studentAverages;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> ExportExcel(int classId)
        {
            var teacher = await GetCurrentTeacherAsync();
            if (teacher == null) return RedirectToAction("Index", "Profile");

            var cls = _context.Class.FirstOrDefault(c => c.Id == classId);
            if (cls == null) return NotFound();

            var students = _context.Student
                .Where(s => s.ClassId == classId)
                .OrderBy(s => s.Surname).ThenBy(s => s.Name)
                .ToList();

            var subjects = _context.ClassSubject
                .Where(cs => cs.ClassId == classId && cs.Subject.TeacherId == teacher.Id)
                .Select(cs => cs.Subject)
                .ToList();

            var studentIds = students.Select(s => s.Id).ToList();
            var subjectIds = subjects.Select(s => s.Id).ToList();

            var allGrades = _context.Grade
                .Where(g => studentIds.Contains(g.StudentId) && subjectIds.Contains(g.SubjectId) && g.Value > 0)
                .ToList();

            var now = DateTime.Now;
            var yearStart = new DateTime(now.Month >= 9 ? now.Year : now.Year - 1, 9, 1);
            var allAbsences = _context.Absence
                .Where(a => studentIds.Contains(a.StudentId) && subjectIds.Contains(a.SubjectId) && a.Date >= yearStart)
                .ToList();

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add($"Звіт {cls.Name}");

            // Header
            ws.Cell(1, 1).Value = $"Звіт: клас {cls.Name}";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;
            ws.Range(1, 1, 1, subjects.Count + 3).Merge();

            // Column headers
            int row = 3;
            ws.Cell(row, 1).Value = "№";
            ws.Cell(row, 2).Value = "Учень";
            for (int i = 0; i < subjects.Count; i++)
            {
                ws.Cell(row, 3 + i).Value = subjects[i].Name;
            }
            ws.Cell(row, 3 + subjects.Count).Value = "Середній бал";
            ws.Cell(row, 4 + subjects.Count).Value = "Пропуски";

            var headerRange = ws.Range(row, 1, row, 4 + subjects.Count);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
            headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            // Data rows
            row = 4;
            int num = 1;
            foreach (var st in students)
            {
                ws.Cell(row, 1).Value = num++;
                ws.Cell(row, 2).Value = $"{st.Surname} {st.Name} {st.Patronymic}";

                var studentGrades = allGrades.Where(g => g.StudentId == st.Id).ToList();
                double totalAvg = 0;
                int subCount = 0;

                for (int i = 0; i < subjects.Count; i++)
                {
                    var sg = studentGrades.Where(g => g.SubjectId == subjects[i].Id).ToList();
                    if (sg.Any())
                    {
                        var avg = Math.Round(sg.Average(g => g.Value), 1);
                        ws.Cell(row, 3 + i).Value = avg;
                        totalAvg += avg;
                        subCount++;
                    }
                    else
                    {
                        ws.Cell(row, 3 + i).Value = "—";
                    }
                }

                ws.Cell(row, 3 + subjects.Count).Value = subCount > 0 ? Math.Round(totalAvg / subCount, 1) : 0;
                ws.Cell(row, 4 + subjects.Count).Value = allAbsences.Count(a => a.StudentId == st.Id);

                row++;
            }

            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;
            var fileName = $"Zvit_{cls.Name}_{DateTime.Now:yyyy-MM-dd}.xlsx";
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
    }
}
