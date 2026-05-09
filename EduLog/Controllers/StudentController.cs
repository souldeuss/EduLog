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

        public StudentController(
            EduLogContext context,
            UserManager<ApplicationUser> userManager,
            IGamificationService gamification,
            IFileStorageService fileStorage)
        {
            _context = context;
            _userManager = userManager;
            _gamification = gamification;
            _fileStorage = fileStorage;
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

            var weekAbsences = await _context.Absence
                .Where(a => a.StudentId == student.Id
                    && a.Date >= monday && a.Date < saturday.AddDays(1))
                .Select(a => a.Date.Date)
                .ToListAsync();

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
                bool absence = weekAbsences.Contains(dayDate);

                cells[(s.DayOfWeek, s.LessonNumber)] = new WeekScheduleCell
                {
                    SubjectName = s.Subject.Name,
                    TeacherName = $"{s.Teacher.Surname} {s.Teacher.Name}".Trim(),
                    Room = s.Room,
                    IsCurrent = isCurrent,
                    HasAbsenceThisWeek = absence
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

            // Achievements
            var achievements = ComputeAchievements(student, submittedHwCount);

            // Grade chart: last 30 days, grouped by subject
            var since = DateTime.UtcNow.Date.AddDays(-30);
            var grades = await (from g in _context.Grade
                                join s in _context.Subject on g.SubjectId equals s.Id
                                where g.StudentId == student.Id && g.Date >= since
                                orderby g.Date
                                select new { SubjectName = s.Name, g.Value, Date = g.Date.Date })
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
                Achievements = achievements,
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

        // ───────── Classmates ─────────

        public async Task<IActionResult> Classmates()
        {
            var student = await GetCurrentStudentAsync();
            if (student == null) return RedirectToAction("Login", "Account");

            var summary = await BuildSummaryAsync(student);

            var classmates = await _context.Student
                .Where(s => s.ClassId == student.ClassId)
                .OrderBy(s => s.Surname).ThenBy(s => s.Name)
                .ToListAsync();

            var rows = new List<ClassmateRow>();
            foreach (var c in classmates)
            {
                var avg = await _gamification.GetGradeAverageAsync(c.Id);
                var levelInfo = _gamification.GetLevelInfo(c.Level);
                var initials = $"{(c.Surname.Length > 0 ? c.Surname[0] : ' ')}" +
                               $"{(c.Name.Length > 0 ? c.Name[0] : ' ')}";
                rows.Add(new ClassmateRow
                {
                    Id = c.Id,
                    FullName = $"{c.Surname} {c.Name}".Trim(),
                    Initials = initials.Trim().ToUpperInvariant(),
                    Level = c.Level,
                    LevelTitle = levelInfo.Title,
                    EduCoins = c.EduCoins,
                    AttendanceStreak = c.AttendanceStreak,
                    GradeAverage = avg,
                    IsCurrent = c.Id == student.Id
                });
            }

            // Default sort: EduCoins desc
            rows = rows.OrderByDescending(r => r.EduCoins).ThenByDescending(r => r.Level).ToList();

            var vm = new StudentClassmatesViewModel { Summary = summary, Rows = rows };
            return View(vm);
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
