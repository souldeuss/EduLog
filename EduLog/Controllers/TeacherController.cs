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
            IFormFile? attachment, CancellationToken cancellationToken)
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
    }
}
