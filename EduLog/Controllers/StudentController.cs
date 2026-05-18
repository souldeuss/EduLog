using EduLog.Data;
using EduLog.Models;
using EduLog.Models.StudentArea;
using EduLog.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduLog.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentController : Controller
    {
        // Approximate timetable: lesson N starts at 8:00 + (N-1)*55min, lasts 45 min
        // (5 min break between lessons). Used for highlighting "current lesson".
        private static readonly TimeSpan LessonStart = new(8, 0, 0);
        private const int LessonLengthMin = 45;
        private const int LessonStepMin = 55;

        private readonly EduLogContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IGamificationService _gamification;
        private readonly IFileStorageService _fileStorage;
        private readonly IIrtBktService _irtBkt;

        // Стоп-критерії для адаптивної сесії.
        private const double AdaptiveMasteryThreshold = 0.8;
        private const int AdaptiveMaxQuestions = 10;
        private const double AdaptiveHintThreshold = 0.4;

        public StudentController(
            EduLogContext context,
            UserManager<ApplicationUser> userManager,
            IGamificationService gamification,
            IFileStorageService fileStorage,
            IIrtBktService irtBkt)
        {
            _context = context;
            _userManager = userManager;
            _gamification = gamification;
            _fileStorage = fileStorage;
            _irtBkt = irtBkt;
        }

        // ───────── Helpers ─────────

        private async Task<Student?> GetCurrentStudentAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return null;
            return await _context.Student
                .Include(s => s.Class)
                .FirstOrDefaultAsync(s => s.ApplicationUserId == user.Id);
        }

        private async Task<StudentSummary> BuildSummaryAsync(Student student)
        {
            var school = await _context.School
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.Id == student.SchoolId);

            var fullName = $"{student.Surname} {student.Name}".Trim();
            var initials = $"{(student.Surname.Length > 0 ? student.Surname[0] : ' ')}" +
                           $"{(student.Name.Length > 0 ? student.Name[0] : ' ')}";

            var levelInfo = _gamification.GetLevelInfo(student.Level);
            var nextLevelXp = _gamification.GetXpForNextLevel(student.Level);
            var xpForCurrent = levelInfo.XpRequired;
            var xpProgress = nextLevelXp > xpForCurrent
                ? Math.Clamp((int)((double)(student.ExperiencePoints - xpForCurrent) / (nextLevelXp - xpForCurrent) * 100), 0, 100)
                : 100;

            var avg = await _gamification.GetGradeAverageAsync(student.Id);
            var status = await _gamification.GetGradeAverageStatusAsync(student.Id);

            return new StudentSummary
            {
                Id = student.Id,
                FullName = fullName,
                Initials = initials.Trim().ToUpperInvariant(),
                AvatarPath = student.AvatarPath,
                ClassName = student.Class?.Name ?? "",
                SchoolName = school?.Name ?? "",
                Level = student.Level,
                LevelTitle = levelInfo.Title,
                ExperiencePoints = student.ExperiencePoints,
                XpForCurrentLevel = xpForCurrent,
                XpForNextLevel = nextLevelXp,
                XpProgressPercent = xpProgress,
                EduCoins = student.EduCoins,
                AttendanceStreak = student.AttendanceStreak,
                GradeAverage = avg,
                GradeAverageStatus = status
            };
        }

        private async Task<AcademicYear?> GetCurrentYearAsync(int schoolId)
        {
            return await _context.AcademicYear
                .Where(y => y.SchoolId == schoolId && y.IsCurrent && !y.IsArchived)
                .OrderByDescending(y => y.StartDate)
                .FirstOrDefaultAsync();
        }

        private static (TimeSpan Start, TimeSpan End) LessonTimeRange(int lessonNumber)
        {
            var start = LessonStart + TimeSpan.FromMinutes((lessonNumber - 1) * LessonStepMin);
            var end = start + TimeSpan.FromMinutes(LessonLengthMin);
            return (start, end);
        }

        private static int IsoDayOfWeek(DayOfWeek dow) =>
            dow == DayOfWeek.Sunday ? 7 : (int)dow;

        // ───────── Dashboard ─────────

        public async Task<IActionResult> Dashboard()
        {
            var student = await GetCurrentStudentAsync();
            if (student == null) return RedirectToAction("Login", "Account");

            var summary = await BuildSummaryAsync(student);
            var year = await GetCurrentYearAsync(student.SchoolId);

            // Today's schedule
            var today = DateTime.Now;
            int todayDow = IsoDayOfWeek(today.DayOfWeek);
            var nowTime = today.TimeOfDay;

            var todaySlots = year == null
                ? new List<ScheduleSlot>()
                : await _context.ScheduleSlot
                    .Include(s => s.Subject)
                    .Include(s => s.Teacher)
                    .Where(s => s.ClassId == student.ClassId
                        && s.AcademicYearId == year.Id
                        && s.DayOfWeek == todayDow)
                    .OrderBy(s => s.LessonNumber)
                    .ToListAsync();

            var todayItems = todaySlots.Select(s =>
            {
                var (start, end) = LessonTimeRange(s.LessonNumber);
                return new ScheduleSlotItem
                {
                    LessonNumber = s.LessonNumber,
                    SubjectName = s.Subject.Name,
                    TeacherName = $"{s.Teacher.Surname} {s.Teacher.Name}".Trim(),
                    Room = s.Room,
                    Date = today.Date.Add(start),
                    IsCurrent = nowTime >= start && nowTime <= end
                };
            }).ToList();

            // Find next upcoming lesson today
            var nextLesson = todayItems.FirstOrDefault(i => i.Date.TimeOfDay > nowTime);

            // Upcoming homework: 5 most-relevant unfinished homeworks
            var allClassMaterials = await _context.LessonMaterial
                .Include(m => m.ClassSubject!).ThenInclude(cs => cs.Subject)
                .Where(m => m.ClassSubjectClassId == student.ClassId
                    && m.Type == MaterialType.Homework)
                .OrderBy(m => m.Deadline ?? DateTime.MaxValue)
                .ThenByDescending(m => m.CreatedAt)
                .ToListAsync();

            var mySubmissions = await _context.HomeworkSubmission
                .Where(hs => hs.StudentId == student.Id)
                .ToDictionaryAsync(hs => hs.LessonMaterialId, hs => hs);

            var upcomingHomework = allClassMaterials
                .Where(m =>
                {
                    mySubmissions.TryGetValue(m.Id, out var sub);
                    return sub == null || sub.Status == SubmissionStatus.NotSubmitted;
                })
                .Take(5)
                .Select(m =>
                {
                    mySubmissions.TryGetValue(m.Id, out var sub);
                    return new UpcomingHomeworkItem
                    {
                        MaterialId = m.Id,
                        SubjectId = m.ClassSubjectSubjectId,
                        SubjectName = m.ClassSubject?.Subject?.Name ?? "",
                        Title = m.Title,
                        Deadline = m.Deadline,
                        Status = sub?.Status ?? SubmissionStatus.NotSubmitted
                    };
                })
                .ToList();

            // Recent grades — top 5
            var recentGrades = await (from g in _context.Grade
                                      join s in _context.Subject on g.SubjectId equals s.Id
                                      where g.StudentId == student.Id
                                      orderby g.Date descending, g.Id descending
                                      select new GradeListItem
                                      {
                                          SubjectId = s.Id,
                                          SubjectName = s.Name,
                                          Value = g.Value,
                                          VerbalValue = g.VerbalValue,
                                          Date = g.Date
                                      }).Take(5).ToListAsync();

            var vm = new StudentDashboardViewModel
            {
                Summary = summary,
                TodaySchedule = todayItems,
                NextLesson = nextLesson,
                UpcomingHomework = upcomingHomework,
                RecentGrades = recentGrades
            };
            return View(vm);
        }

        // ───────── Schedule ─────────

        public async Task<IActionResult> Schedule(DateTime? weekStart)
        {
            var student = await GetCurrentStudentAsync();
            if (student == null) return RedirectToAction("Login", "Account");

            var summary = await BuildSummaryAsync(student);
            var year = await GetCurrentYearAsync(student.SchoolId);

            // Snap to Monday of selected week
            var anchor = (weekStart ?? DateTime.Today).Date;
            int diff = (7 + (IsoDayOfWeek(anchor.DayOfWeek) - 1)) % 7;
            var monday = anchor.AddDays(-diff);
            var saturday = monday.AddDays(5);

            var slots = year == null
                ? new List<ScheduleSlot>()
                : await _context.ScheduleSlot
                    .Include(s => s.Subject)
                    .Include(s => s.Teacher)
                    .Where(s => s.ClassId == student.ClassId
                        && s.AcademicYearId == year.Id
                        && s.DayOfWeek >= 1 && s.DayOfWeek <= 6)
                    .ToListAsync();

            // Load absences for the week, keyed by (date, subjectId) so we can mark
            // only the specific lesson cell — not the whole day.
            var weekAbsences = await _context.Absence
                .Where(a => a.StudentId == student.Id
                    && a.Date >= monday && a.Date < saturday.AddDays(1))
                .Select(a => new { a.Date, a.SubjectId })
                .ToListAsync();

            var absenceSet = new HashSet<(DateTime Date, int SubjectId)>(
                weekAbsences.Select(a => (a.Date.Date, a.SubjectId)));

            var nowDow = IsoDayOfWeek(DateTime.Now.DayOfWeek);
            var nowTime = DateTime.Now.TimeOfDay;
            bool isThisWeek = monday <= DateTime.Today && DateTime.Today <= saturday;

            var cells = new Dictionary<(int, int), WeekScheduleCell>();
            int maxLesson = 0;
            foreach (var s in slots)
            {
                if (s.LessonNumber > maxLesson) maxLesson = s.LessonNumber;
                var (start, end) = LessonTimeRange(s.LessonNumber);
                bool isCurrent = isThisWeek && s.DayOfWeek == nowDow && nowTime >= start && nowTime <= end;
                var dayDate = monday.AddDays(s.DayOfWeek - 1);
                bool hasAbsence = absenceSet.Contains((dayDate, s.SubjectId));

                cells[(s.DayOfWeek, s.LessonNumber)] = new WeekScheduleCell
                {
                    SubjectName = s.Subject.Name,
                    TeacherName = $"{s.Teacher.Surname} {s.Teacher.Name}".Trim(),
                    Room = s.Room,
                    IsCurrent = isCurrent,
                    HasAbsenceThisLesson = hasAbsence
                };
            }

            var vm = new StudentScheduleViewModel
            {
                Summary = summary,
                WeekStart = monday,
                WeekEnd = saturday,
                PreviousWeek = monday.AddDays(-7),
                NextWeek = monday.AddDays(7),
                Cells = cells,
                MaxLessonNumber = Math.Max(maxLesson, 6)
            };
            return View(vm);
        }

        // ───────── Materials ─────────

        public async Task<IActionResult> Materials(int? subjectId, string? filter)
        {
            var student = await GetCurrentStudentAsync();
            if (student == null) return RedirectToAction("Login", "Account");

            var summary = await BuildSummaryAsync(student);
            filter = filter?.ToLowerInvariant() switch
            {
                "homework" => "homework",
                "notes" => "notes",
                _ => "all"
            };

            // Subjects sidebar with pending counts
            var subjects = await _context.ClassSubject
                .Include(cs => cs.Subject)
                .Where(cs => cs.ClassId == student.ClassId)
                .Select(cs => new SubjectSidebarItem
                {
                    Id = cs.Subject.Id,
                    Name = cs.Subject.Name,
                    PendingHomeworkCount = _context.LessonMaterial
                        .Count(m => m.ClassSubjectClassId == student.ClassId
                            && m.ClassSubjectSubjectId == cs.SubjectId
                            && m.Type == MaterialType.Homework
                            && !_context.HomeworkSubmission.Any(hs =>
                                hs.LessonMaterialId == m.Id
                                && hs.StudentId == student.Id
                                && hs.Status != SubmissionStatus.NotSubmitted))
                })
                .OrderBy(s => s.Name)
                .ToListAsync();

            // Materials list
            var query = _context.LessonMaterial
                .Include(m => m.Teacher)
                .Include(m => m.ClassSubject!).ThenInclude(cs => cs.Subject)
                .Where(m => m.ClassSubjectClassId == student.ClassId)
                .AsQueryable();

            if (subjectId.HasValue)
                query = query.Where(m => m.ClassSubjectSubjectId == subjectId.Value);

            if (filter == "homework")
                query = query.Where(m => m.Type == MaterialType.Homework);
            else if (filter == "notes")
                query = query.Where(m => m.Type != MaterialType.Homework);

            var materials = await query
                .OrderByDescending(m => m.Date)
                .ThenByDescending(m => m.CreatedAt)
                .ToListAsync();

            var materialIds = materials.Select(m => m.Id).ToList();
            var submissions = await _context.HomeworkSubmission
                .Where(hs => hs.StudentId == student.Id && materialIds.Contains(hs.LessonMaterialId))
                .ToDictionaryAsync(hs => hs.LessonMaterialId, hs => hs);

            var items = materials.Select(m =>
            {
                submissions.TryGetValue(m.Id, out var sub);
                return new MaterialItem
                {
                    Id = m.Id,
                    SubjectId = m.ClassSubjectSubjectId,
                    SubjectName = m.ClassSubject?.Subject?.Name ?? "",
                    Type = m.Type,
                    Title = m.Title,
                    Description = m.Description,
                    Date = m.Date,
                    Deadline = m.Deadline,
                    TeacherName = $"{m.Teacher?.Surname} {m.Teacher?.Name}".Trim(),
                    AttachmentPath = m.AttachmentPath,
                    AttachmentFileName = m.AttachmentFileName,
                    SubmissionStatus = sub?.Status ?? SubmissionStatus.NotSubmitted,
                    SubmissionId = sub?.Id,
                    SubmissionTextAnswer = sub?.TextAnswer,
                    SubmissionAttachmentPath = sub?.AttachmentPath,
                    SubmissionAttachmentFileName = sub?.AttachmentFileName,
                    SubmittedAt = sub?.SubmittedAt,
                    TeacherComment = sub?.TeacherComment,
                    ReviewScore = sub?.ReviewScore
                };
            }).ToList();

            // Множина LessonMaterial.Id, для яких є питання банку (адаптивний режим увімкнено).
            // Питання в банку зараз прив'язані до Subject, тому матеріал має адаптив, якщо
            // для його предмета існує хоч один QuestionItem.
            var subjectIdsWithQuestions = await _context.QuestionItem
                .Where(q => materials.Select(m => m.ClassSubjectSubjectId).Contains(q.SubjectId))
                .Select(q => q.SubjectId)
                .Distinct()
                .ToListAsync();
            var adaptiveMaterialIds = materials
                .Where(m => m.Type == MaterialType.Homework
                            && subjectIdsWithQuestions.Contains(m.ClassSubjectSubjectId))
                .Select(m => m.Id)
                .ToHashSet();

            ViewData["AdaptiveMaterialIds"] = adaptiveMaterialIds;

            var vm = new StudentMaterialsViewModel
            {
                Summary = summary,
                Subjects = subjects,
                SelectedSubjectId = subjectId,
                Filter = filter,
                Materials = items
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitHomework(
            int lessonMaterialId, string? textAnswer, IFormFile? attachment,
            CancellationToken cancellationToken)
        {
            var student = await GetCurrentStudentAsync();
            if (student == null) return RedirectToAction("Login", "Account");

            var material = await _context.LessonMaterial
                .FirstOrDefaultAsync(m => m.Id == lessonMaterialId);
            if (material == null) return NotFound();
            if (material.ClassSubjectClassId != student.ClassId)
                return Forbid();
            if (material.Type != MaterialType.Homework)
            {
                TempData["Error"] = "Цей матеріал не є домашнім завданням.";
                return RedirectToAction(nameof(Materials), new { subjectId = material.ClassSubjectSubjectId });
            }

            if (string.IsNullOrWhiteSpace(textAnswer) && (attachment == null || attachment.Length == 0))
            {
                TempData["Error"] = "Додайте текст або файл відповіді.";
                return RedirectToAction(nameof(Materials), new { subjectId = material.ClassSubjectSubjectId });
            }

            var submission = await _context.HomeworkSubmission
                .FirstOrDefaultAsync(hs => hs.LessonMaterialId == lessonMaterialId && hs.StudentId == student.Id);

            bool wasNew = submission == null;
            submission ??= new HomeworkSubmission
            {
                LessonMaterialId = lessonMaterialId,
                StudentId = student.Id
            };

            submission.TextAnswer = string.IsNullOrWhiteSpace(textAnswer) ? null : textAnswer.Trim();

            if (attachment != null && attachment.Length > 0)
            {
                try
                {
                    var stored = await _fileStorage.SaveAsync(attachment, "submissions", cancellationToken);
                    if (stored != null)
                    {
                        // Replace previous file if any
                        _fileStorage.Delete(submission.AttachmentPath);
                        submission.AttachmentPath = stored.RelativePath;
                        submission.AttachmentFileName = stored.OriginalFileName;
                    }
                }
                catch (InvalidOperationException ex)
                {
                    TempData["Error"] = ex.Message;
                    return RedirectToAction(nameof(Materials), new { subjectId = material.ClassSubjectSubjectId });
                }
            }

            submission.Status = SubmissionStatus.Submitted;
            submission.SubmittedAt = DateTime.UtcNow;

            if (wasNew) _context.HomeworkSubmission.Add(submission);
            await _context.SaveChangesAsync();

            // Award gamification only on FIRST submission and only if before deadline
            if (wasNew)
            {
                bool onTime = !material.Deadline.HasValue || DateTime.UtcNow <= material.Deadline.Value;
                var reward = await _gamification.ProcessHomeworkSubmittedAsync(student.Id, lessonMaterialId, onTime);
                if (reward.XpAwarded > 0)
                    TempData["GamificationReward"] = $"+{reward.XpAwarded} XP, +{reward.CoinsAwarded} 🪙 за здачу ДЗ!";
            }

            TempData["Success"] = "Домашнє завдання надіслано.";
            return RedirectToAction(nameof(Materials), new { subjectId = material.ClassSubjectSubjectId });
        }

        // ───────── Profile ─────────

        public async Task<IActionResult> Profile()
        {
            var student = await GetCurrentStudentAsync();
            if (student == null) return RedirectToAction("Login", "Account");

            var summary = await BuildSummaryAsync(student);

            var totalXp = student.ExperiencePoints;

            // Homework counts (across the student's class lifetime)
            var assignedHwCount = await _context.LessonMaterial
                .CountAsync(m => m.ClassSubjectClassId == student.ClassId && m.Type == MaterialType.Homework);
            var submittedHwCount = await _context.HomeworkSubmission
                .CountAsync(hs => hs.StudentId == student.Id && hs.Status != SubmissionStatus.NotSubmitted);
            int hwPercent = assignedHwCount > 0 ? (int)Math.Round(submittedHwCount * 100.0 / assignedHwCount) : 0;

            // Attendance % for current academic year using ScheduleSlot to estimate expected lessons
            int attendancePercent = await ComputeAttendancePercentAsync(student);

            // Grade chart: last 30 days, grouped by subject
            var since = DateTime.UtcNow.Date.AddDays(-30);
            var grades = await (from g in _context.Grade
                                join s in _context.Subject on g.SubjectId equals s.Id
                                where g.StudentId == student.Id && g.Date >= since && g.Value != null
                                orderby g.Date
                                select new { SubjectName = s.Name, Value = g.Value!.Value, Date = g.Date.Date })
                                .ToListAsync();

            var dateLabels = Enumerable.Range(0, 31)
                .Select(d => since.AddDays(d).ToString("yyyy-MM-dd"))
                .ToList();

            var chartSeries = grades
                .GroupBy(x => x.SubjectName)
                .ToDictionary(
                    g => g.Key,
                    g => dateLabels.Select(label =>
                    {
                        var on = g.Where(x => x.Date.ToString("yyyy-MM-dd") == label).ToList();
                        return on.Any() ? (int?)(int)on.Average(x => x.Value) : null;
                    }).ToList()
                );

            var vm = new StudentProfileViewModel
            {
                Summary = summary,
                TotalXp = totalXp,
                HomeworkSubmittedCount = submittedHwCount,
                HomeworkAssignedCount = assignedHwCount,
                HomeworkPercent = hwPercent,
                AttendancePercent = attendancePercent,
                ChartLabels = dateLabels,
                ChartSeries = chartSeries
            };
            return View(vm);
        }

        private async Task<int> ComputeAttendancePercentAsync(Student student)
        {
            var year = await GetCurrentYearAsync(student.SchoolId);
            if (year == null) return 100;

            var today = DateTime.Today;
            var rangeStart = year.StartDate.Date;
            var rangeEnd = (today < year.EndDate.Date) ? today : year.EndDate.Date;
            if (rangeEnd < rangeStart) return 100;

            // Count expected lessons by walking from start to end and summing slots per weekday
            var slots = await _context.ScheduleSlot
                .Where(s => s.ClassId == student.ClassId && s.AcademicYearId == year.Id)
                .Select(s => new { s.DayOfWeek })
                .ToListAsync();
            var slotsPerDay = slots.GroupBy(x => x.DayOfWeek).ToDictionary(g => g.Key, g => g.Count());

            int expected = 0;
            for (var d = rangeStart; d <= rangeEnd; d = d.AddDays(1))
            {
                int dow = IsoDayOfWeek(d.DayOfWeek);
                if (slotsPerDay.TryGetValue(dow, out var n)) expected += n;
            }

            int absences = await _context.Absence
                .CountAsync(a => a.StudentId == student.Id && a.Date >= rangeStart && a.Date <= rangeEnd);

            if (expected == 0) return 100;
            int present = Math.Max(0, expected - absences);
            return Math.Clamp((int)Math.Round(present * 100.0 / expected), 0, 100);
        }

        private List<AchievementItem> ComputeAchievements(Student student, int submittedHwCount)
        {
            return new List<AchievementItem>
            {
                new()
                {
                    Icon = "🔥", Title = "Тиждень без пропусків",
                    Description = "Streak 7 днів",
                    Unlocked = student.AttendanceStreak >= 7
                },
                new()
                {
                    Icon = "🔥", Title = "Місяць без пропусків",
                    Description = "Streak 30 днів",
                    Unlocked = student.AttendanceStreak >= 30
                },
                new()
                {
                    Icon = "📚", Title = "Старанний учень",
                    Description = "Здати 10 ДЗ",
                    Unlocked = submittedHwCount >= 10
                },
                new()
                {
                    Icon = "🏆", Title = "Академік",
                    Description = "Досягти рівня 6",
                    Unlocked = student.Level >= 6
                },
                new()
                {
                    Icon = "✨", Title = "Геній",
                    Description = "Досягти рівня 7",
                    Unlocked = student.Level >= 7
                }
            };
        }

        // ───────── Avatar ─────────

        private static readonly HashSet<string> _allowedAvatarExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".gif", ".webp"
        };

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadAvatar(IFormFile? avatar, CancellationToken cancellationToken)
        {
            var student = await GetCurrentStudentAsync();
            if (student == null) return RedirectToAction("Login", "Account");

            if (avatar == null || avatar.Length == 0)
            {
                TempData["Error"] = "Оберіть файл зображення.";
                return RedirectToAction(nameof(Profile));
            }

            var ext = Path.GetExtension(avatar.FileName);
            if (!_allowedAvatarExtensions.Contains(ext))
            {
                TempData["Error"] = "Дозволені формати: PNG, JPG, GIF, WebP.";
                return RedirectToAction(nameof(Profile));
            }

            try
            {
                var stored = await _fileStorage.SaveAsync(avatar, "avatars", cancellationToken);
                if (stored == null)
                {
                    TempData["Error"] = "Не вдалося зберегти файл.";
                    return RedirectToAction(nameof(Profile));
                }

                // Cleanup previous avatar
                _fileStorage.Delete(student.AvatarPath);

                student.AvatarPath = stored.RelativePath;
                await _context.SaveChangesAsync(cancellationToken);

                TempData["Success"] = "Аватар оновлено.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Profile));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAvatar(CancellationToken cancellationToken)
        {
            var student = await GetCurrentStudentAsync();
            if (student == null) return RedirectToAction("Login", "Account");

            if (!string.IsNullOrEmpty(student.AvatarPath))
            {
                _fileStorage.Delete(student.AvatarPath);
                student.AvatarPath = null;
                await _context.SaveChangesAsync(cancellationToken);
                TempData["Success"] = "Аватар видалено.";
            }

            return RedirectToAction(nameof(Profile));
        }

        // ───────── Achievements + per-subject grade summary ─────────

        public async Task<IActionResult> Achievements()
        {
            var student = await GetCurrentStudentAsync();
            if (student == null) return RedirectToAction("Login", "Account");

            var summary = await BuildSummaryAsync(student);

            var submittedHwCount = await _context.HomeworkSubmission
                .CountAsync(hs => hs.StudentId == student.Id && hs.Status != SubmissionStatus.NotSubmitted);

            var achievements = ComputeAchievements(student, submittedHwCount);

            // Per-subject grade summary
            var grades = await (from g in _context.Grade
                                join s in _context.Subject on g.SubjectId equals s.Id
                                where g.StudentId == student.Id && g.Value != null
                                orderby g.Date descending, g.Id descending
                                select new { SubjectId = s.Id, SubjectName = s.Name, Value = g.Value!.Value, g.Date })
                                .ToListAsync();

            var subjectRows = grades
                .GroupBy(x => new { x.SubjectId, x.SubjectName })
                .Select(g =>
                {
                    var ordered = g.OrderByDescending(x => x.Date).ToList();
                    var recent = ordered.Take(5).Select(x => x.Value).ToList();
                    var avg = g.Average(x => (double)x.Value);

                    int trend = 0;
                    if (recent.Count >= 2)
                    {
                        // Compare avg of newest half vs oldest half of recent grades
                        int half = recent.Count / 2;
                        if (half > 0)
                        {
                            var newest = recent.Take(recent.Count - half).Average();
                            var oldest = recent.Skip(recent.Count - half).Average();
                            if (newest - oldest >= 0.5) trend = 1;
                            else if (oldest - newest >= 0.5) trend = -1;
                        }
                    }

                    string status = avg switch
                    {
                        >= 10.0 => "✨ Ідеальний",
                        >= 8.0 => "🌟 Чудовий",
                        >= 6.0 => "👍 Добрий",
                        >= 4.0 => "📚 Непогано",
                        _ => "💪 Намагається"
                    };

                    return new SubjectGradesRow
                    {
                        SubjectId = g.Key.SubjectId,
                        SubjectName = g.Key.SubjectName,
                        GradeCount = g.Count(),
                        Average = avg,
                        LastGrade = ordered[0].Value,
                        LastGradeDate = ordered[0].Date,
                        Trend = trend,
                        RecentGrades = recent,
                        Status = status
                    };
                })
                .OrderByDescending(r => r.Average)
                .ToList();

            var vm = new StudentAchievementsViewModel
            {
                Summary = summary,
                Achievements = achievements,
                UnlockedCount = achievements.Count(a => a.Unlocked),
                SubjectGrades = subjectRows
            };
            return View(vm);
        }

        // ───────── Adaptive homework (IRT 3PL + BKT) ─────────

        // Створює (або повертає існуючу) сесію адаптивного ДЗ для конкретного матеріалу.
        // Підбирає перше питання за поточним станом знань учня по темі.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartAdaptiveSession(int lessonMaterialId)
        {
            var student = await GetCurrentStudentAsync();
            if (student == null) return Unauthorized();

            var material = await _context.LessonMaterial
                .FirstOrDefaultAsync(m => m.Id == lessonMaterialId && m.Type == MaterialType.Homework);
            if (material == null) return NotFound();

            // Якщо є незавершена сесія — повертаємо її.
            var session = await _context.AdaptiveSession
                .Include(s => s.CurrentQuestion)
                .FirstOrDefaultAsync(s => s.StudentId == student.Id
                                        && s.LessonMaterialId == lessonMaterialId
                                        && s.CompletedAt == null);

            if (session == null)
            {
                session = new AdaptiveSession
                {
                    StudentId = student.Id,
                    LessonMaterialId = lessonMaterialId
                };
                _context.AdaptiveSession.Add(session);
                await _context.SaveChangesAsync();
            }

            var subjectId = material.ClassSubjectSubjectId;
            var firstQuestion = session.CurrentQuestion
                ?? await PickQuestionForSessionAsync(session, student.Id, subjectId);

            if (firstQuestion != null && session.CurrentQuestionId != firstQuestion.Id)
            {
                session.CurrentQuestionId = firstQuestion.Id;
                await _context.SaveChangesAsync();
            }

            // Початковий pL для теми першого питання (для прогрес-бара).
            double pLearned = firstQuestion == null
                ? 0.0
                : await GetOrInitKnowledgeAsync(student, subjectId, firstQuestion.TopicTag);

            return Json(new
            {
                sessionId = session.Id,
                nextQuestion = firstQuestion == null ? null : new { id = firstQuestion.Id, text = firstQuestion.Text },
                hint = (firstQuestion != null && pLearned < AdaptiveHintThreshold) ? firstQuestion.HintText : null,
                pLearned,
                isComplete = firstQuestion == null
            });
        }

        // POST /Student/SubmitAdaptiveAnswer
        // Приймає (sessionId, questionId, isCorrect), оновлює BKT-стан, підбирає наступне питання.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitAdaptiveAnswer(int sessionId, int questionId, bool isCorrect)
        {
            var student = await GetCurrentStudentAsync();
            if (student == null) return Unauthorized();

            var session = await _context.AdaptiveSession
                .Include(s => s.LessonMaterial)
                .FirstOrDefaultAsync(s => s.Id == sessionId && s.StudentId == student.Id);
            if (session == null) return NotFound();
            if (session.CompletedAt != null) return BadRequest("Сесію вже завершено.");

            var question = await _context.QuestionItem.FirstOrDefaultAsync(q => q.Id == questionId);
            if (question == null) return NotFound();

            // 1) Фіксуємо відповідь.
            _context.AdaptiveAnswer.Add(new AdaptiveAnswer
            {
                SessionId = session.Id,
                QuestionId = question.Id,
                IsCorrect = isCorrect
            });

            // 2) Оновлюємо BKT-стан по темі цього питання.
            var state = await _context.StudentKnowledgeState
                .FirstOrDefaultAsync(s => s.StudentId == student.Id
                                        && s.SubjectId == question.SubjectId
                                        && s.TopicTag == question.TopicTag);
            if (state == null)
            {
                state = new StudentKnowledgeState
                {
                    StudentId = student.Id,
                    SubjectId = question.SubjectId,
                    TopicTag = question.TopicTag,
                    ProbabilityLearned = 0.2
                };
                _context.StudentKnowledgeState.Add(state);
            }

            double newPl = _irtBkt.UpdateBkt(
                state.ProbabilityLearned,
                _irtBkt.DefaultPT, _irtBkt.DefaultPS, _irtBkt.DefaultPG,
                isCorrect);
            state.ProbabilityLearned = newPl;
            state.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // 3) Перевіряємо стоп-критерії.
            int answersCount = await _context.AdaptiveAnswer.CountAsync(a => a.SessionId == session.Id);
            bool stopByMastery = newPl > AdaptiveMasteryThreshold;
            bool stopByLimit = answersCount >= AdaptiveMaxQuestions;

            if (stopByMastery || stopByLimit)
            {
                session.CompletedAt = DateTime.UtcNow;
                session.CurrentQuestionId = null;
                await _context.SaveChangesAsync();

                return Json(new
                {
                    nextQuestion = (object?)null,
                    hint = (string?)null,
                    pLearned = newPl,
                    isComplete = true
                });
            }

            // 4) Підбираємо наступне питання.
            var subjectId = session.LessonMaterial?.ClassSubjectSubjectId ?? question.SubjectId;
            var next = await PickQuestionForSessionAsync(session, student.Id, subjectId);

            session.CurrentQuestionId = next?.Id;
            await _context.SaveChangesAsync();

            // pL для теми наступного питання (для відображення прогресу/підказки).
            double pLearnedForNext = next == null
                ? newPl
                : await GetOrInitKnowledgeAsync(student, subjectId, next.TopicTag);

            return Json(new
            {
                nextQuestion = next == null ? null : new { id = next.Id, text = next.Text },
                hint = (next != null && pLearnedForNext < AdaptiveHintThreshold) ? next.HintText : null,
                pLearned = pLearnedForNext,
                isComplete = next == null
            });
        }

        // Список питань предмета, які учень ще не давав у поточній сесії, та їх вибір через IRT/BKT.
        private async Task<QuestionItem?> PickQuestionForSessionAsync(AdaptiveSession session, int studentId, int subjectId)
        {
            var answeredIds = await _context.AdaptiveAnswer
                .Where(a => a.SessionId == session.Id)
                .Select(a => a.QuestionId)
                .ToListAsync();

            var available = await _context.QuestionItem
                .Where(q => q.SubjectId == subjectId && !answeredIds.Contains(q.Id))
                .ToListAsync();
            if (available.Count == 0) return null;

            // Якщо банк містить кілька тем — балансуємо: беремо середнє pL по темах,
            // що зустрічаються в доступних питаннях. Це грубо, але достатньо для першого ітерації.
            var topics = available.Select(q => q.TopicTag).Distinct().ToList();
            var states = await _context.StudentKnowledgeState
                .Where(s => s.StudentId == studentId && s.SubjectId == subjectId && topics.Contains(s.TopicTag))
                .ToDictionaryAsync(s => s.TopicTag, s => s.ProbabilityLearned);

            double avgPl = topics.Count == 0 ? 0.2
                : topics.Average(t => states.TryGetValue(t, out var v) ? v : 0.2);

            return _irtBkt.SelectNextQuestion(avgPl, available);
        }

        private async Task<double> GetOrInitKnowledgeAsync(Student student, int subjectId, string topicTag)
        {
            var s = await _context.StudentKnowledgeState
                .FirstOrDefaultAsync(x => x.StudentId == student.Id
                                       && x.SubjectId == subjectId
                                       && x.TopicTag == topicTag);
            return s?.ProbabilityLearned ?? 0.2;
        }

        // ───────── Coins ─────────

        public async Task<IActionResult> Coins(int page = 1)
        {
            const int pageSize = 20;
            var student = await GetCurrentStudentAsync();
            if (student == null) return RedirectToAction("Login", "Account");

            var summary = await BuildSummaryAsync(student);

            var totalCount = await _context.CoinTransaction.CountAsync(t => t.StudentId == student.Id);
            int totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            page = Math.Clamp(page, 1, totalPages);

            var transactions = await _context.CoinTransaction
                .Where(t => t.StudentId == student.Id)
                .OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var vm = new StudentCoinsViewModel
            {
                Summary = summary,
                Transactions = transactions,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages
            };
            return View(vm);
        }
    }
}
