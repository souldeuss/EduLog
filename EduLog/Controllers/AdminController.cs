using EduLog.Data;
using EduLog.Models;
using EduLog.Models.Admin;
using EduLog.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EduLog.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly EduLogContext _context;
        private readonly ITenantService _tenantService;
        private readonly ISchedulerService _schedulerService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            EduLogContext context,
            ITenantService tenantService,
            ISchedulerService schedulerService,
            UserManager<ApplicationUser> userManager,
            ILogger<AdminController> logger)
        {
            _context = context;
            _tenantService = tenantService;
            _schedulerService = schedulerService;
            _userManager = userManager;
            _logger = logger;
        }

        // ───────── Dashboard ─────────
        public IActionResult Index()
        {
            ViewData["ClassCount"] = _context.Class.Count();
            ViewData["TeacherCount"] = _context.Teacher.Count();
            ViewData["StudentCount"] = _context.Student.Count();
            ViewData["SubjectCount"] = _context.Subject.Count();
            return View();
        }

        // ───────── Classes ─────────
        public IActionResult Classes()
        {
            var classes = _context.Class.Include(c => c.ClassSubjects).ToList();
            var teachers = _context.Teacher.ToList();
            ViewData["Teachers"] = teachers;
            return View(classes);
        }

        public IActionResult CreateClass()
        {
            ViewData["Teachers"] = new SelectList(_context.Teacher.ToList(), "Id", "Surname");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateClass(string name, int? teacherId)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                ModelState.AddModelError("", "Введіть назву класу");
                ViewData["Teachers"] = new SelectList(_context.Teacher.ToList(), "Id", "Surname");
                return View();
            }

            var cls = new Class { Name = name.Trim(), TeacherId = teacherId };
            _context.Class.Add(cls);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Classes));
        }

        public IActionResult EditClass(int id)
        {
            var cls = _context.Class
                .Include(c => c.ClassSubjects).ThenInclude(cs => cs.Subject)
                .FirstOrDefault(c => c.Id == id);
            if (cls == null) return NotFound();

            ViewData["Teachers"] = new SelectList(_context.Teacher.ToList(), "Id", "Surname", cls.TeacherId);
            ViewData["AllSubjects"] = _context.Subject.ToList();
            return View(cls);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateClass(int id, string name, int? teacherId)
        {
            var cls = await _context.Class.FindAsync(id);
            if (cls == null) return NotFound();

            cls.Name = name?.Trim() ?? cls.Name;
            cls.TeacherId = teacherId;
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(EditClass), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteClass(int id)
        {
            var cls = await _context.Class.FindAsync(id);
            if (cls == null) return NotFound();
            _context.Class.Remove(cls);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Classes));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BindSubjectToClass(int classId, int subjectId)
        {
            var exists = await _context.ClassSubject
                .AnyAsync(cs => cs.ClassId == classId && cs.SubjectId == subjectId);
            if (!exists)
            {
                _context.ClassSubject.Add(new ClassSubject { ClassId = classId, SubjectId = subjectId });
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(EditClass), new { id = classId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnbindSubjectFromClass(int classId, int subjectId)
        {
            var cs = await _context.ClassSubject
                .FirstOrDefaultAsync(x => x.ClassId == classId && x.SubjectId == subjectId);
            if (cs != null)
            {
                _context.ClassSubject.Remove(cs);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(EditClass), new { id = classId });
        }

        // ───────── Class Students ─────────
        public IActionResult ClassStudents(int id)
        {
            var cls = _context.Class.FirstOrDefault(c => c.Id == id);
            if (cls == null) return NotFound();

            var students = _context.Student.Where(s => s.ClassId == id).OrderBy(s => s.Surname).ToList();
            ViewData["Class"] = cls;
            return View(students);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddStudent(int classId, string surname, string name, string? patronymic)
        {
            if (string.IsNullOrWhiteSpace(surname) || string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Прізвище та ім'я обов'язкові";
                return RedirectToAction(nameof(ClassStudents), new { id = classId });
            }

            _context.Student.Add(new Student
            {
                ClassId = classId,
                Surname = surname.Trim(),
                Name = name.Trim(),
                Patronymic = patronymic?.Trim() ?? ""
            });
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(ClassStudents), new { id = classId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditStudent(int id, string surname, string name, string? patronymic)
        {
            var student = await _context.Student.FindAsync(id);
            if (student == null) return NotFound();

            student.Surname = surname?.Trim() ?? student.Surname;
            student.Name = name?.Trim() ?? student.Name;
            student.Patronymic = patronymic?.Trim() ?? student.Patronymic;
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(ClassStudents), new { id = student.ClassId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteStudent(int id)
        {
            var student = await _context.Student.FindAsync(id);
            if (student == null) return NotFound();
            int classId = student.ClassId;
            _context.Student.Remove(student);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(ClassStudents), new { id = classId });
        }

        // ───────── Teachers ─────────
        public async Task<IActionResult> Teachers()
        {
            var teachers = await _context.Teacher.ToListAsync();
            var users = await _userManager.Users
                .Where(u => u.SchoolId == _tenantService.SchoolId)
                .ToListAsync();
            ViewData["Users"] = users;
            return View(teachers);
        }

        // ───────── Subjects ─────────
        public IActionResult Subjects()
        {
            var subjects = _context.Subject
                .Include(s => s.Teacher)
                .Include(s => s.ClassSubjects).ThenInclude(cs => cs.Class)
                .ToList();
            ViewData["Teachers"] = _context.Teacher.ToList();
            ViewData["Classes"] = _context.Class.OrderBy(c => c.Name).ToList();
            return View(subjects);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSubject(string name, int teacherId, int[] classIds)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Введіть назву предмета";
                return RedirectToAction(nameof(Subjects));
            }

            var subject = new Subject { Name = name.Trim(), TeacherId = teacherId };
            _context.Subject.Add(subject);
            await _context.SaveChangesAsync();

            if (classIds != null)
            {
                foreach (var cid in classIds)
                {
                    _context.ClassSubject.Add(new ClassSubject { ClassId = cid, SubjectId = subject.Id });
                }
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Subjects));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSubject(int id)
        {
            var subject = await _context.Subject.FindAsync(id);
            if (subject == null) return NotFound();

            var bindings = _context.ClassSubject.Where(cs => cs.SubjectId == id);
            _context.ClassSubject.RemoveRange(bindings);
            _context.Subject.Remove(subject);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Subjects));
        }

        [HttpPost]
        public async Task<IActionResult> AddClassToSubject([FromBody] AddClassRequest request)
        {
            var exists = await _context.ClassSubject
                .AnyAsync(cs => cs.ClassId == request.ClassId && cs.SubjectId == request.SubjectId);
            if (!exists)
            {
                _context.ClassSubject.Add(new ClassSubject { ClassId = request.ClassId, SubjectId = request.SubjectId });
                await _context.SaveChangesAsync();
            }
            var cls = await _context.Class.FindAsync(request.ClassId);
            return Json(new { success = true, className = cls?.Name });
        }

        [HttpPost]
        public async Task<IActionResult> RemoveClassFromSubject([FromBody] RemoveClassRequest request)
        {
            var cs = await _context.ClassSubject
                .FirstOrDefaultAsync(x => x.ClassId == request.ClassId && x.SubjectId == request.SubjectId);
            if (cs != null)
            {
                _context.ClassSubject.Remove(cs);
                await _context.SaveChangesAsync();
            }
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> ChangeSubjectTeacher([FromBody] ChangeTeacherRequest request)
        {
            var subject = await _context.Subject.FindAsync(request.SubjectId);
            if (subject == null) return NotFound();

            subject.TeacherId = request.TeacherId;
            await _context.SaveChangesAsync();

            var teacher = await _context.Teacher.FindAsync(request.TeacherId);
            return Json(new { success = true, teacherName = $"{teacher?.Surname} {teacher?.Name}" });
        }

        [HttpPost]
        public async Task<IActionResult> BulkCreateSubjects([FromBody] List<BulkSubjectItem> items)
        {
            if (items == null || !items.Any())
                return Json(new { success = false, error = "Список порожній" });

            int created = 0;
            foreach (var item in items)
            {
                if (!string.IsNullOrWhiteSpace(item.Name) && item.TeacherId > 0)
                {
                    _context.Subject.Add(new Subject { Name = item.Name.Trim(), TeacherId = item.TeacherId });
                    created++;
                }
            }
            await _context.SaveChangesAsync();
            return Json(new { success = true, created });
        }

        [HttpPost]
        public async Task<IActionResult> ImportSubjects(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return Json(new { success = false, error = "Файл порожній" });

            var rows = new List<ImportSubjectRow>();
            using var stream = file.OpenReadStream();

            if (file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                using var reader = new StreamReader(stream);
                string? line;
                bool first = true;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (first) { first = false; continue; }
                    var parts = line.Split(new[] { ',', ';', '\t' }, StringSplitOptions.TrimEntries);
                    if (parts.Length >= 1 && !string.IsNullOrWhiteSpace(parts[0]))
                    {
                        rows.Add(new ImportSubjectRow
                        {
                            Name = parts[0],
                            TeacherName = parts.Length > 1 ? parts[1] : "",
                            ClassName = parts.Length > 2 ? parts[2] : ""
                        });
                    }
                }
            }
            else
            {
                using var workbook = new ClosedXML.Excel.XLWorkbook(stream);
                var sheet = workbook.Worksheets.First();
                var rowCount = sheet.LastRowUsed()?.RowNumber() ?? 0;
                for (int i = 2; i <= rowCount; i++)
                {
                    var name = sheet.Cell(i, 1).GetString().Trim();
                    var teacher = sheet.Cell(i, 2).GetString().Trim();
                    var className = sheet.Cell(i, 3).GetString().Trim();
                    if (!string.IsNullOrEmpty(name))
                    {
                        rows.Add(new ImportSubjectRow { Name = name, TeacherName = teacher, ClassName = className });
                    }
                }
            }

            return Json(new { success = true, rows });
        }

        [HttpPost]
        public async Task<IActionResult> ImportSubjectsConfirm([FromBody] List<ImportSubjectRow> rows)
        {
            if (rows == null || !rows.Any())
                return Json(new { success = false, error = "Список порожній" });

            var teachers = await _context.Teacher.ToListAsync();
            var classes = await _context.Class.ToListAsync();
            int created = 0;

            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.Name)) continue;

                var teacher = teachers.FirstOrDefault(t =>
                    $"{t.Surname} {t.Name}" == row.TeacherName ||
                    $"{t.Surname} {t.Name} {t.Patronymic}" == row.TeacherName ||
                    t.Surname == row.TeacherName);
                if (teacher == null) continue;

                var subject = new Subject { Name = row.Name.Trim(), TeacherId = teacher.Id };
                _context.Subject.Add(subject);
                await _context.SaveChangesAsync();

                if (!string.IsNullOrWhiteSpace(row.ClassName))
                {
                    var cls = classes.FirstOrDefault(c => c.Name == row.ClassName.Trim());
                    if (cls != null)
                    {
                        _context.ClassSubject.Add(new ClassSubject { ClassId = cls.Id, SubjectId = subject.Id });
                    }
                }
                created++;
            }
            await _context.SaveChangesAsync();
            return Json(new { success = true, created });
        }

        // ───────── Academic Years ─────────
        public IActionResult AcademicYears()
        {
            var years = _context.AcademicYear.OrderByDescending(y => y.StartDate).ToList();
            return View(years);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAcademicYear(string name, DateTime startDate, DateTime endDate)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Введіть назву навчального року";
                return RedirectToAction(nameof(AcademicYears));
            }

            _context.AcademicYear.Add(new AcademicYear
            {
                Name = name.Trim(),
                StartDate = startDate,
                EndDate = endDate
            });
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(AcademicYears));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetCurrent(int id)
        {
            var all = await _context.AcademicYear.ToListAsync();
            foreach (var y in all) y.IsCurrent = y.Id == id;
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(AcademicYears));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleArchive(int id)
        {
            var year = await _context.AcademicYear.FindAsync(id);
            if (year == null) return NotFound();
            year.IsArchived = !year.IsArchived;
            if (year.IsArchived) year.IsCurrent = false;
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(AcademicYears));
        }

        // ───────── Schedule ─────────
        public async Task<IActionResult> Schedule(int? yearId, int? classId)
        {
            var years = _context.AcademicYear.Where(y => !y.IsArchived).OrderByDescending(y => y.StartDate).ToList();
            var classes = _context.Class.OrderBy(c => c.Name).ToList();
            var schedulerModes = await _schedulerService.GetAvailableModesAsync(HttpContext.RequestAborted);

            var selectedYear = yearId.HasValue
                ? years.FirstOrDefault(y => y.Id == yearId)
                : years.FirstOrDefault(y => y.IsCurrent) ?? years.FirstOrDefault();

            var selectedClass = classId.HasValue
                ? classes.FirstOrDefault(c => c.Id == classId)
                : classes.FirstOrDefault();

            var slots = new List<ScheduleSlot>();
            var allSlots = new List<ScheduleSlot>();

            if (selectedYear != null)
            {
                allSlots = _context.ScheduleSlot
                    .Include(s => s.Class)
                    .Include(s => s.Subject)
                    .Include(s => s.Teacher)
                    .Where(s => s.AcademicYearId == selectedYear.Id)
                    .ToList();

                if (selectedClass != null)
                {
                    slots = allSlots.Where(s => s.ClassId == selectedClass.Id).ToList();
                }
            }

            // Detect conflicts across ALL slots for this academic year
            var conflicts = DetectConflicts(allSlots);

            ViewData["Years"] = years;
            ViewData["Classes"] = classes;
            ViewData["SelectedYear"] = selectedYear;
            ViewData["SelectedClass"] = selectedClass;
            ViewData["Slots"] = slots;
            ViewData["Conflicts"] = conflicts;
            ViewData["Teachers"] = new SelectList(_context.Teacher.ToList(), "Id", "Surname");
            ViewData["Subjects"] = new SelectList(_context.Subject.ToList(), "Id", "Name");
            ViewData["SchedulerModes"] = schedulerModes;
            ViewData["SchedulerConfig"] = new SchedulerConfigOptions();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateAiSchedule(
            int yearId,
            int? classId,
            SchedulerMode mode,
            SchedulerConfigOptions options,
            CancellationToken cancellationToken)
        {
            return await GenerateAndPersistScheduleAsync(yearId, classId, mode, options, mergeExisting: false, cancellationToken);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApplyGeneratedSchedule(
            int yearId,
            int? classId,
            SchedulerMode mode,
            SchedulerConfigOptions options,
            bool mergeExisting,
            CancellationToken cancellationToken)
        {
            return await GenerateAndPersistScheduleAsync(yearId, classId, mode, options, mergeExisting, cancellationToken);
        }

        [HttpGet]
        public async Task<IActionResult> ExportSchedule(int yearId, string format = "json", CancellationToken cancellationToken = default)
        {
            try
            {
                var schoolId = _tenantService.SchoolId;
                if (schoolId == null)
                {
                    return BadRequest("School is not selected.");
                }

                var bytes = await _schedulerService.ExportScheduleAsync(schoolId.Value, yearId, format, cancellationToken);
                var fileExtension = string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase) ? "csv" : "json";
                var fileName = $"schedule_{yearId}.{fileExtension}";
                var contentType = string.Equals(fileExtension, "csv", StringComparison.OrdinalIgnoreCase)
                    ? "text/csv"
                    : "application/json";

                return File(bytes, contentType, fileName);
            }
            catch (HttpRequestException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Schedule), new { yearId, classId = (int?)null });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportSchedule(IFormFile file, int yearId, CancellationToken cancellationToken)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Оберіть файл для імпорту.";
                return RedirectToAction(nameof(Schedule), new { yearId });
            }

            try
            {
                var schoolId = _tenantService.SchoolId;
                if (schoolId == null)
                {
                    return BadRequest("School is not selected.");
                }

                await using var stream = file.OpenReadStream();
                var importedSlots = await _schedulerService.ImportScheduleAsync(stream, schoolId.Value, yearId, cancellationToken);
                await ReplaceScheduleSlotsAsync(yearId, importedSlots, cancellationToken);

                TempData["Success"] = $"Імпортовано {importedSlots.Count} слотів.";
                return RedirectToAction(nameof(Schedule), new { yearId });
            }
            catch (HttpRequestException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Schedule), new { yearId });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSlot(int? slotId, int yearId, int classId,
            int dayOfWeek, int lessonNumber, int subjectId, int teacherId, string? room)
        {
            var isAjax = string.Equals(
                Request.Headers["X-Requested-With"],
                "XMLHttpRequest",
                StringComparison.OrdinalIgnoreCase);

            // Check conflicts before saving
            var existingSlots = await _context.ScheduleSlot
                .Where(s => s.AcademicYearId == yearId && s.DayOfWeek == dayOfWeek && s.LessonNumber == lessonNumber)
                .Where(s => slotId == null || s.Id != slotId)
                .ToListAsync();

            var conflictMessages = new List<string>();

            if (existingSlots.Any(s => s.TeacherId == teacherId))
                conflictMessages.Add("Цей вчитель вже має урок у цей час");

            if (!string.IsNullOrWhiteSpace(room) && existingSlots.Any(s => s.Room == room.Trim()))
                conflictMessages.Add($"Кабінет \"{room.Trim()}\" вже зайнятий у цей час");

            if (existingSlots.Any(s => s.ClassId == classId))
                conflictMessages.Add("Цей клас вже має урок у цей час");

            if (conflictMessages.Any())
            {
                var errorMessage = string.Join("; ", conflictMessages);
                if (isAjax)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = errorMessage,
                        conflicts = conflictMessages
                    });
                }

                TempData["Error"] = errorMessage;
                return RedirectToAction(nameof(Schedule), new { yearId, classId });
            }

            ScheduleSlot? savedSlot;

            if (slotId.HasValue && slotId > 0)
            {
                var slot = await _context.ScheduleSlot.FindAsync(slotId.Value);
                if (slot == null)
                {
                    if (isAjax)
                    {
                        return NotFound(new { success = false, message = "Слот не знайдено." });
                    }

                    return NotFound();
                }

                slot.DayOfWeek = dayOfWeek;
                slot.LessonNumber = lessonNumber;
                slot.SubjectId = subjectId;
                slot.TeacherId = teacherId;
                slot.Room = room?.Trim();
                savedSlot = slot;
            }
            else
            {
                savedSlot = new ScheduleSlot
                {
                    AcademicYearId = yearId,
                    ClassId = classId,
                    DayOfWeek = dayOfWeek,
                    LessonNumber = lessonNumber,
                    SubjectId = subjectId,
                    TeacherId = teacherId,
                    Room = room?.Trim()
                };

                _context.ScheduleSlot.Add(savedSlot);
            }

            await _context.SaveChangesAsync();

            if (isAjax)
            {
                return Json(new
                {
                    success = true,
                    slotId = savedSlot!.Id,
                    dayOfWeek,
                    lessonNumber
                });
            }

            return RedirectToAction(nameof(Schedule), new { yearId, classId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSlot(int id, int yearId, int classId)
        {
            var isAjax = string.Equals(
                Request.Headers["X-Requested-With"],
                "XMLHttpRequest",
                StringComparison.OrdinalIgnoreCase);

            var slot = await _context.ScheduleSlot.FindAsync(id);
            if (slot != null)
            {
                _context.ScheduleSlot.Remove(slot);
                await _context.SaveChangesAsync();
            }

            if (isAjax)
            {
                return Json(new { success = true, deletedSlotId = id });
            }

            return RedirectToAction(nameof(Schedule), new { yearId, classId });
        }

        private async Task<IActionResult> GenerateAndPersistScheduleAsync(
            int yearId,
            int? classId,
            SchedulerMode mode,
            SchedulerConfigOptions options,
            bool mergeExisting,
            CancellationToken cancellationToken)
        {
            try
            {
                var schoolId = _tenantService.SchoolId;
                if (schoolId == null)
                {
                    return BadRequest("School is not selected.");
                }

                var result = await _schedulerService.GenerateScheduleAsync(schoolId.Value, yearId, mode, options, cancellationToken);
                if (!result.Success)
                {
                    TempData["Error"] = string.Join(" ", result.Warnings.Concat(result.Conflicts));
                    return RedirectToAction(nameof(Schedule), new { yearId, classId });
                }

                await ReplaceScheduleSlotsAsync(yearId, result.Slots, cancellationToken, mergeExisting);

                TempData["Success"] = $"Розклад згенеровано і збережено. Слотів: {result.Slots.Count}.";
                return RedirectToAction(nameof(Schedule), new { yearId, classId });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Scheduler API request failed for school {SchoolId}, year {YearId}", _tenantService.SchoolId, yearId);
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Schedule), new { yearId, classId });
            }
        }

        private async Task ReplaceScheduleSlotsAsync(
            int yearId,
            IEnumerable<ScheduleSlotDto> generatedSlots,
            CancellationToken cancellationToken,
            bool mergeExisting = false)
        {
            var schoolId = _tenantService.SchoolId;
            if (schoolId == null)
            {
                throw new InvalidOperationException("School is not selected.");
            }

            var slots = generatedSlots
                .Where(slot => slot.AcademicYearId == yearId && slot.SchoolId == schoolId.Value)
                .ToList();

            if (!mergeExisting)
            {
                var existingSlots = await _context.ScheduleSlot
                    .Where(slot => slot.SchoolId == schoolId.Value && slot.AcademicYearId == yearId)
                    .ToListAsync(cancellationToken);
                _context.ScheduleSlot.RemoveRange(existingSlots);
            }

            foreach (var slot in slots)
            {
                var existingSlot = mergeExisting
                    ? await _context.ScheduleSlot.FirstOrDefaultAsync(
                        item => item.SchoolId == schoolId.Value &&
                                item.AcademicYearId == yearId &&
                                item.ClassId == slot.ClassId &&
                                item.DayOfWeek == slot.DayOfWeek &&
                                item.LessonNumber == slot.LessonNumber,
                        cancellationToken)
                    : null;

                if (existingSlot != null)
                {
                    existingSlot.SubjectId = slot.SubjectId;
                    existingSlot.TeacherId = slot.TeacherId;
                    existingSlot.Room = slot.Room;
                    continue;
                }

                _context.ScheduleSlot.Add(new ScheduleSlot
                {
                    SchoolId = schoolId.Value,
                    AcademicYearId = yearId,
                    ClassId = slot.ClassId,
                    SubjectId = slot.SubjectId,
                    TeacherId = slot.TeacherId,
                    DayOfWeek = slot.DayOfWeek,
                    LessonNumber = slot.LessonNumber,
                    Room = slot.Room
                });
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        private Task ReplaceScheduleSlotsAsync(
            int yearId,
            IEnumerable<ScheduleSlot> generatedSlots,
            CancellationToken cancellationToken,
            bool mergeExisting = false)
        {
            var slotDtos = generatedSlots.Select(slot => new ScheduleSlotDto
            {
                SchoolId = slot.SchoolId,
                AcademicYearId = slot.AcademicYearId,
                ClassId = slot.ClassId,
                SubjectId = slot.SubjectId,
                TeacherId = slot.TeacherId,
                DayOfWeek = slot.DayOfWeek,
                LessonNumber = slot.LessonNumber,
                Room = slot.Room
            });

            return ReplaceScheduleSlotsAsync(yearId, slotDtos, cancellationToken, mergeExisting);
        }

        // ───────── Invitations ─────────
        public IActionResult InviteTeacher()
        {
            var invitations = _context.Invitation.ToList();
            ViewData["Invitations"] = invitations;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InviteTeacher(InviteTeacherViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewData["Invitations"] = _context.Invitation.ToList();
                return View(model);
            }

            var token = Guid.NewGuid().ToString("N");
            var invitation = new Invitation
            {
                SchoolId = _tenantService.SchoolId!.Value,
                Email = model.Email,
                Token = token,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                IsUsed = false
            };

            _context.Invitation.Add(invitation);
            await _context.SaveChangesAsync();

            var link = Url.Action("RegisterTeacher", "Account", new { token }, Request.Scheme);
            ViewData["InvitationLink"] = link;
            ViewData["Invitations"] = _context.Invitation.ToList();

            return View(new InviteTeacherViewModel());
        }

        // ───────── Helpers ─────────
        private Dictionary<int, List<string>> DetectConflicts(List<ScheduleSlot> allSlots)
        {
            var conflicts = new Dictionary<int, List<string>>();

            var grouped = allSlots.GroupBy(s => new { s.DayOfWeek, s.LessonNumber });

            foreach (var group in grouped)
            {
                var items = group.ToList();
                if (items.Count < 2) continue;

                // Teacher conflicts
                var teacherDups = items.GroupBy(s => s.TeacherId).Where(g => g.Count() > 1);
                foreach (var dup in teacherDups)
                {
                    foreach (var slot in dup)
                    {
                        if (!conflicts.ContainsKey(slot.Id)) conflicts[slot.Id] = new List<string>();
                        conflicts[slot.Id].Add($"Вчитель {slot.Teacher?.Surname} має інший урок у цей час");
                    }
                }

                // Room conflicts
                var roomDups = items.Where(s => !string.IsNullOrWhiteSpace(s.Room))
                    .GroupBy(s => s.Room).Where(g => g.Count() > 1);
                foreach (var dup in roomDups)
                {
                    foreach (var slot in dup)
                    {
                        if (!conflicts.ContainsKey(slot.Id)) conflicts[slot.Id] = new List<string>();
                        conflicts[slot.Id].Add($"Кабінет \"{slot.Room}\" зайнятий іншим класом");
                    }
                }

                // Class conflicts (same class two slots at same time)
                var classDups = items.GroupBy(s => s.ClassId).Where(g => g.Count() > 1);
                foreach (var dup in classDups)
                {
                    foreach (var slot in dup)
                    {
                        if (!conflicts.ContainsKey(slot.Id)) conflicts[slot.Id] = new List<string>();
                        conflicts[slot.Id].Add($"Клас {slot.Class?.Name} має два уроки одночасно");
                    }
                }
            }

            return conflicts;
        }
    }
}
