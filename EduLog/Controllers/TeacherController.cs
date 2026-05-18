using EduLog.Data;
using EduLog.Models;
using EduLog.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduLog.Controllers
{
    [Authorize(Roles = "Teacher,Admin")]
    public class TeacherController : Controller
    {
        private readonly EduLogContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IGamificationService _gamification;
        private readonly IFileStorageService _fileStorage;

        public TeacherController(
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

        private async Task<Teacher?> GetCurrentTeacherAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.TeacherId == null) return null;
            return await _context.Teacher.FirstOrDefaultAsync(t => t.Id == user.TeacherId);
        }

        // ───────── Materials ─────────

        [HttpGet]
        public async Task<IActionResult> Materials(int classId, int subjectId)
        {
            var teacher = await GetCurrentTeacherAsync();

            var cls = await _context.Class.FirstOrDefaultAsync(c => c.Id == classId);
            var subject = await _context.Subject.FirstOrDefaultAsync(s => s.Id == subjectId);

            if (cls == null || subject == null) return NotFound();

            var classSubject = await _context.ClassSubject
                .FirstOrDefaultAsync(cs => cs.ClassId == classId && cs.SubjectId == subjectId);

            if (classSubject == null)
            {
                TempData["Error"] = "Цей предмет не закріплено за класом.";
                return RedirectToAction("spreader", "Journal");
            }

            var materials = await _context.LessonMaterial
                .Include(lm => lm.Teacher)
                .Where(lm => lm.ClassSubjectClassId == classId && lm.ClassSubjectSubjectId == subjectId)
                .OrderByDescending(lm => lm.Date)
                .ToListAsync();

            var materialIds = materials.Select(m => m.Id).ToList();
            var submissionStats = await _context.HomeworkSubmission
                .Where(hs => materialIds.Contains(hs.LessonMaterialId)
                    && hs.Status != SubmissionStatus.NotSubmitted)
                .GroupBy(hs => new { hs.LessonMaterialId, hs.Status })
                .Select(g => new { g.Key.LessonMaterialId, g.Key.Status, Count = g.Count() })
                .ToListAsync();

            var totalStudents = await _context.Student.CountAsync(s => s.ClassId == classId);

            ViewData["Class"] = cls;
            ViewData["Subject"] = subject;
            ViewData["TotalStudents"] = totalStudents;
            ViewData["SubmittedCounts"] = submissionStats
                .Where(s => s.Status == SubmissionStatus.Submitted)
                .ToDictionary(s => s.LessonMaterialId, s => s.Count);
            ViewData["ReviewedCounts"] = submissionStats
                .Where(s => s.Status == SubmissionStatus.Reviewed)
                .ToDictionary(s => s.LessonMaterialId, s => s.Count);
            ViewData["CurrentTeacherId"] = teacher?.Id;

            return View(materials);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateMaterial(
            int classId, int subjectId, MaterialType type,
            string title, string? description, DateTime? date, DateTime? deadline,
            IFormFile? attachment, int eduCoinReward, CancellationToken cancellationToken)
        {
            var teacher = await GetCurrentTeacherAsync();
            if (teacher == null)
            {
                TempData["Error"] = "Дія доступна лише вчителю.";
                return RedirectToAction(nameof(Materials), new { classId, subjectId });
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                TempData["Error"] = "Заголовок обов'язковий.";
                return RedirectToAction(nameof(Materials), new { classId, subjectId });
            }

            var classSubject = await _context.ClassSubject
                .FirstOrDefaultAsync(cs => cs.ClassId == classId && cs.SubjectId == subjectId);
            if (classSubject == null) return NotFound();

            StoredFile? stored = null;
            if (attachment != null && attachment.Length > 0)
            {
                try
                {
                    stored = await _fileStorage.SaveAsync(attachment, "materials", cancellationToken);
                }
                catch (InvalidOperationException ex)
                {
                    TempData["Error"] = ex.Message;
                    return RedirectToAction(nameof(Materials), new { classId, subjectId });
                }
            }

            // Validate EduCoin reward (0..50). Only meaningful for Homework but stored uniformly.
            if (eduCoinReward < 0 || eduCoinReward > 50)
            {
                TempData["Error"] = "Нагорода EduCoin має бути від 0 до 50.";
                return RedirectToAction(nameof(Materials), new { classId, subjectId });
            }

            _context.LessonMaterial.Add(new LessonMaterial
            {
                ClassSubjectClassId = classId,
                ClassSubjectSubjectId = subjectId,
                TeacherId = teacher.Id,
                Type = type,
                Title = title.Trim(),
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                Date = (date ?? DateTime.UtcNow.Date),
                Deadline = deadline,
                AttachmentPath = stored?.RelativePath,
                AttachmentFileName = stored?.OriginalFileName,
                EduCoinReward = type == MaterialType.Homework ? eduCoinReward : 0,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = "Матеріал створено.";
            return RedirectToAction(nameof(Materials), new { classId, subjectId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMaterial(int id)
        {
            var material = await _context.LessonMaterial
                .Include(m => m.Submissions)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (material == null) return NotFound();

            int classId = material.ClassSubjectClassId;
            int subjectId = material.ClassSubjectSubjectId;

            // Cleanup attached files (material itself + all student submissions)
            _fileStorage.Delete(material.AttachmentPath);
            foreach (var sub in material.Submissions)
            {
                _fileStorage.Delete(sub.AttachmentPath);
            }

            _context.LessonMaterial.Remove(material);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Матеріал видалено.";
            return RedirectToAction(nameof(Materials), new { classId, subjectId });
        }

        // ───────── Homework review ─────────

        [HttpGet]
        public async Task<IActionResult> Homework(int id)
        {
            var material = await _context.LessonMaterial
                .Include(m => m.Teacher)
                .Include(m => m.ClassSubject!).ThenInclude(cs => cs.Subject)
                .Include(m => m.ClassSubject!).ThenInclude(cs => cs.Class)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (material == null) return NotFound();

            var students = await _context.Student
                .Where(s => s.ClassId == material.ClassSubjectClassId)
                .OrderBy(s => s.Surname).ThenBy(s => s.Name)
                .ToListAsync();

            var submissions = await _context.HomeworkSubmission
                .Where(hs => hs.LessonMaterialId == id)
                .ToDictionaryAsync(hs => hs.StudentId, hs => hs);

            ViewData["Material"] = material;
            ViewData["Submissions"] = submissions;

            return View(students);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReviewSubmission(int submissionId, string? comment, int? score)
        {
            var submission = await _context.HomeworkSubmission
                .Include(s => s.LessonMaterial)
                .FirstOrDefaultAsync(s => s.Id == submissionId);

            if (submission == null) return NotFound();

            if (score.HasValue && (score.Value < 0 || score.Value > 100))
            {
                TempData["Error"] = "Оцінка має бути в межах 0–100.";
                return RedirectToAction(nameof(Homework), new { id = submission.LessonMaterialId });
            }

            submission.TeacherComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
            submission.ReviewScore = score;
            submission.Status = SubmissionStatus.Reviewed;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Homework), new { id = submission.LessonMaterialId });
        }

        // ───────── Question bank (adaptive IRT 3PL) ─────────

        // Відображає банк питань для конкретного предмета поточного вчителя.
        // subjectId опціональний — якщо не задано, показуємо перший предмет.
        [HttpGet]
        public async Task<IActionResult> QuestionBank(int? subjectId)
        {
            var teacher = await GetCurrentTeacherAsync();
            if (teacher == null) return RedirectToAction("Index", "Home");

            // Усі предмети, які закріплені за вчителем (через Subject.TeacherId або SubjectTeacher).
            var subjects = await _context.Subject
                .Where(s => s.TeacherId == teacher.Id
                         || s.SubjectTeachers.Any(st => st.TeacherId == teacher.Id))
                .OrderBy(s => s.Name)
                .ToListAsync();

            var selectedSubjectId = subjectId ?? subjects.FirstOrDefault()?.Id;
            var questions = selectedSubjectId == null
                ? new List<QuestionItem>()
                : await _context.QuestionItem
                    .Where(q => q.SubjectId == selectedSubjectId.Value)
                    .OrderBy(q => q.TopicTag).ThenBy(q => q.IrtB)
                    .ToListAsync();

            ViewData["Subjects"] = subjects;
            ViewData["SelectedSubjectId"] = selectedSubjectId;
            return View(questions);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateQuestion(
            int subjectId, string text, string topicTag,
            double irtA, double irtB, double irtC, string? hintText)
        {
            var teacher = await GetCurrentTeacherAsync();
            if (teacher == null) return Forbid();

            // Перевірка прав — предмет має бути в списку вчителя.
            var subject = await _context.Subject
                .FirstOrDefaultAsync(s => s.Id == subjectId
                                       && (s.TeacherId == teacher.Id
                                           || s.SubjectTeachers.Any(st => st.TeacherId == teacher.Id)));
            if (subject == null)
            {
                TempData["Error"] = "Доступ до предмета заборонено.";
                return RedirectToAction(nameof(QuestionBank));
            }

            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(topicTag))
            {
                TempData["Error"] = "Текст питання та тема обов'язкові.";
                return RedirectToAction(nameof(QuestionBank), new { subjectId });
            }

            // Клемпи на діапазони, обумовлені моделлю IRT 3PL.
            irtA = Math.Clamp(irtA, 0.1, 3.0);
            irtB = Math.Clamp(irtB, -3.0, 3.0);
            irtC = Math.Clamp(irtC, 0.0, 0.35);

            _context.QuestionItem.Add(new QuestionItem
            {
                SubjectId = subjectId,
                Text = text.Trim(),
                TopicTag = topicTag.Trim(),
                IrtA = irtA,
                IrtB = irtB,
                IrtC = irtC,
                HintText = string.IsNullOrWhiteSpace(hintText) ? null : hintText.Trim()
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = "Питання додано до банку.";
            return RedirectToAction(nameof(QuestionBank), new { subjectId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteQuestion(int id)
        {
            var teacher = await GetCurrentTeacherAsync();
            if (teacher == null) return Forbid();

            var question = await _context.QuestionItem
                .Include(q => q.Subject)
                .FirstOrDefaultAsync(q => q.Id == id);
            if (question == null) return NotFound();

            // Право видаляти має лише вчитель цього предмета.
            bool isOwner = question.Subject?.TeacherId == teacher.Id
                || await _context.SubjectTeacher.AnyAsync(st => st.SubjectId == question.SubjectId && st.TeacherId == teacher.Id);
            if (!isOwner)
            {
                TempData["Error"] = "Немає прав видалити це питання.";
                return RedirectToAction(nameof(QuestionBank), new { subjectId = question.SubjectId });
            }

            int subjectId = question.SubjectId;

            // Видаляємо також пов'язані відповіді — інакше FK Restrict не дозволить.
            var answers = _context.AdaptiveAnswer.Where(a => a.QuestionId == id);
            _context.AdaptiveAnswer.RemoveRange(answers);

            _context.QuestionItem.Remove(question);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Питання видалено.";
            return RedirectToAction(nameof(QuestionBank), new { subjectId });
        }
    }
}
