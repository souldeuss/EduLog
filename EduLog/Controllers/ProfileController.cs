using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using EduLog.Models;
using EduLog.Data;

namespace EduLog.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly EduLogContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly string _defaultPhotoPath = "/Data/UserImages/User-avatar.svg.png";
        private readonly string _userImagesFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Data", "UserImages");

        public ProfileController(EduLogContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private async Task<Teacher?> GetCurrentTeacherAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.TeacherId == null)
                return null;
            return _context.Teacher.FirstOrDefault(t => t.Id == user.TeacherId);
        }

        public async Task<IActionResult> Index()
        {
            var teacher = await GetCurrentTeacherAsync() ?? new Teacher();
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
            var teacher = await GetCurrentTeacherAsync();
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
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteClass(int classId)
        {
            var teacher = await GetCurrentTeacherAsync();
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
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddClass(int selectedClassId)
        {
            var teacher = await GetCurrentTeacherAsync();
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
            var teacher = await GetCurrentTeacherAsync();
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
            var teacher = await GetCurrentTeacherAsync();
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

        [HttpPost]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
            {
                TempData["PasswordError"] = "Заповніть усі поля";
                return RedirectToAction("Index");
            }

            if (newPassword != confirmPassword)
            {
                TempData["PasswordError"] = "Новий пароль і підтвердження не збігаються";
                return RedirectToAction("Index");
            }

            if (newPassword.Length < 6)
            {
                TempData["PasswordError"] = "Пароль має містити щонайменше 6 символів";
                return RedirectToAction("Index");
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Index");

            var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
            if (result.Succeeded)
            {
                TempData["PasswordSuccess"] = "Пароль успішно змінено";
            }
            else
            {
                TempData["PasswordError"] = string.Join("; ", result.Errors.Select(e => e.Description));
            }

            return RedirectToAction("Index");
        }
    }
}
