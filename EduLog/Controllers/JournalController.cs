using EduLog.Data;
using EduLog.Models;
using EduLog.Services;
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
        private readonly IGamificationService _gamification;

        public JournalController(
            EduLogContext context,
            UserManager<ApplicationUser> userManager,
            IGamificationService gamification)
        {
            _context = context;
            _userManager = userManager;
            _gamification = gamification;
        }

        private async Task<Teacher?> GetCurrentTeacherAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.TeacherId == null) return null;
            return _context.Teacher.FirstOrDefault(t => t.Id == user.TeacherId);
        }

        // Source of truth: SubjectTeacher M2M. A class is "the teacher's"
        // if at least one of its bound subjects assigns this teacher.
        private IQueryable<Class> ClassesForTeacher(int teacherId)
        {
            return _context.Class
                .Where(c => c.ClassSubjects
                    .Any(cs => cs.Subject.SubjectTeachers
                        .Any(st => st.TeacherId == teacherId)));
        }

        public async Task<IActionResult> TeacherSchedule()
        {
            var teacher = await GetCurrentTeacherAsync();
            if (teacher == null)
            {
                return RedirectToAction("Index", "Profile");
            }

            var currentYear = await _context.AcademicYear
                .Where(y => y.SchoolId == teacher.SchoolId && y.IsCurrent && !y.IsArchived)
                .OrderByDescending(y => y.StartDate)
                .FirstOrDefaultAsync();

            if (currentYear == null)
            {
                ViewData["ScheduleSlots"] = new List<TeacherScheduleSlotView>();
                ViewData["SelectedYear"] = null;
                return View();
            }

            var slots = await _context.ScheduleSlot
                .Include(s => s.Subject)
                .Include(s => s.Class)
                .Where(s => s.TeacherId == teacher.Id && s.AcademicYearId == currentYear.Id)
                .OrderBy(s => s.DayOfWeek)
                .ThenBy(s => s.LessonNumber)
                .Select(s => new TeacherScheduleSlotView
                {
                    DayOfWeek = s.DayOfWeek,
                    LessonNumber = s.LessonNumber,
                    SubjectName = s.Subject.Name,
                    ClassName = s.Class.Name,
                    Room = s.Room
                })
                .ToListAsync();

            ViewData["ScheduleSlots"] = slots;
            ViewData["SelectedYear"] = currentYear;
            return View();
        }

        public IActionResult Index(int classId, int subjectId, string viewMode = "week",
            int? month = null, int? year = null)
        {
            var now = DateTime.Now;
            int selectedMonth = month ?? now.Month;
            int selectedYear = year ?? now.Year;

            // Визначаємо дні для різних режимів
            List<DateTime> days;
            if (viewMode == "day")
            {
                days = new List<DateTime> { now.Date };
            }
            else if (viewMode == "week")
            {
                // Поточний тиждень (понеділок-п'ятниця)
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
                ? ClassesForTeacher(teacher.Id).ToList()
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

            var classes = ClassesForTeacher(teacher.Id).ToList();

            if (classes.Count == 0)
                return RedirectToAction("Index", "Profile");
            if (classes.Count == 1)
                return RedirectToAction("SelectSubject", new { classId = classes[0].Id });
            return RedirectToAction("SelectClasses");
        }

        public async Task<IActionResult> SelectSubject(int classId)
        {
            var teacher = await GetCurrentTeacherAsync();
            if (teacher == null)
                return RedirectToAction("Index", "Profile");

            // Only subjects taught BY THIS TEACHER in this class
            var subjects = _context.ClassSubject
                .Where(cs => cs.ClassId == classId
                    && cs.Subject.SubjectTeachers.Any(st => st.TeacherId == teacher.Id))
                .Select(cs => cs.Subject)
                .ToList();

            var cls = _context.Class.FirstOrDefault(c => c.Id == classId);
            var classes = ClassesForTeacher(teacher.Id).ToList();

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

            var classes = ClassesForTeacher(teacher.Id).ToList();
            return View(classes);
        }

        [HttpPost]
        public async Task<IActionResult> SaveCell([FromBody] SaveCellRequest request)
        {
            if (request == null)
                return BadRequest();

            // Парсимо дату
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

            bool wasAbsenceChanged = false;
            int? newGradeValue = null;
            int? previousGradeValue = grade?.Value;

            // Empty cell — remove both grade and absence
            if (string.IsNullOrWhiteSpace(raw))
            {
                if (grade != null) _context.Grade.Remove(grade);
                if (absence != null) { _context.Absence.Remove(absence); wasAbsenceChanged = true; }
                await _context.SaveChangesAsync();
            }
            else if (isAbsence)
            {
                // "Н" — mark absence, remove grade if any
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
                    wasAbsenceChanged = true;
                }
                await _context.SaveChangesAsync();
            }
            else
            {
                // Validate grade 1-12
                if (!int.TryParse(raw, out var val) || val < 1 || val > 12)
                {
                    return BadRequest(new { error = "Допустимі значення: 1-12 або Н" });
                }

                // Save grade, remove absence if any
                if (absence != null) { _context.Absence.Remove(absence); wasAbsenceChanged = true; }

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
                    newGradeValue = val;
                }
                else
                {
                    if (grade.Value != val)
                    {
                        newGradeValue = val;
                    }
                    grade.Value = val;
                    _context.Grade.Update(grade);
                }

                await _context.SaveChangesAsync();
            }

            // Gamification: award XP/coins for newly added or changed grade.
            // Only award when value increased compared to previous (avoid farming by re-saving same value).
            if (newGradeValue.HasValue && (previousGradeValue == null || newGradeValue.Value > previousGradeValue.Value))
            {
                await _gamification.ProcessGradeAddedAsync(request.StudentId, newGradeValue.Value);
            }

            // Recalculate streak when attendance changed (absence added/removed) for that day.
            if (wasAbsenceChanged)
            {
                await _gamification.RecalculateStreakAsync(request.StudentId, date);
            }

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
