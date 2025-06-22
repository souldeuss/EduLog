using EduLog.Data;
using EduLog.Models;
using Microsoft.AspNetCore.Mvc;

namespace EduLog.Controllers
{
    public class JournalController : Controller
    {
        private readonly EduLogContext _context;

        public JournalController(EduLogContext context)
        {
            _context = context;
        }
        public IActionResult Index(int classId, int subjectId, string viewMode = "week")
        {
            var now = DateTime.Now;
            int selectedMonth = now.Month;
            int selectedYear = now.Year;

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
            else // "month" або інше
            {
                int daysInMonth = DateTime.DaysInMonth(selectedYear, selectedMonth);
                days = Enumerable.Range(1, daysInMonth)
                    .Select(d => new DateTime(selectedYear, selectedMonth, d))
                    .ToList();
            }

            var students = _context.Student
                .Where(s => s.ClassId == classId)
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

            // Передаємо режим у ViewBag
            ViewBag.ViewMode = viewMode;

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

        public IActionResult spreader()
        {
            var teacher = _context.Teacher.FirstOrDefault();
            var classes = _context.Class
             .Where(c => c.ClassSubjects.Any(cs => cs.Subject.TeacherId == teacher.Id))
             .ToList();

            if (classes.Count == 0)
                return RedirectToAction("Index", "Profile");
            else if (classes.Count == 1)
                return RedirectToAction("SelectSubject", new { classId = classes[0].Id });
            else if (classes.Count > 1)
                return RedirectToAction("SelectClasses");
            return View();
        }

        public IActionResult SelectSubject(int classId)
        {
            var teacher = _context.Teacher.FirstOrDefault();
            var subjects = _context.ClassSubject
                .Where(cs => cs.ClassId == classId)
                .Select(cs => cs.Subject)
                .ToList();
            var cls = _context.Class
                .FirstOrDefault(c => c.Id == classId); // <-- виправлено тут
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

        //public IActionResult SelectClasses(int? subjectId)
        //{
        //    var teacher = _context.Teacher.FirstOrDefault();
        //    if (teacher == null)
        //        return RedirectToAction("Index", "Profile");

        //    var ListSubjects = _context.Subject
        //        .Where(s => s.TeacherId == teacher.Id)
        //        .ToList();

        //    if (!subjectId.HasValue && ListSubjects.Count == 1)
        //    {
        //        // Redirect to self with the only subject's ID
        //        return RedirectToAction("SelectClasses", new { subjectId = ListSubjects[0].Id });
        //    }

        //    ViewData["Subjects"] = ListSubjects;

        //    var classesQuery = _context.Class
        //        .Where(c => _context.Subject.Any(s => s.ClassId == c.Id && s.Id == ListSubjects.First().Id));

        //    if (subjectId.HasValue && subjectId.Value != 0)
        //    {
        //        var classIdsWithSubject = _context.Subject
        //            .Where(s => s.Id == subjectId.Value)
        //            .Select(s => s.ClassId)
        //            .Distinct()
        //            .ToList();

        //        classesQuery = classesQuery.Where(c => classIdsWithSubject.Contains(c.Id));
        //    }

        //    var classes = classesQuery.ToList();
        //    return View(classes);
        //}
        public IActionResult SelectClasses()
        {
            var teacher = _context.Teacher.FirstOrDefault();
            if (teacher == null)
                return RedirectToAction("Index", "Profile");

            var classes = _context.Class
                .Where(c => c.TeacherId == teacher.Id)
                .ToList();

            return View(classes);
        }

        [HttpPost]
        public IActionResult SaveCell([FromBody] SaveCellRequest request)
        {
            if (request == null || request.Type != "grade")
                return BadRequest();

            // Парсимо дату
            if (!DateTime.TryParse(request.Date, out var date))
                return BadRequest();

            // Шукаємо оцінку
            var grade = _context.Grade.FirstOrDefault(g =>
                g.StudentId == request.StudentId &&
                g.SubjectId == request.SubjectId &&
                g.Date.Date == date.Date);

            if (grade == null)
            {
                // Додаємо нову оцінку
                grade = new Grade
                {
                    StudentId = request.StudentId,
                    SubjectId = request.SubjectId,
                    Date = date,
                    Value = int.TryParse(request.Value?.ToString(), out var val) ? val : 0
                };
                _context.Grade.Add(grade);
            }
            else
            {
                // Оновлюємо існуючу
                grade.Value = int.TryParse(request.Value?.ToString(), out var val) ? val : 0;
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
