using EduLog.Data;
using EduLog.Models;
using EduLog.Models.Admin;
using EduLog.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EduLog.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly EduLogContext _context;
        private readonly ITenantService _tenantService;
        private readonly ISchedulerService _schedulerService;
        private readonly IEmailService _emailService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<AdminController> _logger;
        private readonly SchedulerApiOptions _schedulerApiOptions;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        private sealed class ScheduleExportBundle
        {
            public int SchoolId { get; set; }
            public DateTime GeneratedAt { get; set; }
            public List<ScheduleExportYear> Years { get; set; } = new();
            public List<ScheduleSlotDto> Slots { get; set; } = new();
        }

        private sealed class ScheduleExportYear
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public bool IsArchived { get; set; }
        }

        public AdminController(
            EduLogContext context,
            ITenantService tenantService,
            ISchedulerService schedulerService,
            IEmailService emailService,
            UserManager<ApplicationUser> userManager,
            ILogger<AdminController> logger,
            IOptions<SchedulerApiOptions> schedulerApiOptions,
            IServiceScopeFactory serviceScopeFactory)
        {
            _context = context;
            _tenantService = tenantService;
            _schedulerService = schedulerService;
            _emailService = emailService;
            _userManager = userManager;
            _logger = logger;
            _schedulerApiOptions = schedulerApiOptions.Value;
            _serviceScopeFactory = serviceScopeFactory;
        }

        // ───────── Dashboard ─────────
        public async Task<IActionResult> Index(DateTime? month)
        {
            var now = (month ?? DateTime.Today).Date;
            var monthStart = new DateTime(now.Year, now.Month, 1);
            var monthEnd = monthStart.AddMonths(1);

            ViewData["ClassCount"] = await _context.Class.CountAsync();
            ViewData["TeacherCount"] = await _context.Teacher.CountAsync();
            ViewData["StudentCount"] = await _context.Student.CountAsync();
            ViewData["SubjectCount"] = await _context.Subject.CountAsync();
            ViewData["Events"] = await _context.SchoolEvent
                .Where(e => e.Date >= monthStart && e.Date < monthEnd)
                .OrderBy(e => e.Date)
                .ThenBy(e => e.Title)
                .ToListAsync();
            ViewData["CurrentMonth"] = monthStart;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetEvents(DateTime month)
        {
            var monthStart = new DateTime(month.Year, month.Month, 1);
            var monthEnd = monthStart.AddMonths(1);

            var events = await _context.SchoolEvent
                .Where(e => e.Date >= monthStart && e.Date < monthEnd)
                .OrderBy(e => e.Date)
                .ThenBy(e => e.Title)
                .Select(e => new
                {
                    id = e.Id,
                    title = e.Title,
                    date = e.Date.ToString("yyyy-MM-dd"),
                    color = e.Color
                })
                .ToListAsync();

            return Json(new
            {
                success = true,
                month = monthStart.ToString("yyyy-MM-dd"),
                events
            });
        }

        [HttpPost]
        public async Task<IActionResult> CreateEvent(string title, DateTime date, string? color)
        {
            if (_tenantService.SchoolId == null)
                return BadRequest(new { success = false, message = "School is not selected." });

            if (string.IsNullOrWhiteSpace(title))
                return BadRequest(new { success = false, message = "Назва події обов'язкова." });

            var schoolEvent = new SchoolEvent
            {
                Title = title.Trim(),
                Date = date.Date,
                Color = NormalizeHexColor(color)
            };

            _context.SchoolEvent.Add(schoolEvent);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                id = schoolEvent.Id,
                title = schoolEvent.Title,
                date = schoolEvent.Date.ToString("yyyy-MM-dd"),
                color = schoolEvent.Color
            });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateEvent(int id, string title, DateTime date, string? color)
        {
            if (string.IsNullOrWhiteSpace(title))
                return BadRequest(new { success = false, message = "Назва події обов'язкова." });

            var schoolEvent = await _context.SchoolEvent.FirstOrDefaultAsync(e => e.Id == id);
            if (schoolEvent == null)
                return NotFound(new { success = false, message = "Подію не знайдено." });

            schoolEvent.Title = title.Trim();
            schoolEvent.Date = date.Date;
            schoolEvent.Color = NormalizeHexColor(color);

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                id = schoolEvent.Id,
                title = schoolEvent.Title,
                date = schoolEvent.Date.ToString("yyyy-MM-dd"),
                color = schoolEvent.Color
            });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteEvent(int id)
        {
            var schoolEvent = await _context.SchoolEvent.FirstOrDefaultAsync(e => e.Id == id);
            if (schoolEvent == null)
                return NotFound(new { success = false, message = "Подію не знайдено." });

            _context.SchoolEvent.Remove(schoolEvent);
            await _context.SaveChangesAsync();

            return Json(new { success = true, id });
        }

        // ───────── Classes ─────────
        public IActionResult Classes()
        {
            var classes = _context.Class
                .Include(c => c.Room)
                .Include(c => c.ClassSubjects)
                .ToList();
            var teachers = _context.Teacher.ToList();
            var templates = _context.ClassTemplate
                .Include(t => t.TemplateSubjects)
                .ThenInclude(ts => ts.Subject)
                .OrderBy(t => t.Name)
                .ToList();
            ViewData["Teachers"] = teachers;
            ViewData["Templates"] = templates;
            ViewData["Subjects"] = _context.Subject.OrderBy(s => s.Name).ToList();
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

            var normalizedName = name.Trim();
            var exists = await _context.Class.AnyAsync(c => c.Name == normalizedName);
            if (exists)
            {
                ModelState.AddModelError("", "Клас із такою назвою вже існує");
                ViewData["Teachers"] = new SelectList(_context.Teacher.ToList(), "Id", "Surname");
                return View();
            }

            var cls = new Class { Name = normalizedName, TeacherId = teacherId };
            _context.Class.Add(cls);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Клас {normalizedName} створено.";
            return RedirectToAction(nameof(Classes));
        }

        public IActionResult EditClass(int id)
        {
            var cls = _context.Class
                .Include(c => c.Room)
                .Include(c => c.ClassSubjects).ThenInclude(cs => cs.Subject)
                .FirstOrDefault(c => c.Id == id);
            if (cls == null) return NotFound();

            var occupiedRoomIds = _context.Class
                .Where(c => c.Id != id && c.RoomId.HasValue)
                .Select(c => c.RoomId!.Value)
                .Distinct()
                .ToHashSet();

            var availableRooms = _context.Room
                .OrderBy(r => r.Number)
                .AsEnumerable()
                .Where(r => !occupiedRoomIds.Contains(r.Id) || r.Id == cls.RoomId)
                .ToList();

            ViewData["Teachers"] = new SelectList(_context.Teacher.ToList(), "Id", "Surname", cls.TeacherId);
            ViewData["Rooms"] = new SelectList(availableRooms, "Id", "Number", cls.RoomId);
            ViewData["AllSubjects"] = _context.Subject.ToList();
            return View(cls);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateClass(int id, string name, int? teacherId, int? roomId)
        {
            var cls = await _context.Class.FindAsync(id);
            if (cls == null) return NotFound();

            if (roomId.HasValue)
            {
                var roomTaken = await _context.Class.AnyAsync(c => c.Id != id
                    && c.SchoolId == cls.SchoolId
                    && c.RoomId == roomId.Value);
                if (roomTaken)
                {
                    TempData["Error"] = "Цей кабінет уже призначено іншому класу.";
                    return RedirectToAction(nameof(EditClass), new { id });
                }
            }

            cls.Name = name?.Trim() ?? cls.Name;
            cls.TeacherId = teacherId;
            cls.RoomId = roomId;
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(EditClass), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkCreatePrimarySchool(int parallelsPerGrade = 3)
        {
            var parallelLetters = new[] { "А", "Б", "В", "Г", "Д" };
            var parallelCount = Math.Clamp(parallelsPerGrade, 2, parallelLetters.Length);
            var teachers = await _context.Teacher
                .OrderBy(t => t.Surname)
                .ThenBy(t => t.Name)
                .ToListAsync();

            if (!teachers.Any())
            {
                TempData["Error"] = "Немає вчителів для призначення класних керівників.";
                return RedirectToAction(nameof(Classes));
            }

            var existingSet = (await _context.Class.Select(c => c.Name).ToListAsync())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var classesToCreate = new List<Class>();
            var teacherIndex = 0;

            for (var grade = 1; grade <= 4; grade++)
            {
                for (var i = 0; i < parallelCount; i++)
                {
                    var className = $"{grade}-{parallelLetters[i]}";
                    if (existingSet.Contains(className))
                        continue;

                    classesToCreate.Add(new Class
                    {
                        Name = className,
                        TeacherId = teachers[teacherIndex % teachers.Count].Id
                    });
                    teacherIndex++;
                }
            }

            if (!classesToCreate.Any())
            {
                TempData["Success"] = "Класи початкової школи вже існують.";
                return RedirectToAction(nameof(Classes));
            }

            _context.Class.AddRange(classesToCreate);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Створено {classesToCreate.Count} класів початкової школи.";
            return RedirectToAction(nameof(Classes));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SeedStudentsForAllClasses()
        {
            var surnames = new[]
            {
                "Бондар", "Бондаренко", "Василенко", "Вовк", "Гаврилюк", "Гнатюк", "Демчук", "Дорошенко", "Іваненко", "Коваленко",
                "Ковальчук", "Козак", "Кравець", "Кравчук", "Лисенко", "Мазур", "Мельник", "Мороз", "Олійник", "Павленко",
                "Петренко", "Поліщук", "Романюк", "Савчук", "Сидоренко", "Степаненко", "Ткаченко", "Федоренко", "Шевченко", "Яременко"
            };

            var names = new[]
            {
                "Андрій", "Богдан", "Владислав", "Гліб", "Данило", "Дмитро", "Єгор", "Захар", "Іван", "Кирило",
                "Максим", "Микита", "Назар", "Олег", "Павло", "Роман", "Сергій", "Тимофій", "Юрій", "Ярослав",
                "Анастасія", "Валерія", "Вероніка", "Дарина", "Діана", "Єва", "Злата", "Ірина", "Катерина", "Марія",
                "Наталія", "Оксана", "Поліна", "Софія", "Тетяна", "Уляна", "Христина", "Юлія", "Яна", "Олена"
            };

            var patronymics = new[]
            {
                "Андрійович", "Богданович", "Вікторович", "Володимирович", "Ігорович", "Іванович", "Максимович", "Миколайович", "Олександрович", "Олегович",
                "Павлович", "Петрович", "Сергійович", "Юрійович", "Ярославович", "Андріївна", "Богданівна", "Вікторівна", "Володимирівна", "Іванівна",
                "Ігорівна", "Максимівна", "Миколаївна", "Олександрівна", "Олегівна", "Павлівна", "Петрівна", "Сергіївна", "Юріївна", "Ярославівна"
            };

            var classes = await _context.Class.OrderBy(c => c.Name).ToListAsync();
            if (!classes.Any())
            {
                TempData["Error"] = "Спочатку створіть Klaси.";
                return RedirectToAction(nameof(Classes));
            }

            var existingCounts = await _context.Student
                .GroupBy(s => s.ClassId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

            var existingNamesByClass = await _context.Student
                .GroupBy(s => s.ClassId)
                .Select(g => new
                {
                    ClassId = g.Key,
                    FullNames = g.Select(s => (s.Surname + " " + s.Name + " " + s.Patronymic).Trim()).ToList()
                })
                .ToDictionaryAsync(
                    x => x.ClassId,
                    x => new HashSet<string>(x.FullNames, StringComparer.OrdinalIgnoreCase));

            var random = Random.Shared;
            var studentsToAdd = new List<Student>();

            foreach (var cls in classes)
            {
                var currentCount = existingCounts.TryGetValue(cls.Id, out var count) ? count : 0;
                var targetCount = random.Next(25, 31);
                var toCreate = Math.Max(0, targetCount - currentCount);
                if (toCreate == 0)
                    continue;

                if (!existingNamesByClass.TryGetValue(cls.Id, out var fullNames))
                {
                    fullNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    existingNamesByClass[cls.Id] = fullNames;
                }

                var attempts = 0;
                while (toCreate > 0 && attempts < 500)
                {
                    attempts++;
                    var surname = surnames[random.Next(surnames.Length)];
                    var nameToUse = names[random.Next(names.Length)];
                    var patronymic = patronymics[random.Next(patronymics.Length)];
                    var fullName = $"{surname} {nameToUse} {patronymic}";

                    if (!fullNames.Add(fullName))
                        continue;

                    studentsToAdd.Add(new Student
                    {
                        ClassId = cls.Id,
                        Surname = surname,
                        Name = nameToUse,
                        Patronymic = patronymic
                    });
                    toCreate--;
                }
            }

            if (!studentsToAdd.Any())
            {
                TempData["Success"] = "У всіх класах вже достатньо учнів (25+).";
                return RedirectToAction(nameof(Classes));
            }

            _context.Student.AddRange(studentsToAdd);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Додано {studentsToAdd.Count} учнів у наявні класи.";
            return RedirectToAction(nameof(Classes));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTemplate(string name, int[] subjectIds)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Введіть назву шаблону";
                return RedirectToAction(nameof(Classes));
            }

            if (subjectIds == null || subjectIds.Length == 0)
            {
                TempData["Error"] = "Оберіть хоча б один предмет";
                return RedirectToAction(nameof(Classes));
            }

            var validSubjectIds = await _context.Subject
                .Where(s => subjectIds.Contains(s.Id))
                .Select(s => s.Id)
                .Distinct()
                .ToArrayAsync();

            if (validSubjectIds.Length == 0)
            {
                TempData["Error"] = "Оберіть коректні предмети";
                return RedirectToAction(nameof(Classes));
            }

            var template = new ClassTemplate
            {
                Name = name.Trim()
            };

            _context.ClassTemplate.Add(template);
            await _context.SaveChangesAsync();

            foreach (var subjectId in validSubjectIds)
            {
                _context.TemplateSubject.Add(new TemplateSubject
                {
                    TemplateId = template.Id,
                    SubjectId = subjectId
                });
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Classes));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTemplate(int id)
        {
            var template = await _context.ClassTemplate.FindAsync(id);
            if (template == null) return NotFound();

            _context.ClassTemplate.Remove(template);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Classes));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApplyTemplate(int templateId, int[] classIds)
        {
            var template = await _context.ClassTemplate
                .Include(t => t.TemplateSubjects)
                .FirstOrDefaultAsync(t => t.Id == templateId);

            if (template == null)
                return NotFound();

            if (classIds == null || classIds.Length == 0)
            {
                TempData["Error"] = "Оберіть хоча б один клас";
                return RedirectToAction(nameof(Classes));
            }

            var subjectIds = template.TemplateSubjects.Select(ts => ts.SubjectId).Distinct().ToArray();
            if (subjectIds.Length == 0)
                return RedirectToAction(nameof(Classes));

            var classIdSet = await _context.Class
                .Where(c => classIds.Contains(c.Id))
                .Select(c => c.Id)
                .Distinct()
                .ToArrayAsync();

            if (classIdSet.Length == 0)
            {
                TempData["Error"] = "Оберіть хоча б один клас";
                return RedirectToAction(nameof(Classes));
            }

            var existing = await _context.ClassSubject
                .Where(cs => classIdSet.Contains(cs.ClassId) && subjectIds.Contains(cs.SubjectId))
                .Select(cs => new { cs.ClassId, cs.SubjectId })
                .ToListAsync();

            var existingPairs = existing
                .Select(x => $"{x.ClassId}:{x.SubjectId}")
                .ToHashSet();

            foreach (var classId in classIdSet)
            {
                foreach (var subjectId in subjectIds)
                {
                    var key = $"{classId}:{subjectId}";
                    if (existingPairs.Contains(key))
                        continue;

                    _context.ClassSubject.Add(new ClassSubject
                    {
                        ClassId = classId,
                        SubjectId = subjectId
                    });
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Classes));
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
            var studentIds = students.Select(s => s.Id).ToList();

            var pendingInvitations = _context.Invitation
                .Where(i => i.Role == "Student"
                    && i.StudentId != null
                    && studentIds.Contains(i.StudentId.Value)
                    && !i.IsUsed
                    && i.ExpiresAt > DateTime.UtcNow)
                .ToDictionary(i => i.StudentId!.Value, i => i);

            ViewData["Class"] = cls;
            ViewData["PendingInvitations"] = pendingInvitations;
            return View(students);
        }

        // ───────── Student account invitations ─────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InviteStudent(int studentId, string? email, CancellationToken cancellationToken)
        {
            var student = await _context.Student.FirstOrDefaultAsync(s => s.Id == studentId);
            if (student == null)
            {
                TempData["Error"] = "Учня не знайдено.";
                return RedirectToAction(nameof(Classes));
            }

            if (student.ApplicationUserId != null)
            {
                TempData["Error"] = "У цього учня вже є акаунт.";
                return RedirectToAction(nameof(ClassStudents), new { id = student.ClassId });
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                TempData["Error"] = "Email обов'язковий для запрошення.";
                return RedirectToAction(nameof(ClassStudents), new { id = student.ClassId });
            }

            // Cancel any previous pending invitation for this student
            var oldInvitations = _context.Invitation
                .Where(i => i.StudentId == studentId && !i.IsUsed)
                .ToList();
            foreach (var oi in oldInvitations) oi.IsUsed = true;

            var token = Guid.NewGuid().ToString("N");
            var invitation = new Invitation
            {
                SchoolId = _tenantService.SchoolId!.Value,
                Email = email.Trim(),
                Token = token,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(14),
                IsUsed = false,
                Role = "Student",
                StudentId = studentId
            };
            _context.Invitation.Add(invitation);
            await _context.SaveChangesAsync(cancellationToken);

            var link = Url.Action("RegisterStudent", "Account", new { token }, Request.Scheme);
            if (!string.IsNullOrWhiteSpace(link))
            {
                try
                {
                    await _emailService.SendInvitationAsync(email.Trim(), link, cancellationToken);
                    TempData["InvitationSentTo"] = email;
                }
                catch (ApplicationException ex)
                {
                    TempData["Error"] = $"Запрошення створено, але email не надіслано: {ex.Message}";
                }
                TempData["InvitationLink"] = link;
            }

            return RedirectToAction(nameof(ClassStudents), new { id = student.ClassId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RevokeStudentInvitation(int invitationId, CancellationToken cancellationToken)
        {
            var invitation = await _context.Invitation.FirstOrDefaultAsync(i => i.Id == invitationId);
            if (invitation == null)
            {
                return NotFound();
            }

            int? classId = null;
            if (invitation.StudentId != null)
            {
                classId = (await _context.Student.FirstOrDefaultAsync(s => s.Id == invitation.StudentId))?.ClassId;
            }

            invitation.IsUsed = true;
            await _context.SaveChangesAsync(cancellationToken);

            return classId.HasValue
                ? RedirectToAction(nameof(ClassStudents), new { id = classId.Value })
                : RedirectToAction(nameof(Classes));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnlinkStudentAccount(int studentId, CancellationToken cancellationToken)
        {
            var student = await _context.Student.FirstOrDefaultAsync(s => s.Id == studentId);
            if (student == null) return NotFound();

            if (student.ApplicationUserId != null)
            {
                var user = await _userManager.FindByIdAsync(student.ApplicationUserId);
                if (user != null)
                {
                    await _userManager.DeleteAsync(user);
                }
                student.ApplicationUserId = null;
                await _context.SaveChangesAsync(cancellationToken);
            }

            return RedirectToAction(nameof(ClassStudents), new { id = student.ClassId });
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
            var teachers = await _context.Teacher
                .OrderBy(t => t.Surname)
                .ThenBy(t => t.Name)
                .ToListAsync();
            var users = await _userManager.Users
                .Where(u => u.SchoolId == _tenantService.SchoolId)
                .ToListAsync();
            var subjects = await _context.Subject
                .OrderBy(s => s.Name)
                .ToListAsync();

            ViewData["Users"] = users;
            ViewData["AllSubjects"] = subjects;
            return View(teachers);
        }

        [HttpGet]
        public async Task<IActionResult> TeacherDetails(int id)
        {
            var teacher = await _context.Teacher.FirstOrDefaultAsync(t => t.Id == id);
            if (teacher == null)
                return NotFound();

            var user = await _userManager.Users
                .Where(u => u.SchoolId == _tenantService.SchoolId && u.TeacherId == id)
                .FirstOrDefaultAsync();

            var subjects = await _context.Subject
                .Where(s => s.SubjectTeachers.Any(st => st.TeacherId == id))
                .OrderBy(s => s.Name)
                .Select(s => new { id = s.Id, name = s.Name })
                .ToListAsync();

            var classes = await _context.ScheduleSlot
                .Where(s => s.TeacherId == id)
                .Select(s => new { id = s.ClassId, name = s.Class.Name })
                .Distinct()
                .OrderBy(c => c.name)
                .ToListAsync();

            return Json(new
            {
                name = teacher.Name,
                surname = teacher.Surname,
                patronymic = teacher.Patronymic,
                email = user?.Email,
                phone = user?.PhoneNumber,
                subjects,
                classes
            });
        }

        [HttpPost]
        public async Task<IActionResult> EditTeacher(int id, string name, string surname, string? patronymic, string? phone)
        {
            var isAjax = string.Equals(
                Request.Headers["X-Requested-With"],
                "XMLHttpRequest",
                StringComparison.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(surname))
            {
                if (isAjax)
                    return BadRequest(new { success = false, message = "Ім'я та прізвище обов'язкові." });

                TempData["Error"] = "Ім'я та прізвище обов'язкові.";
                return RedirectToAction(nameof(Teachers));
            }

            var teacher = await _context.Teacher.FindAsync(id);
            if (teacher == null)
            {
                if (isAjax)
                    return NotFound(new { success = false, message = "Вчителя не знайдено." });

                return NotFound();
            }

            teacher.Name = name.Trim();
            teacher.Surname = surname.Trim();
            teacher.Patronymic = patronymic?.Trim() ?? string.Empty;

            var normalizedPhone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
            var user = await _userManager.Users
                .Where(u => u.SchoolId == _tenantService.SchoolId && u.TeacherId == id)
                .FirstOrDefaultAsync();

            await _context.SaveChangesAsync();

            if (user != null && user.PhoneNumber != normalizedPhone)
            {
                user.PhoneNumber = normalizedPhone;
                await _userManager.UpdateAsync(user);
            }

            if (isAjax)
            {
                return Json(new
                {
                    success = true,
                    id = teacher.Id,
                    name = teacher.Name,
                    surname = teacher.Surname,
                    patronymic = teacher.Patronymic,
                    email = user?.Email,
                    phone = user?.PhoneNumber
                });
            }

            return RedirectToAction(nameof(Teachers));
        }

        [HttpPost]
        public async Task<IActionResult> AssignSubjectToTeacher(int teacherId, int subjectId)
        {
            var teacherExists = await _context.Teacher.AnyAsync(t => t.Id == teacherId);
            if (!teacherExists)
                return NotFound(new { success = false, message = "Вчителя не знайдено." });

            var subject = await _context.Subject.FindAsync(subjectId);
            if (subject == null)
                return NotFound(new { success = false, message = "Предмет не знайдено." });

            var exists = await _context.SubjectTeacher
                .AnyAsync(st => st.SubjectId == subjectId && st.TeacherId == teacherId);
            if (!exists)
            {
                _context.SubjectTeacher.Add(new SubjectTeacher
                {
                    SubjectId = subjectId,
                    TeacherId = teacherId
                });
            }

            subject.TeacherId = teacherId;
            await _context.SaveChangesAsync();

            return Json(new { success = true, subjectId = subject.Id, subjectName = subject.Name });
        }

        [HttpPost]
        public async Task<IActionResult> UnassignSubjectFromTeacher(int teacherId, int subjectId)
        {
            var assignment = await _context.SubjectTeacher
                .FirstOrDefaultAsync(st => st.SubjectId == subjectId && st.TeacherId == teacherId);
            if (assignment == null)
                return NotFound(new { success = false, message = "Предмет не знайдено у цього вчителя." });

            var assignmentCount = await _context.SubjectTeacher
                .CountAsync(st => st.SubjectId == subjectId);
            if (assignmentCount <= 1)
                return BadRequest(new { success = false, message = "У предмета має залишитись хоча б один вчитель." });

            _context.SubjectTeacher.Remove(assignment);
            var subject = await _context.Subject.FindAsync(subjectId);
            if (subject != null && subject.TeacherId == teacherId)
            {
                var replacementTeacherId = await _context.SubjectTeacher
                    .Where(st => st.SubjectId == subjectId && st.TeacherId != teacherId)
                    .Select(st => st.TeacherId)
                    .FirstOrDefaultAsync();

                if (replacementTeacherId != 0)
                {
                    subject.TeacherId = replacementTeacherId;
                }
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        // ───────── Rooms ─────────
        public IActionResult Rooms()
        {
            var rooms = _context.Room.OrderBy(r => r.Number).ToList();
            return View(rooms);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRoom(string number, int? capacity)
        {
            if (string.IsNullOrWhiteSpace(number))
            {
                TempData["Error"] = "Введіть номер кабінету";
                return RedirectToAction(nameof(Rooms));
            }

            _context.Room.Add(new Room
            {
                Number = number.Trim(),
                Capacity = capacity
            });

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Rooms));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRoom(int id)
        {
            var room = await _context.Room.FindAsync(id);
            if (room == null) return NotFound();

            _context.Room.Remove(room);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Rooms));
        }

        // ───────── Subjects ─────────
        public IActionResult Subjects()
        {
            var subjects = _context.Subject
                .Include(s => s.Teacher)
                .Include(s => s.DefaultRoom)
                .Include(s => s.SubjectTeachers)
                    .ThenInclude(st => st.Teacher)
                .Include(s => s.ClassSubjects).ThenInclude(cs => cs.Class)
                .ToList();
            ViewData["Teachers"] = _context.Teacher.ToList();
            ViewData["Classes"] = _context.Class.OrderBy(c => c.Name).ToList();
            ViewData["Rooms"] = _context.Room.OrderBy(r => r.Number).ToList();
            return View(subjects);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSubject(string name, int teacherId, int[] classIds, int hoursPerWeek = 1, int? defaultRoomId = null, bool isRoomFixed = false)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Введіть назву предмета";
                return RedirectToAction(nameof(Subjects));
            }

            var normalizedHours = Math.Max(1, hoursPerWeek);
            var subject = new Subject
            {
                Name = name.Trim(),
                TeacherId = teacherId,
                HoursPerWeek = normalizedHours,
                DefaultRoomId = defaultRoomId,
                IsRoomFixed = isRoomFixed && defaultRoomId.HasValue
            };
            _context.Subject.Add(subject);
            await _context.SaveChangesAsync();

            _context.SubjectTeacher.Add(new SubjectTeacher
            {
                SubjectId = subject.Id,
                TeacherId = teacherId
            });
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
        public async Task<IActionResult> BulkCreateSubjects([FromBody] List<BulkSubjectItem> items)
        {
            if (items == null || !items.Any())
                return Json(new { success = false, error = "Список порожній" });

            int created = 0;
            foreach (var item in items)
            {
                if (string.IsNullOrWhiteSpace(item.Name) || item.TeacherId <= 0)
                    continue;

                var subject = new Subject
                {
                    Name = item.Name.Trim(),
                    TeacherId = item.TeacherId
                };

                _context.Subject.Add(subject);
                await _context.SaveChangesAsync();

                _context.SubjectTeacher.Add(new SubjectTeacher
                {
                    SubjectId = subject.Id,
                    TeacherId = item.TeacherId
                });

                created++;
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, created });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSubject(int id)
        {
            var subject = await _context.Subject.FindAsync(id);
            if (subject == null) return NotFound();

            var classBindings = _context.ClassSubject.Where(cs => cs.SubjectId == id);
            var teacherBindings = _context.SubjectTeacher.Where(st => st.SubjectId == id);
            var templateBindings = _context.TemplateSubject.Where(ts => ts.SubjectId == id);
            var scheduleSlots = _context.ScheduleSlot.Where(s => s.SubjectId == id);

            _context.ClassSubject.RemoveRange(classBindings);
            _context.SubjectTeacher.RemoveRange(teacherBindings);
            _context.TemplateSubject.RemoveRange(templateBindings);
            _context.ScheduleSlot.RemoveRange(scheduleSlots);
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
            var subject = await _context.Subject
                .Include(s => s.SubjectTeachers)
                .ThenInclude(st => st.Teacher)
                .FirstOrDefaultAsync(s => s.Id == request.SubjectId);
            if (subject == null) return NotFound();

            var existing = subject.SubjectTeachers.ToList();
            if (existing.Any())
            {
                _context.SubjectTeacher.RemoveRange(existing);
            }

            _context.SubjectTeacher.Add(new SubjectTeacher
            {
                SubjectId = subject.Id,
                TeacherId = request.TeacherId
            });

            subject.TeacherId = request.TeacherId;
            await _context.SaveChangesAsync();

            var teacher = await _context.Teacher.FindAsync(request.TeacherId);
            return Json(new { success = true, teacherName = $"{teacher?.Surname} {teacher?.Name}" });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateSubject(int id, string? name, int? hoursPerWeek, int? defaultRoomId, bool? isRoomFixed)
        {
            var subject = await _context.Subject.FindAsync(id);
            if (subject == null)
                return NotFound(new { success = false, message = "Предмет не знайдено." });

            if (!string.IsNullOrWhiteSpace(name))
            {
                subject.Name = name.Trim();
            }

            if (hoursPerWeek.HasValue)
            {
                subject.HoursPerWeek = Math.Max(1, hoursPerWeek.Value);
            }

            subject.DefaultRoomId = defaultRoomId;
            if (isRoomFixed.HasValue)
            {
                subject.IsRoomFixed = isRoomFixed.Value;
            }

            if (!subject.DefaultRoomId.HasValue)
            {
                subject.IsRoomFixed = false;
            }

            await _context.SaveChangesAsync();

            var roomNumber = subject.DefaultRoomId.HasValue
                ? await _context.Room.Where(r => r.Id == subject.DefaultRoomId.Value).Select(r => r.Number).FirstOrDefaultAsync()
                : null;

            return Json(new
            {
                success = true,
                id = subject.Id,
                name = subject.Name,
                hoursPerWeek = subject.HoursPerWeek,
                defaultRoomId = subject.DefaultRoomId,
                defaultRoomNumber = roomNumber,
                isRoomFixed = subject.IsRoomFixed
            });
        }

        [HttpPost]
        public async Task<IActionResult> AddTeacherToSubject([FromBody] SubjectTeacherRequest request)
        {
            var subject = await _context.Subject
                .Include(s => s.SubjectTeachers)
                .ThenInclude(st => st.Teacher)
                .FirstOrDefaultAsync(s => s.Id == request.SubjectId);
            if (subject == null)
                return NotFound(new { success = false, message = "Предмет не знайдено." });

            var teacher = await _context.Teacher.FindAsync(request.TeacherId);
            if (teacher == null)
                return NotFound(new { success = false, message = "Вчителя не знайдено." });

            var exists = subject.SubjectTeachers.Any(st => st.TeacherId == request.TeacherId);
            if (!exists)
            {
                _context.SubjectTeacher.Add(new SubjectTeacher
                {
                    SubjectId = request.SubjectId,
                    TeacherId = request.TeacherId
                });

                if (subject.TeacherId == 0)
                {
                    subject.TeacherId = request.TeacherId;
                }

                await _context.SaveChangesAsync();
            }

            return Json(new
            {
                success = true,
                teacherId = teacher.Id,
                teacherName = $"{teacher.Surname} {teacher.Name}".Trim()
            });
        }

        [HttpPost]
        public async Task<IActionResult> RemoveTeacherFromSubject([FromBody] SubjectTeacherRequest request)
        {
            var assignment = await _context.SubjectTeacher
                .FirstOrDefaultAsync(st => st.SubjectId == request.SubjectId && st.TeacherId == request.TeacherId);
            if (assignment == null)
                return NotFound(new { success = false, message = "Прив'язку не знайдено." });

            var count = await _context.SubjectTeacher.CountAsync(st => st.SubjectId == request.SubjectId);
            if (count <= 1)
                return BadRequest(new { success = false, message = "У предмета має залишитись хоча б один вчитель." });

            _context.SubjectTeacher.Remove(assignment);

            var subject = await _context.Subject.FindAsync(request.SubjectId);
            if (subject != null && subject.TeacherId == request.TeacherId)
            {
                var replacementTeacherId = await _context.SubjectTeacher
                    .Where(st => st.SubjectId == request.SubjectId && st.TeacherId != request.TeacherId)
                    .Select(st => st.TeacherId)
                    .FirstOrDefaultAsync();

                if (replacementTeacherId != 0)
                {
                    subject.TeacherId = replacementTeacherId;
                }
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true });
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
            var schoolId = _tenantService.SchoolId;
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
            var classOverview = new List<ScheduleSubjectOverviewItem>();

            if (selectedYear != null)
            {
                allSlots = _context.ScheduleSlot
                    .Include(s => s.Class)
                    .Include(s => s.Subject)
                    .Include(s => s.Teacher)
                    .Where(s => s.AcademicYearId == selectedYear.Id
                        && (!schoolId.HasValue || s.SchoolId == schoolId.Value || s.SchoolId == 0))
                    .ToList();

                if (selectedClass != null)
                {
                    slots = allSlots.Where(s => s.ClassId == selectedClass.Id).ToList();

                    var classSubjects = await _context.ClassSubject
                        .Where(cs => cs.ClassId == selectedClass.Id)
                        .Select(cs => cs.Subject)
                        .OrderBy(s => s.Name)
                        .ToListAsync();

                    classOverview = classSubjects
                        .Select(subject =>
                        {
                            var assignedLessons = slots.Count(s => s.SubjectId == subject.Id);
                            return new ScheduleSubjectOverviewItem
                            {
                                SubjectId = subject.Id,
                                SubjectName = subject.Name,
                                RequiredLessons = Math.Max(0, subject.HoursPerWeek),
                                AssignedLessons = assignedLessons,
                                ColorHex = NormalizeHexColor(GenerateDeterministicHexColor(subject.Name))
                            };
                        })
                        .ToList();
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
            ViewData["ClassOverview"] = classOverview;
            ViewData["SchedulerModes"] = schedulerModes;
            ViewData["SchedulerConfig"] = new SchedulerConfigOptions();
            ViewData["SchedulerApiBaseUrl"] = _schedulerApiOptions.BaseUrl;

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
            if (string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
            {
                var schoolId = _tenantService.SchoolId;
                if (schoolId == null)
                {
                    return BadRequest(new { success = false, message = "School is not selected." });
                }

                try
                {
                    // Delete existing slots for the year before starting a new generation
                    var existingSlotsList = await _context.ScheduleSlot
                        .Where(s => s.AcademicYearId == yearId && (s.SchoolId == schoolId.Value || s.SchoolId == 0))
                        .ToListAsync(cancellationToken);
                    if (existingSlotsList.Any())
                    {
                        _context.ScheduleSlot.RemoveRange(existingSlotsList);
                        await _context.SaveChangesAsync(cancellationToken);
                    }

                     var session = await _schedulerService.StartGenerationAsync(schoolId.Value, yearId, mode, options, cancellationToken);

                     _ = Task.Run(async () =>
                     {
                         try
                         {
                             using var scope = _serviceScopeFactory.CreateScope();
                             var scopedSchedulerService = scope.ServiceProvider.GetRequiredService<ISchedulerService>();
                             var scopedContext = scope.ServiceProvider.GetRequiredService<EduLogContext>();

                             var result = await scopedSchedulerService.CompleteGenerationAsync(session, CancellationToken.None);
                             if (!result.Success)
                             {
                                 _logger.LogWarning(
                                     "Background AI schedule completion failed for generation {GenerationId}: {Warnings}",
                                     session.GenerationId,
                                     string.Join(" | ", result.Warnings));
                                 return;
                             }

                             await ReplaceScheduleSlotsAsync(scopedContext, schoolId.Value, yearId, result.Slots, CancellationToken.None, mergeExisting: false);

                             _logger.LogInformation(
                                 "Background AI schedule generation completed for generation {GenerationId}. Slots persisted: {Slots}",
                                 session.GenerationId,
                                 result.Slots.Count);
                         }
                         catch (Exception ex)
                         {
                             _logger.LogError(ex, "Background AI schedule generation failed for generation {GenerationId}", session.GenerationId);
                         }
                     });

                     return Json(new
                     {
                         success = true,
                         generationId = session.GenerationId,
                         iterations = session.Iterations,
                         message = "Генерацію запущено."
                     });
                 }
                 catch (HttpRequestException ex)
                 {
                     _logger.LogWarning(ex, "AI schedule generation start failed for school {SchoolId}, year {YearId}", schoolId, yearId);
                     return BadRequest(new { success = false, message = ex.Message });
                 }
             }

             return await GenerateAndPersistScheduleAsync(yearId, classId, mode, options, mergeExisting: false, cancellationToken);
         }

        [HttpGet]
        public async Task<IActionResult> ExportAllSchedules(CancellationToken cancellationToken = default)
        {
            var schoolId = _tenantService.SchoolId;
            if (schoolId == null)
            {
                return BadRequest("School is not selected.");
            }

            var years = await _context.AcademicYear
                .Where(y => y.SchoolId == schoolId.Value)
                .OrderBy(y => y.StartDate)
                .ToListAsync(cancellationToken);

            var yearIds = years.Select(y => y.Id).ToList();
            var slots = await _context.ScheduleSlot
                .Where(s => s.SchoolId == schoolId.Value && yearIds.Contains(s.AcademicYearId))
                .Select(s => new ScheduleSlotDto
                {
                    SchoolId = s.SchoolId,
                    AcademicYearId = s.AcademicYearId,
                    ClassId = s.ClassId,
                    SubjectId = s.SubjectId,
                    TeacherId = s.TeacherId,
                    DayOfWeek = s.DayOfWeek,
                    LessonNumber = s.LessonNumber,
                    Room = s.Room
                })
                .ToListAsync(cancellationToken);

            var payload = new ScheduleExportBundle
            {
                SchoolId = schoolId.Value,
                GeneratedAt = DateTime.UtcNow,
                Years = years.Select(y => new ScheduleExportYear
                {
                    Id = y.Id,
                    Name = y.Name,
                    StartDate = y.StartDate,
                    EndDate = y.EndDate,
                    IsArchived = y.IsArchived
                }).ToList(),
                Slots = slots
            };

            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                WriteIndented = true
            };
            var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, options);
            var fileName = $"schedules_all_{DateTime.UtcNow:yyyyMMdd_HHmm}.json";
            return File(bytes, "application/json", fileName);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportAllSchedules(IFormFile file, CancellationToken cancellationToken)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Оберіть файл для імпорту.";
                return RedirectToAction(nameof(Schedule));
            }

            var schoolId = _tenantService.SchoolId;
            if (schoolId == null)
            {
                return BadRequest("School is not selected.");
            }

            ScheduleExportBundle? payload;
            await using (var stream = file.OpenReadStream())
            {
                payload = await JsonSerializer.DeserializeAsync<ScheduleExportBundle>(stream, cancellationToken: cancellationToken);
            }

            if (payload == null)
            {
                TempData["Error"] = "Невірний файл імпорту.";
                return RedirectToAction(nameof(Schedule));
            }

            var newYearIds = new Dictionary<int, int>();
            var totalImportedSlots = 0;
            foreach (var year in payload.Years.OrderBy(y => y.StartDate))
            {
                var existingYear = await _context.AcademicYear
                    .FirstOrDefaultAsync(y => y.SchoolId == schoolId.Value && y.Name == year.Name, cancellationToken);

                if (existingYear == null)
                {
                    existingYear = new AcademicYear
                    {
                        SchoolId = schoolId.Value,
                        Name = year.Name,
                        StartDate = year.StartDate,
                        EndDate = year.EndDate,
                        IsArchived = year.IsArchived,
                        IsCurrent = false
                    };
                    _context.AcademicYear.Add(existingYear);
                    await _context.SaveChangesAsync(cancellationToken);
                }
                else
                {
                    existingYear.StartDate = year.StartDate;
                    existingYear.EndDate = year.EndDate;
                    existingYear.IsArchived = year.IsArchived;
                    await _context.SaveChangesAsync(cancellationToken);
                }

                newYearIds[year.Id] = existingYear.Id;

                var yearSlots = payload.Slots
                    .Where(s => s.AcademicYearId == year.Id)
                    .Select(s => new ScheduleSlot
                    {
                        SchoolId = schoolId.Value,
                        AcademicYearId = existingYear.Id,
                        ClassId = s.ClassId,
                        SubjectId = s.SubjectId,
                        TeacherId = s.TeacherId,
                        DayOfWeek = s.DayOfWeek,
                        LessonNumber = s.LessonNumber,
                        Room = s.Room
                    })
                    .ToList();

                await ReplaceScheduleSlotsAsync(existingYear.Id, yearSlots, cancellationToken, mergeExisting: false);
                totalImportedSlots += yearSlots.Count;
            }

            TempData["Success"] = $"Імпортовано {totalImportedSlots} слотів для {newYearIds.Count} років.";
            return RedirectToAction(nameof(Schedule));
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

            var schoolId = _tenantService.SchoolId;
            if (schoolId == null)
            {
                if (isAjax)
                {
                    return BadRequest(new { success = false, message = "School is not selected." });
                }

                TempData["Error"] = "School is not selected.";
                return RedirectToAction(nameof(Schedule), new { yearId, classId });
            }

            var subjectRoomMeta = await _context.Subject
                .Where(s => s.Id == subjectId)
                .Select(s => new
                {
                    s.IsRoomFixed,
                    DefaultRoomNumber = s.DefaultRoom != null ? s.DefaultRoom.Number : null
                })
                .FirstOrDefaultAsync();

            var classRoomNumber = await _context.Class
                .Where(c => c.Id == classId)
                .Select(c => c.Room != null ? c.Room.Number : null)
                .FirstOrDefaultAsync();

            var normalizedRoom = room?.Trim();
            if (subjectRoomMeta?.IsRoomFixed == true && !string.IsNullOrWhiteSpace(subjectRoomMeta.DefaultRoomNumber))
            {
                normalizedRoom = subjectRoomMeta.DefaultRoomNumber;
            }
            else if (string.IsNullOrWhiteSpace(normalizedRoom))
            {
                normalizedRoom = classRoomNumber;
            }

            // Check conflicts before saving
            var existingSlots = await _context.ScheduleSlot
                .Where(s => s.AcademicYearId == yearId
                    && s.DayOfWeek == dayOfWeek
                    && s.LessonNumber == lessonNumber
                    && (s.SchoolId == schoolId.Value || s.SchoolId == 0))
                .Where(s => slotId == null || s.Id != slotId)
                .ToListAsync();

            var conflictMessages = new List<string>();

            if (existingSlots.Any(s => s.TeacherId == teacherId))
                conflictMessages.Add("Цей вчитель вже має урок у цей час");

            if (!string.IsNullOrWhiteSpace(normalizedRoom) && existingSlots.Any(s => s.Room == normalizedRoom))
                conflictMessages.Add($"Кабінет \"{normalizedRoom}\" вже зайнятий у цей час");

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
                var slot = await _context.ScheduleSlot
                    .FirstOrDefaultAsync(s => s.Id == slotId.Value && (s.SchoolId == schoolId.Value || s.SchoolId == 0));
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
                slot.Room = normalizedRoom;
                slot.SchoolId = schoolId.Value;
                savedSlot = slot;
            }
            else
            {
                savedSlot = new ScheduleSlot
                {
                    SchoolId = schoolId.Value,
                    AcademicYearId = yearId,
                    ClassId = classId,
                    DayOfWeek = dayOfWeek,
                    LessonNumber = lessonNumber,
                    SubjectId = subjectId,
                    TeacherId = teacherId,
                    Room = normalizedRoom
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

            var schoolId = _tenantService.SchoolId;
            if (schoolId == null)
            {
                if (isAjax)
                {
                    return BadRequest(new { success = false, message = "School is not selected." });
                }

                TempData["Error"] = "School is not selected.";
                return RedirectToAction(nameof(Schedule), new { yearId, classId });
            }

            var slot = await _context.ScheduleSlot
                .FirstOrDefaultAsync(s => s.Id == id && (s.SchoolId == schoolId.Value || s.SchoolId == 0));
            if (slot != null)
            {
                slot.SchoolId = schoolId.Value;
                _context.ScheduleSlot.Remove(slot);
                await _context.SaveChangesAsync();
            }

            if (isAjax)
            {
                return Json(new { success = true, deletedSlotId = id });
            }

            return RedirectToAction(nameof(Schedule), new { yearId, classId });
        }

        [HttpGet]
        public async Task<IActionResult> GetTeachersForSubjectAndClass(int subjectId, int classId)
        {
            var classBound = await _context.ClassSubject
                .AnyAsync(cs => cs.ClassId == classId && cs.SubjectId == subjectId);
            if (!classBound)
                return Json(new { success = true, classBound = false, teachers = Array.Empty<object>() });

            var teachers = await _context.SubjectTeacher
                .Where(st => st.SubjectId == subjectId)
                .Include(st => st.Teacher)
                .Select(st => new
                {
                    id = st.TeacherId,
                    name = $"{st.Teacher.Surname} {st.Teacher.Name}".Trim()
                })
                .OrderBy(t => t.name)
                .ToListAsync();

            return Json(new { success = true, classBound = true, teachers });
        }

        [HttpGet]
        public async Task<IActionResult> GetSubjectRoomInfo(int subjectId, int classId)
        {
            var subject = await _context.Subject
                .Where(s => s.Id == subjectId)
                .Select(s => new
                {
                    s.IsRoomFixed,
                    DefaultRoomNumber = s.DefaultRoom != null ? s.DefaultRoom.Number : null
                })
                .FirstOrDefaultAsync();
            if (subject == null)
                return NotFound(new { success = false, message = "Предмет не знайдено." });

            var classRoom = await _context.Class
                .Where(c => c.Id == classId)
                .Select(c => c.Room != null ? c.Room.Number : null)
                .FirstOrDefaultAsync();

            var room = subject.IsRoomFixed
                ? subject.DefaultRoomNumber ?? string.Empty
                : classRoom ?? string.Empty;

            return Json(new
            {
                success = true,
                isFixed = subject.IsRoomFixed,
                room
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetConflicts(int yearId, int? classId)
        {
            var schoolId = _tenantService.SchoolId;
            if (schoolId == null)
                return BadRequest(new { success = false, message = "School is not selected." });

            var allSlots = await _context.ScheduleSlot
                .Include(s => s.Teacher)
                .Include(s => s.Class)
                .Where(s => s.AcademicYearId == yearId)
                .ToListAsync();

            var conflicts = DetectConflicts(allSlots);

            var classSlotIds = classId.HasValue
                ? allSlots.Where(s => s.ClassId == classId.Value).Select(s => s.Id).ToHashSet()
                : null;

            var totalCount = conflicts.Count;
            var items = conflicts
                .Select(kv =>
                {
                    var slot = allSlots.FirstOrDefault(s => s.Id == kv.Key);
                    return new
                    {
                        slotId = kv.Key,
                        dayOfWeek = slot?.DayOfWeek ?? 0,
                        lessonNumber = slot?.LessonNumber ?? 0,
                        className = slot?.Class?.Name ?? string.Empty,
                        messages = kv.Value,
                        belongsToClass = classSlotIds == null || classSlotIds.Contains(kv.Key)
                    };
                })
                .OrderBy(x => x.dayOfWeek)
                .ThenBy(x => x.lessonNumber)
                .ToList();

            var classConflictSlotIds = items
                .Where(i => i.belongsToClass)
                .Select(i => i.slotId)
                .ToList();

            return Json(new
            {
                success = true,
                count = totalCount,
                conflicts = items,
                classConflictSlotIds
            });
        }

        [HttpGet]
        public IActionResult GetTeacherSchedule(int yearId, int teacherId)
        {
            var schoolId = _tenantService.SchoolId;
            if (schoolId == null)
                return BadRequest(new { success = false, message = "School is not selected." });

            var slots = _context.ScheduleSlot
                .Include(s => s.Subject)
                .Include(s => s.Class)
                .Where(s => s.AcademicYearId == yearId
                    && s.TeacherId == teacherId
                    && (!schoolId.HasValue || s.SchoolId == schoolId.Value || s.SchoolId == 0))
                .OrderBy(s => s.DayOfWeek)
                .ThenBy(s => s.LessonNumber)
                .Select(s => new
                {
                    s.DayOfWeek,
                    s.LessonNumber,
                    subjectName  = s.Subject != null ? s.Subject.Name : string.Empty,
                    className    = s.Class   != null ? s.Class.Name   : string.Empty,
                    s.Room,
                    subjectColor = (string?)null   // color resolved client-side from overview cache
                })
                .ToList();

            return Json(slots);
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

                // If we're not merging, clear existing slots before generation as requested
                if (!mergeExisting)
                {
                    var existingSlots = await _context.ScheduleSlot
                        .Where(s => s.AcademicYearId == yearId && (s.SchoolId == schoolId.Value || s.SchoolId == 0))
                        .ToListAsync(cancellationToken);

                    if (existingSlots.Any())
                    {
                        _context.ScheduleSlot.RemoveRange(existingSlots);
                        await _context.SaveChangesAsync(cancellationToken);
                    }
                }

                _logger.LogInformation(
                    "AI schedule generation requested for school {SchoolId}, year {YearId}, mode {Mode}, iterations {Iterations}, learning rate {LearningRate}",
                    schoolId.Value,
                    yearId,
                    mode,
                    options.Iterations,
                    options.LearningRate);

                var session = await _schedulerService.StartGenerationAsync(schoolId.Value, yearId, mode, options, cancellationToken);
                var result = await _schedulerService.CompleteGenerationAsync(session, cancellationToken);
                if (!result.Success)
                {
                    _logger.LogWarning(
                        "AI schedule generation failed for school {SchoolId}, year {YearId}. Warnings: {Warnings}. Conflicts: {Conflicts}",
                        schoolId.Value,
                        yearId,
                        string.Join(" | ", result.Warnings),
                        string.Join(" | ", result.Conflicts));
                    TempData["Error"] = string.Join(" ", result.Warnings.Concat(result.Conflicts));
                    return RedirectToAction(nameof(Schedule), new { yearId, classId });
                }

                await ReplaceScheduleSlotsAsync(_context, schoolId.Value, yearId, result.Slots, cancellationToken, mergeExisting);

                _logger.LogInformation(
                    "AI schedule generation completed for school {SchoolId}, year {YearId}. Slots persisted: {Slots}",
                    schoolId.Value,
                    yearId,
                    result.Slots.Count);

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
            EduLogContext context,
            int schoolId,
            int yearId,
            IEnumerable<ScheduleSlotDto> generatedSlots,
            CancellationToken cancellationToken,
            bool mergeExisting = false)
        {
            var validSubjectIds = await context.Subject
                .Where(s => s.SchoolId == schoolId)
                .Select(s => s.Id)
                .ToListAsync(cancellationToken);

            var validTeacherIds = await context.Teacher
                .Where(t => t.SchoolId == schoolId)
                .Select(t => t.Id)
                .ToListAsync(cancellationToken);

            var validClassIds = await context.Class
                .Where(c => c.SchoolId == schoolId)
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);

            var subjectIdSet = validSubjectIds.ToHashSet();
            var teacherIdSet = validTeacherIds.ToHashSet();
            var classIdSet = validClassIds.ToHashSet();

            var slots = generatedSlots
                .Where(slot => slot.AcademicYearId == yearId && slot.SchoolId == schoolId)
                .GroupBy(slot => new { slot.ClassId, slot.DayOfWeek, slot.LessonNumber })
                .Select(group => group.Last())
                .ToList();

            var skippedSlots = 0;

            var fixedRooms = await context.Subject
                .Where(s => s.SchoolId == schoolId && s.IsRoomFixed && s.DefaultRoomId != null)
                .Select(s => new { s.Id, RoomNumber = s.DefaultRoom!.Number })
                .ToDictionaryAsync(s => s.Id, s => s.RoomNumber, cancellationToken);

            var classRooms = await context.Class
                .Where(c => c.SchoolId == schoolId)
                .Select(c => new { c.Id, RoomNumber = c.Room != null ? c.Room.Number : null })
                .ToDictionaryAsync(c => c.Id, c => c.RoomNumber, cancellationToken);

            if (!mergeExisting)
            {
                var existingSlots = await context.ScheduleSlot
                    .Where(slot => slot.SchoolId == schoolId && slot.AcademicYearId == yearId)
                    .ToListAsync(cancellationToken);
                context.ScheduleSlot.RemoveRange(existingSlots);
            }

            foreach (var slot in slots)
            {
                if (!subjectIdSet.Contains(slot.SubjectId) || !teacherIdSet.Contains(slot.TeacherId) || !classIdSet.Contains(slot.ClassId))
                {
                    skippedSlots++;
                    continue;
                }

                var fixedRoom = fixedRooms.TryGetValue(slot.SubjectId, out var configuredRoom) ? configuredRoom : null;
                var classRoom = classRooms.TryGetValue(slot.ClassId, out var homeRoom) ? homeRoom : null;
                var resolvedRoom = fixedRoom ?? classRoom ?? (!string.IsNullOrWhiteSpace(slot.Room) ? slot.Room : null);

                var existingSlot = mergeExisting
                    ? await context.ScheduleSlot.FirstOrDefaultAsync(
                        item => item.SchoolId == schoolId &&
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
                    existingSlot.Room = resolvedRoom;
                    continue;
                }

                context.ScheduleSlot.Add(new ScheduleSlot
                {
                    SchoolId = schoolId,
                    AcademicYearId = yearId,
                    ClassId = slot.ClassId,
                    SubjectId = slot.SubjectId,
                    TeacherId = slot.TeacherId,
                    DayOfWeek = slot.DayOfWeek,
                    LessonNumber = slot.LessonNumber,
                    Room = resolvedRoom
                });
            }

            if (skippedSlots > 0)
            {
                _logger.LogWarning("Skipped {SkippedSlots} generated slots due to missing subject/teacher/class mappings.", skippedSlots);
            }

            await context.SaveChangesAsync(cancellationToken);
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

            return ReplaceScheduleSlotsAsync(_context, _tenantService.SchoolId ?? 0, yearId, slotDtos, cancellationToken, mergeExisting);
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
        public async Task<IActionResult> InviteTeacher(InviteTeacherViewModel model, CancellationToken cancellationToken)
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
            await _context.SaveChangesAsync(cancellationToken);

            var link = Url.Action("RegisterTeacher", "Account", new { token }, Request.Scheme);
            if (string.IsNullOrWhiteSpace(link))
            {
                TempData["Error"] = "Не вдалося сформувати посилання для запрошення.";
                ViewData["Invitations"] = _context.Invitation.ToList();
                return View(new InviteTeacherViewModel());
            }

            try
            {
                await _emailService.SendInvitationAsync(model.Email, link, cancellationToken);
                TempData["InvitationSentTo"] = model.Email;
            }
            catch (ApplicationException ex)
            {
                TempData["Error"] = ex.Message;
            }

            TempData["InvitationLink"] = link;
            ViewData["Invitations"] = _context.Invitation.ToList();

            return View(new InviteTeacherViewModel());
        }

        // ───────── Bulk subject binding ─────────
        [HttpPost]
        public async Task<IActionResult> BindSubjects([FromBody] BindSubjectsRequest request)
        {
            if (request == null || request.SubjectIds == null || request.SubjectIds.Length == 0)
                return Json(new { success = false, message = "Оберіть хоча б один предмет." });
            if (request.TeacherId <= 0)
                return Json(new { success = false, message = "Оберіть вчителя." });
            if (request.ClassIds == null || request.ClassIds.Length == 0)
                return Json(new { success = false, message = "Оберіть хоча б один клас." });

            var hours = Math.Max(1, request.HoursPerWeek);
            var subjects = await _context.Subject
                .Where(s => request.SubjectIds.Contains(s.Id))
                .ToListAsync();
            var classes = await _context.Class
                .Where(c => request.ClassIds.Contains(c.Id))
                .Select(c => c.Id).ToListAsync();

            var addedTeacher = 0;
            var addedClass = 0;
            foreach (var subject in subjects)
            {
                subject.HoursPerWeek = hours;
                var hasTeacher = await _context.SubjectTeacher
                    .AnyAsync(st => st.SubjectId == subject.Id && st.TeacherId == request.TeacherId);
                if (!hasTeacher)
                {
                    _context.SubjectTeacher.Add(new SubjectTeacher
                    {
                        SubjectId = subject.Id,
                        TeacherId = request.TeacherId
                    });
                    addedTeacher++;
                }
                if (subject.TeacherId == 0) subject.TeacherId = request.TeacherId;

                foreach (var classId in classes)
                {
                    var hasClass = await _context.ClassSubject
                        .AnyAsync(cs => cs.SubjectId == subject.Id && cs.ClassId == classId);
                    if (!hasClass)
                    {
                        _context.ClassSubject.Add(new ClassSubject
                        {
                            SubjectId = subject.Id,
                            ClassId = classId
                        });
                        addedClass++;
                    }
                }
            }

            await _context.SaveChangesAsync();
            return Json(new
            {
                success = true,
                subjects = subjects.Count,
                teacherLinks = addedTeacher,
                classLinks = addedClass,
                hoursPerWeek = hours
            });
        }

        [HttpGet]
        public async Task<IActionResult> ExportSubjects()
        {
            var subjects = await _context.Subject
                .Include(s => s.SubjectTeachers).ThenInclude(st => st.Teacher)
                .Include(s => s.ClassSubjects).ThenInclude(cs => cs.Class)
                .OrderBy(s => s.Name)
                .ToListAsync();

            using var workbook = new ClosedXML.Excel.XLWorkbook();
            var sheet = workbook.Worksheets.Add("Прив'язки");
            sheet.Cell(1, 1).Value = "Предмет";
            sheet.Cell(1, 2).Value = "Вчителі";
            sheet.Cell(1, 3).Value = "Класи";
            sheet.Cell(1, 4).Value = "Год/тиж";
            sheet.Range(1, 1, 1, 4).Style.Font.Bold = true;

            var row = 2;
            foreach (var s in subjects)
            {
                sheet.Cell(row, 1).Value = s.Name;
                sheet.Cell(row, 2).Value = string.Join("; ", s.SubjectTeachers
                    .Select(st => $"{st.Teacher.Surname} {st.Teacher.Name}".Trim()));
                sheet.Cell(row, 3).Value = string.Join(", ", s.ClassSubjects
                    .Select(cs => cs.Class.Name)
                    .OrderBy(n => n));
                sheet.Cell(row, 4).Value = s.HoursPerWeek;
                row++;
            }
            sheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var fileName = $"subjects-{DateTime.Now:yyyyMMdd-HHmm}.xlsx";
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        // ───────── Teacher absences & substitutions ─────────
        [HttpGet]
        public async Task<IActionResult> GetTeacherAbsences(int teacherId)
        {
            var absences = await _context.TeacherAbsence
                .Where(a => a.TeacherId == teacherId)
                .OrderByDescending(a => a.StartDate)
                .Select(a => new
                {
                    id = a.Id,
                    type = a.Type.ToString(),
                    typeLabel = a.Type == TeacherAbsenceType.SickLeave ? "Лікарняний"
                              : a.Type == TeacherAbsenceType.Vacation ? "Відпустка"
                              : "Інше",
                    startDate = a.StartDate.ToString("yyyy-MM-dd"),
                    endDate = a.EndDate.ToString("yyyy-MM-dd"),
                    note = a.Note,
                    overrideCount = a.Overrides.Count(),
                    coveredCount = a.Overrides.Count(o => o.SubstituteTeacherId != null)
                })
                .ToListAsync();
            return Json(new { success = true, absences });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTeacherAbsence(int teacherId, TeacherAbsenceType type, DateTime startDate, DateTime endDate, string? note)
        {
            if (endDate < startDate)
                return BadRequest(new { success = false, message = "Дата завершення не може бути раніше початку." });

            var teacher = await _context.Teacher.FindAsync(teacherId);
            if (teacher == null)
                return NotFound(new { success = false, message = "Вчителя не знайдено." });

            var absence = new TeacherAbsence
            {
                TeacherId = teacherId,
                Type = type,
                StartDate = startDate.Date,
                EndDate = endDate.Date,
                Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
                CreatedAt = DateTime.UtcNow
            };
            _context.TeacherAbsence.Add(absence);
            await _context.SaveChangesAsync();

            var occurrences = await BuildAbsenceOccurrencesAsync(absence);
            return Json(new { success = true, absenceId = absence.Id, occurrences });
        }

        [HttpGet]
        public async Task<IActionResult> GetAbsenceOccurrences(int absenceId)
        {
            var absence = await _context.TeacherAbsence.FirstOrDefaultAsync(a => a.Id == absenceId);
            if (absence == null)
                return NotFound(new { success = false, message = "Запис не знайдено." });
            var occurrences = await BuildAbsenceOccurrencesAsync(absence);
            return Json(new { success = true, occurrences });
        }

        private async Task<List<object>> BuildAbsenceOccurrencesAsync(TeacherAbsence absence)
        {
            var slots = await _context.ScheduleSlot
                .Include(s => s.Subject)
                .Include(s => s.Class)
                .Where(s => s.TeacherId == absence.TeacherId)
                .ToListAsync();

            var existingOverrides = await _context.ScheduleSlotOverride
                .Where(o => o.Date >= absence.StartDate && o.Date <= absence.EndDate)
                .ToListAsync();

            var allTeachers = await _context.Teacher
                .OrderBy(t => t.Surname).ThenBy(t => t.Name)
                .Select(t => new { t.Id, t.Name, t.Surname, t.Patronymic })
                .ToListAsync();

            var allSlots = await _context.ScheduleSlot
                .Select(s => new { s.TeacherId, s.DayOfWeek, s.LessonNumber })
                .ToListAsync();

            string[] dayNames = { "Понеділок", "Вівторок", "Середа", "Четвер", "П'ятниця" };
            var result = new List<object>();

            for (var date = absence.StartDate.Date; date <= absence.EndDate.Date; date = date.AddDays(1))
            {
                var dow = (int)date.DayOfWeek;
                if (dow == 0 || dow > 5) continue; // skip Sun/Sat
                var isoDow = dow; // Mon=1..Fri=5

                var todaySlots = slots.Where(s => s.DayOfWeek == isoDow).ToList();
                foreach (var slot in todaySlots)
                {
                    var ovr = existingOverrides
                        .FirstOrDefault(o => o.ScheduleSlotId == slot.Id && o.Date.Date == date.Date);

                    var busyTeacherIds = allSlots
                        .Where(s => s.DayOfWeek == slot.DayOfWeek && s.LessonNumber == slot.LessonNumber)
                        .Select(s => s.TeacherId)
                        .ToHashSet();

                    var availableTeachers = allTeachers
                        .Where(t => t.Id != absence.TeacherId && !busyTeacherIds.Contains(t.Id))
                        .Select(t => new { id = t.Id, name = $"{t.Surname} {t.Name} {t.Patronymic}".Trim() })
                        .ToList();

                    result.Add(new
                    {
                        slotId = slot.Id,
                        date = date.ToString("yyyy-MM-dd"),
                        dayName = dayNames[isoDow - 1],
                        lessonNumber = slot.LessonNumber,
                        subjectName = slot.Subject.Name,
                        className = slot.Class.Name,
                        room = slot.Room,
                        currentSubstituteId = ovr?.SubstituteTeacherId,
                        overrideId = ovr?.Id,
                        availableTeachers
                    });
                }
            }

            return result;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetSlotSubstitution(int slotId, DateTime date, int? substituteTeacherId, int? absenceId)
        {
            var slot = await _context.ScheduleSlot.FindAsync(slotId);
            if (slot == null)
                return NotFound(new { success = false, message = "Слот не знайдено." });

            if (substituteTeacherId.HasValue)
            {
                var clash = await _context.ScheduleSlot.AnyAsync(s =>
                    s.TeacherId == substituteTeacherId.Value &&
                    s.DayOfWeek == slot.DayOfWeek &&
                    s.LessonNumber == slot.LessonNumber &&
                    s.Id != slotId);
                if (clash)
                    return BadRequest(new { success = false, message = "Обраний вчитель зайнятий у цей таймслот." });
            }

            var existing = await _context.ScheduleSlotOverride
                .FirstOrDefaultAsync(o => o.ScheduleSlotId == slotId && o.Date == date.Date);

            if (existing != null)
            {
                existing.SubstituteTeacherId = substituteTeacherId;
                existing.AbsenceId = absenceId;
            }
            else
            {
                _context.ScheduleSlotOverride.Add(new ScheduleSlotOverride
                {
                    ScheduleSlotId = slotId,
                    Date = date.Date,
                    SubstituteTeacherId = substituteTeacherId,
                    AbsenceId = absenceId,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTeacherAbsence(int id)
        {
            var absence = await _context.TeacherAbsence.FirstOrDefaultAsync(a => a.Id == id);
            if (absence == null)
                return NotFound(new { success = false, message = "Запис не знайдено." });

            var overrides = _context.ScheduleSlotOverride.Where(o => o.AbsenceId == id);
            _context.ScheduleSlotOverride.RemoveRange(overrides);
            _context.TeacherAbsence.Remove(absence);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
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

        private static string? NormalizeHexColor(string? color)
        {
            if (string.IsNullOrWhiteSpace(color))
                return null;

            var normalized = color.Trim();
            if (!normalized.StartsWith('#'))
            {
                normalized = $"#{normalized}";
            }

            return Regex.IsMatch(normalized, "^#[0-9A-Fa-f]{6}$")
                ? normalized
                : null;
        }

        private static string GenerateDeterministicHexColor(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "#6C757D";

            unchecked
            {
                var hash = 17;
                foreach (var ch in input.Trim())
                {
                    hash = hash * 31 + ch;
                }

                var r = 80 + Math.Abs(hash & 0x7F);
                var g = 80 + Math.Abs((hash >> 8) & 0x7F);
                var b = 80 + Math.Abs((hash >> 16) & 0x7F);

                return $"#{r:X2}{g:X2}{b:X2}";
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearSchedule(int yearId)
        {
            var schoolId = _tenantService.SchoolId;
            if (schoolId == null)
            {
                TempData["Error"] = "School is not selected.";
                return RedirectToAction(nameof(Schedule), new { yearId });
            }

            var slots = await _context.ScheduleSlot
                .Where(s => s.AcademicYearId == yearId && (s.SchoolId == schoolId.Value || s.SchoolId == 0))
                .ToListAsync();

            if (!slots.Any())
            {
                TempData["Success"] = "Розклад уже порожній.";
                return RedirectToAction(nameof(Schedule), new { yearId });
            }

            _context.ScheduleSlot.RemoveRange(slots);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Очищено розклад для навчального року.";
            return RedirectToAction(nameof(Schedule), new { yearId });
        }
    }
}