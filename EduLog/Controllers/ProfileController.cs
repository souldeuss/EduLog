using Microsoft.AspNetCore.Mvc;
using EduLog.Models;
using EduLog.Data;

namespace EduLog.Controllers
{
    public class ProfileController : Controller
    {
        private readonly EduLogContext _context;
        private readonly string _defaultPhotoPath = "/Data/UserImages/User-avatar.svg.png";
        private readonly string _userImagesFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Data", "UserImages");

        public ProfileController(EduLogContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var teacher = _context.Teacher.FirstOrDefault() ?? new Teacher();
            List<Class> Classes = _context.Class
                .Where(c => c.TeacherId == teacher.Id)
                .Where(c => c != null)
                .ToList();
            List<Class> AvaibleClasses = _context.Class
                .Where(c => c.TeacherId != teacher.Id)
                .ToList();

            // Ось тут зміна: отримуємо повні об'єкти Subject
            var subjects = _context.Subject
                .Where(s => s.TeacherId == teacher.Id)
                .ToList();

            ViewData["Classes"] = Classes;
            ViewData["AvaibleClasses"] = AvaibleClasses;
            ViewData["Subjects"] = subjects;

            return View(teacher);
        }

        [HttpPost]
        public async Task<IActionResult> ChangePhoto(IFormFile ProfilePhoto)
        {
            var teacher = _context.Teacher.FirstOrDefault();
            if (teacher == null)
                return RedirectToAction("Index");

            string relativePath = _defaultPhotoPath;
            if (ProfilePhoto != null && ProfilePhoto.Length > 0)
            {
                var fileName = Path.GetFileName(ProfilePhoto.FileName);
                var savePath = Path.Combine(_userImagesFolder, fileName);

                // Ensure the directory exists
                if (!Directory.Exists(_userImagesFolder))
                {
                    Directory.CreateDirectory(_userImagesFolder);
                }

                using (var stream = new FileStream(savePath, FileMode.Create))
                {
                    await ProfilePhoto.CopyToAsync(stream);
                }

                relativePath = $"/Data/UserImages/{fileName}";
            }

            teacher.PhotoPath = relativePath;
            _context.Update(teacher);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Edit(Teacher model)
        {
            var teacher = _context.Teacher.FirstOrDefault(t => t.Id == model.Id);
            if (teacher == null)
                return NotFound();

            // Оновлюємо властивості
            teacher.Name = model.Name;
            teacher.Surname = model.Surname;
            teacher.Patronymic = model.Patronymic;
            // Якщо потрібно, оновіть інші властивості

            _context.Update(teacher);
            await _context.SaveChangesAsync();

            // Повертаємо користувача на сторінку профілю
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteClass(int classId)
        {
            var teacher = _context.Teacher.FirstOrDefault();
            if (teacher == null)
                return RedirectToAction("Index");

            var selectedClass = _context.Class.FirstOrDefault(c => c.Id == classId && c.TeacherId == teacher.Id);
            if (selectedClass != null)
            {
                selectedClass.TeacherId = null;
                _context.Update(selectedClass);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> AddClass(int selectedClassId)
        {
            var teacher = _context.Teacher.FirstOrDefault();
            if (teacher == null)
                return RedirectToAction("Index");

            var selectedClass = _context.Class.FirstOrDefault(c => c.Id == selectedClassId);
            if (selectedClass == null)
                return RedirectToAction("Index");

            selectedClass.TeacherId = teacher.Id;
            _context.Update(selectedClass);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> AddSubject(string subjectName)
        {
            var teacher = _context.Teacher.FirstOrDefault();
            if (teacher == null || string.IsNullOrWhiteSpace(subjectName))
                return RedirectToAction("Index");

            // Перевірка, чи вже існує такий предмет у цього вчителя
            bool exists = _context.Subject.Any(s => s.Name == subjectName && s.TeacherId == teacher.Id);
            if (!exists)
            {
                var subject = new Subject
                {
                    Name = subjectName,
                    TeacherId = teacher.Id
                };
                _context.Subject.Add(subject);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteSubject(string subjectName)
        {
            var teacher = _context.Teacher.FirstOrDefault();
            if (teacher == null)
                return RedirectToAction("Index");

            var subject = _context.Subject.FirstOrDefault(s => s.Name == subjectName && s.TeacherId == teacher.Id);
            if (subject != null)
            {
                _context.Subject.Remove(subject);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index");
        }
    }
}
