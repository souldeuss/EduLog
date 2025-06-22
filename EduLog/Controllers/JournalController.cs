using EduLog.Data;
using EduLog.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduLog.Controllers
{
    [Authorize]
    public class JournalController : Controller
    {
        private readonly EduLogContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public JournalController(EduLogContext context, UserManager<ApplicationUser> userManager)
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

        public IActionResult Index(int classId, int subjectId, string viewMode = "week",
            int? month = null, int? year = null)
        {
            var now = DateTime.Now;
            int selectedMonth = month ?? now.Month;
            int selectedYear = year ?? now.Year;

            List<DateTime> days;
            if (viewMode == "day")
            {
                days = new List<DateTime> { now.Date };
            }
            else if (viewMode == "week")
            {
                int diff = (7 + (now.DayOfWeek - DayOfWeek.Monday)) % 7;
                DateTime monday = now.AddDays(-1 * diff).Date;
                days = Enumerable.Range(0, 5).Select(i => monday.AddDays(i)).ToList();
            }
            else // "month"
            {
                int daysInMonth = DateTime.DaysInMonth(selectedYear, selectedMonth);
                days = Enumerable.Range(1, daysInMonth)
                    .Select(d => new DateTime(selectedYear, selectedMonth, d))
                    .ToList();
            }

            var students = _context.Student
                .Where(s => s.ClassId == classId)
                .OrderBy(s => s.Surname).ThenBy(s => s.Name)
                .ToList();

            var subject = _context.Subject
                .FirstOrDefault(s => s.Id == subjectId);

            var grades = _context.Grade
                .Where(g => g.SubjectId == subjectId
                    && students.Select(s => s.Id).Contains(g.StudentId)
                    && days.Select(d => d.Month).Contains(g.Date.Month)
                    && days.Select(d => d.Year).Contains(g.Date.Year))
                .ToList();

            var absences = _context.Absence
                .Where(a => a.SubjectId == subjectId
                    && students.Select(s => s.Id).Contains(a.StudentId)
                    && days.Select(d => d.Month).Contains(a.Date.Month)
                    && days.Select(d => d.Year).Contains(a.Date.Year))
                .ToList();

            var Cls = _context.Class
                .FirstOrDefault(c => c.Id == classId);

            // Data for class/subject switcher
            var teacher = GetCurrentTeacherAsync().Result;
            var allClasses = teacher != null
                ? _context.Class.Where(c => c.ClassSubjects.Any(cs => cs.Subject.TeacherId == teacher.Id)).ToList()
                : new List<Class>();

            var subjectsForClass = _context.ClassSubject
                .Where(cs => cs.ClassId == classId)
                .Select(cs => cs.Subject)
                .ToList();

            ViewBag.ViewMode = viewMode;
            ViewBag.SelectedMonth = selectedMonth;
            ViewBag.SelectedYear = selectedYear;
            ViewBag.AllClasses = allClasses;
            ViewBag.SubjectsForClass = subjectsForClass;

            var Model = new JournalView
            {
                Month = selectedMonth,
                Year = selectedYear,
                Days = days,
                Class = Cls,
                Students = students,
                Subject = subject,
                Grades = grades,
                Absences = absences
            };
            return View(Model);
        }

        [HttpGet]
        public IActionResult GetSubjectsForClass(int classId)
        {
            var subjects = _context.ClassSubject
                .Where(cs => cs.ClassId == classId)
                .Select(cs => cs.Subject)
                .Select(s => new { s.Id, s.Name })
                .ToList();
            return Json(subjects);
        }

        public async Task<IActionResult> spreader()
        {
            var teacher = await GetCurrentTeacherAsync();
            if (teacher == null)
                return RedirectToAction("Index", "Profile");

            var classes = _context.Class
             .Where(c => c.ClassSubjects.Any(cs => cs.Subject.TeacherId == teacher.Id))
             .ToList();

            if (classes.Count == 0)
                return RedirectToAction("Index", "Profile");
            else if (classes.Count == 1)
                return RedirectToAction("SelectSubject", new { classId = classes[0].Id });
            else
                return RedirectToAction("SelectClasses");
        }

        public async Task<IActionResult> SelectSubject(int classId)
        {
            var teacher = await GetCurrentTeacherAsync();
            var subjects = _context.ClassSubject
                .Where(cs => cs.ClassId == classId)
                .Select(cs => cs.Subject)
                .ToList();
            var cls = _context.Class
                .FirstOrDefault(c => c.Id == classId);
            var classes = _context.Class
                .Where(c => c.TeacherId == teacher.Id)
                .ToList();

            if (subjects.Count == 1)
            {
                return RedirectToAction("Index", new { classId, subjectId = subjects[0].Id });
            }
            ViewData["Classes"] = classes;
            ViewData["Class"] = cls;
            return View(subjects);
        }

        public async Task<IActionResult> SelectClasses()
        {
            var teacher = await GetCurrentTeacherAsync();
            if (teacher == null)
                return RedirectToAction("Index", "Profile");

            var classes = _context.Class
                .Where(c => c.ClassSubjects.Any(cs => cs.Subject.TeacherId == teacher.Id))
                .ToList();

            return View(classes);
        }

        [HttpPost]
        public IActionResult SaveCell([FromBody] SaveCellRequest request)
        {
            if (request == null)
                return BadRequest();

            if (!DateTime.TryParse(request.Date, out var date))
                return BadRequest();

            var raw = request.Value?.ToString()?.Trim() ?? "";
            var isAbsence = raw.Equals("н", StringComparison.OrdinalIgnoreCase);

            var grade = _context.Grade.FirstOrDefault(g =>
                g.StudentId == request.StudentId &&
                g.SubjectId == request.SubjectId &&
                g.Date.Date == date.Date);

            var absence = _context.Absence.FirstOrDefault(a =>
                a.StudentId == request.StudentId &&
                a.SubjectId == request.SubjectId &&
                a.Date.Date == date.Date);

            // Empty cell — remove both grade and absence
            if (string.IsNullOrWhiteSpace(raw))
            {
                if (grade != null) _context.Grade.Remove(grade);
                if (absence != null) _context.Absence.Remove(absence);
                _context.SaveChanges();
                return Ok();
            }

            // "Н" — mark absence, remove grade if any
            if (isAbsence)
            {
                if (grade != null) _context.Grade.Remove(grade);
                if (absence == null)
                {
                    _context.Absence.Add(new Absence
                    {
                        StudentId = request.StudentId,
                        SubjectId = request.SubjectId,
                        Date = date,
                        Reason = ""
                    });
                }
                _context.SaveChanges();
                return Ok();
            }

            // Validate grade 1-12
            if (!int.TryParse(raw, out var val) || val < 1 || val > 12)
            {
                return BadRequest(new { error = "Допустимі значення: 1-12 або Н" });
            }

            // Save grade, remove absence if any
            if (absence != null) _context.Absence.Remove(absence);

            if (grade == null)
            {
                grade = new Grade
                {
                    StudentId = request.StudentId,
                    SubjectId = request.SubjectId,
                    Date = date,
                    Value = val
                };
                _context.Grade.Add(grade);
            }
            else
            {
                grade.Value = val;
                _context.Grade.Update(grade);
            }

            _context.SaveChanges();
            return Ok();
        }

        public class SaveCellRequest
        {
            public int StudentId { get; set; }
            public int SubjectId { get; set; }
            public string Date { get; set; }
            public string Type { get; set; }
            public object Value { get; set; }
        }
    }
}
