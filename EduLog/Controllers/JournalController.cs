using EduLog.Data;
using EduLog.Models;
using EduLog.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

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
                ViewData["NoTeacherLinked"] = true;
                ViewData["ScheduleSlots"] = new List<TeacherScheduleSlotView>();
                return View();
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

            var subjectColors = slots
                .Select(s => s.SubjectName)
                .Distinct()
                .ToDictionary(name => name, name => GenerateDeterministicHexColor(name));

            ViewData["ScheduleSlots"] = slots;
            ViewData["SelectedYear"] = currentYear;
            ViewData["SubjectColors"] = subjectColors;
            return View();
        }

        public IActionResult Index(int classId, int subjectId, string viewMode = "week",
            int? month = null, int? year = null, int? day = null, string? weekStart = null)
        {
            var now = DateTime.Now;
            int selectedMonth = month ?? now.Month;
            int selectedYear = year ?? now.Year;

            // Визначаємо дні для різних режимів
            List<DateTime> days;
            DateTime? selectedDay = null;
            DateTime? mondayOfWeek = null;

            if (viewMode == "day")
            {
                DateTime pick;
                if (day.HasValue && month.HasValue && year.HasValue
                    && DateTime.TryParse($"{year:D4}-{month:D2}-{day:D2}", out var parsed))
                {
                    pick = parsed.Date;
                }
                else
                {
                    pick = now.Date;
                }
                selectedDay = pick;
                days = new List<DateTime> { pick };
            }
            else if (viewMode == "week")
            {
                DateTime monday;
                if (!string.IsNullOrWhiteSpace(weekStart)
                    && DateTime.TryParse(weekStart, out var ws))
                {
                    monday = ws.Date;
                }
                else
                {
                    int diff = (7 + (now.DayOfWeek - DayOfWeek.Monday)) % 7;
                    monday = now.AddDays(-1 * diff).Date;
                }
                // Нормалізуємо до понеділка
                int normDiff = (7 + (monday.DayOfWeek - DayOfWeek.Monday)) % 7;
                monday = monday.AddDays(-normDiff);
                mondayOfWeek = monday;
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

            var studentIds = students.Select(s => s.Id).ToList();
            var monthsInRange = days.Select(d => d.Month).Distinct().ToList();
            var yearsInRange = days.Select(d => d.Year).Distinct().ToList();

            var customColumns = _context.CustomGradeColumn
                .Where(c => c.ClassId == classId && c.SubjectId == subjectId)
                .OrderBy(c => c.Date).ThenBy(c => c.Id)
                .ToList();
            var customColumnIds = customColumns.Select(c => c.Id).ToList();

            var grades = _context.Grade
                .Where(g => g.SubjectId == subjectId
                    && studentIds.Contains(g.StudentId)
                    && ((g.CustomGradeColumnId == null
                            && monthsInRange.Contains(g.Date.Month)
                            && yearsInRange.Contains(g.Date.Year))
                        || (g.CustomGradeColumnId != null
                            && customColumnIds.Contains(g.CustomGradeColumnId.Value))))
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

            // Навігаційні дати для prev/next
            DateTime? prevDay = null, nextDay = null;
            if (selectedDay.HasValue)
            {
                var p = selectedDay.Value.AddDays(-1);
                while (p.DayOfWeek == DayOfWeek.Saturday || p.DayOfWeek == DayOfWeek.Sunday)
                    p = p.AddDays(-1);
                prevDay = p;

                var n = selectedDay.Value.AddDays(1);
                while (n.DayOfWeek == DayOfWeek.Saturday || n.DayOfWeek == DayOfWeek.Sunday)
                    n = n.AddDays(1);
                nextDay = n;
            }

            DateTime? prevWeekStart = null, nextWeekStart = null;
            if (mondayOfWeek.HasValue)
            {
                prevWeekStart = mondayOfWeek.Value.AddDays(-7);
                nextWeekStart = mondayOfWeek.Value.AddDays(7);
            }

            ViewBag.ViewMode = viewMode;
            ViewBag.SelectedMonth = selectedMonth;
            ViewBag.SelectedYear = selectedYear;
            ViewBag.SelectedDay = selectedDay;
            ViewBag.WeekStart = mondayOfWeek;
            ViewBag.PrevDay = prevDay;
            ViewBag.NextDay = nextDay;
            ViewBag.PrevWeekStart = prevWeekStart;
            ViewBag.NextWeekStart = nextWeekStart;
            ViewBag.AllClasses = allClasses;
            ViewBag.SubjectsForClass = subjectsForClass;

            var gradeScale = _context.GradeScale
                .FirstOrDefault(s => s.ClassId == classId && s.SubjectId == subjectId);

            var Model = new JournalView
            {
                Month = selectedMonth,
                Year = selectedYear,
                Days = days,
                Class = Cls,
                Students = students,
                Subject = subject,
                Grades = grades,
                Absences = absences,
                CustomColumns = customColumns,
                Scale = gradeScale
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
            var isCustomColumn = request.CustomColumnId.HasValue;
            var isAbsence = !isCustomColumn && raw.Equals("н", StringComparison.OrdinalIgnoreCase);

            var grade = isCustomColumn
                ? _context.Grade.FirstOrDefault(g =>
                    g.StudentId == request.StudentId &&
                    g.SubjectId == request.SubjectId &&
                    g.CustomGradeColumnId == request.CustomColumnId)
                : _context.Grade.FirstOrDefault(g =>
                    g.StudentId == request.StudentId &&
                    g.SubjectId == request.SubjectId &&
                    g.CustomGradeColumnId == null &&
                    g.Date.Date == date.Date);

            var absence = isCustomColumn ? null : _context.Absence.FirstOrDefault(a =>
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
                // Determine class via student to look up the scale for (class, subject)
                var classId = _context.Student
                    .Where(s => s.Id == request.StudentId)
                    .Select(s => (int?)s.ClassId)
                    .FirstOrDefault();

                var scale = classId.HasValue
                    ? _context.GradeScale.FirstOrDefault(s => s.ClassId == classId && s.SubjectId == request.SubjectId)
                    : null;

                int? numericVal = null;
                string? verbalVal = null;
                string? validationError = null;

                if (scale == null || scale.ScaleType == ScaleType.Numeric)
                {
                    int min = scale?.MinValue ?? 1;
                    int max = scale?.MaxValue ?? 12;
                    if (!int.TryParse(raw, out var val) || val < min || val > max)
                        validationError = $"Допустимі значення: {min}–{max} або Н";
                    else
                        numericVal = val;
                }
                else if (scale.ScaleType == ScaleType.Percent)
                {
                    if (!int.TryParse(raw, out var val) || val < 0 || val > 100)
                        validationError = "Допустимі значення: 0–100 або Н";
                    else
                        numericVal = val;
                }
                else // Verbal
                {
                    var options = ParseVerbalOptions(scale.VerbalOptions);
                    var match = options.FirstOrDefault(o => string.Equals(o, raw, StringComparison.OrdinalIgnoreCase));
                    if (match == null)
                        validationError = "Допустимі значення: " + (options.Count > 0 ? string.Join(", ", options) : "(порожньо)") + " або Н";
                    else
                        verbalVal = match;
                }

                if (validationError != null)
                    return BadRequest(new { error = validationError });

                // Save grade, remove absence if any
                if (absence != null) { _context.Absence.Remove(absence); wasAbsenceChanged = true; }

                if (grade == null)
                {
                    grade = new Grade
                    {
                        StudentId = request.StudentId,
                        SubjectId = request.SubjectId,
                        Date = date,
                        Value = numericVal,
                        VerbalValue = verbalVal,
                        CustomGradeColumnId = request.CustomColumnId
                    };
                    _context.Grade.Add(grade);
                    newGradeValue = numericVal;
                }
                else
                {
                    if (grade.Value != numericVal || grade.VerbalValue != verbalVal)
                    {
                        newGradeValue = numericVal;
                    }
                    grade.Value = numericVal;
                    grade.VerbalValue = verbalVal;
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
            public int? CustomColumnId { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> CreateCustomColumn(int classId, int subjectId, string name, DateTime date)
        {
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest(new { error = "Назва колонки обов'язкова." });

            var teacher = await GetCurrentTeacherAsync();
            if (teacher == null)
                return Forbid();

            var column = new CustomGradeColumn
            {
                ClassId = classId,
                SubjectId = subjectId,
                Name = name.Trim(),
                Date = date.Date,
                CreatedByTeacherId = teacher.Id
            };

            _context.CustomGradeColumn.Add(column);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                columnId = column.Id,
                name = column.Name,
                date = column.Date.ToString("yyyy-MM-dd")
            });
        }

        [HttpGet]
        public IActionResult GradeScaleSettings(int classId, int subjectId)
        {
            var scale = _context.GradeScale.FirstOrDefault(s => s.ClassId == classId && s.SubjectId == subjectId);
            if (scale == null)
                return Json(new { exists = false });

            return Json(new
            {
                exists = true,
                scaleType = (int)scale.ScaleType,
                minValue = scale.MinValue,
                maxValue = scale.MaxValue,
                verbalOptions = ParseVerbalOptions(scale.VerbalOptions)
            });
        }

        [HttpPost]
        public async Task<IActionResult> GradeScaleSettings(int classId, int subjectId, ScaleType scaleType,
            int? minValue, int? maxValue, string? verbalOptionsRaw)
        {
            int? min = null;
            int? max = null;
            string? optionsJson = null;

            switch (scaleType)
            {
                case ScaleType.Numeric:
                    if (!minValue.HasValue || !maxValue.HasValue || minValue >= maxValue)
                        return BadRequest(new { error = "Введіть коректні Min та Max (Min < Max)." });
                    min = minValue;
                    max = maxValue;
                    break;
                case ScaleType.Percent:
                    min = 0;
                    max = 100;
                    break;
                case ScaleType.Verbal:
                    var opts = (verbalOptionsRaw ?? "")
                        .Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(o => o.Trim())
                        .Where(o => o.Length > 0)
                        .Distinct()
                        .ToList();
                    if (opts.Count == 0)
                        return BadRequest(new { error = "Введіть принаймні один варіант." });
                    optionsJson = JsonSerializer.Serialize(opts);
                    break;
            }

            var scale = _context.GradeScale.FirstOrDefault(s => s.ClassId == classId && s.SubjectId == subjectId);
            if (scale == null)
            {
                scale = new GradeScale
                {
                    ClassId = classId,
                    SubjectId = subjectId,
                    ScaleType = scaleType,
                    MinValue = min,
                    MaxValue = max,
                    VerbalOptions = optionsJson
                };
                _context.GradeScale.Add(scale);
            }
            else
            {
                scale.ScaleType = scaleType;
                scale.MinValue = min;
                scale.MaxValue = max;
                scale.VerbalOptions = optionsJson;
                _context.GradeScale.Update(scale);
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        private static List<string> ParseVerbalOptions(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<string>();
            try
            {
                return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteCustomColumn(int columnId)
        {
            var column = await _context.CustomGradeColumn.FirstOrDefaultAsync(c => c.Id == columnId);
            if (column == null)
                return NotFound();

            var grades = _context.Grade.Where(g => g.CustomGradeColumnId == columnId);
            _context.Grade.RemoveRange(grades);
            _context.CustomGradeColumn.Remove(column);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        private static string GenerateDeterministicHexColor(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "#6C757D";

            unchecked
            {
                var hash = 17;
                foreach (var ch in input.Trim())
                    hash = hash * 31 + ch;

                var r = 80 + Math.Abs(hash & 0x7F);
                var g = 80 + Math.Abs((hash >> 8) & 0x7F);
                var b = 80 + Math.Abs((hash >> 16) & 0x7F);

                return $"#{r:X2}{g:X2}{b:X2}";
            }
        }

    }
}
