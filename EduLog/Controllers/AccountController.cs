using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EduLog.Models;
using EduLog.Models.Account;
using EduLog.Data;

namespace EduLog.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly EduLogContext _context;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            EduLogContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
        }

        // Old register redirects to school registration
        [HttpGet]
        public IActionResult Register()
        {
            return RedirectToAction("RegisterSchool");
        }

        // ===================== School Registration =====================

        [HttpGet]
        public IActionResult RegisterSchool()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterSchool(RegisterSchoolViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var school = new School
            {
                Name = model.SchoolName,
                Address = model.SchoolAddress,
                Type = model.SchoolType
            };
            _context.School.Add(school);
            await _context.SaveChangesAsync();

            var teacher = new Teacher
            {
                Name = model.Name,
                Surname = model.Surname,
                Patronymic = model.Patronymic ?? string.Empty,
                PhotoPath = "/Data/UserImages/User-avatar.svg.png",
                SchoolId = school.Id
            };
            _context.Teacher.Add(teacher);
            await _context.SaveChangesAsync();

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                TeacherId = teacher.Id,
                SchoolId = school.Id
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "Admin");
                await _signInManager.SignInAsync(user, isPersistent: false);
                return RedirectToAction("Index", "Home");
            }

            // Rollback
            _context.Teacher.Remove(teacher);
            _context.School.Remove(school);
            await _context.SaveChangesAsync();

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return View(model);
        }

        // ===================== Teacher Registration (via invitation) =====================

        [HttpGet]
        public IActionResult RegisterTeacher(string? token)
        {
            if (string.IsNullOrEmpty(token))
                return RedirectToAction("Login");

            var invitation = _context.Invitation
                .IgnoreQueryFilters()
                .FirstOrDefault(i => i.Token == token && !i.IsUsed && i.ExpiresAt > DateTime.UtcNow);

            if (invitation == null)
            {
                TempData["Error"] = "Посилання недійсне або термін дії закінчився.";
                return RedirectToAction("Login");
            }

            var model = new RegisterTeacherViewModel
            {
                Token = token,
                Email = invitation.Email
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterTeacher(RegisterTeacherViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var invitation = await _context.Invitation
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(i => i.Token == model.Token && !i.IsUsed && i.ExpiresAt > DateTime.UtcNow);

            if (invitation == null)
            {
                ModelState.AddModelError(string.Empty, "Посилання недійсне або термін дії закінчився.");
                return View(model);
            }

            if (!string.Equals(invitation.Email, model.Email, StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(model.Email), "Email не відповідає запрошенню.");
                return View(model);
            }

            var teacher = new Teacher
            {
                Name = model.Name,
                Surname = model.Surname,
                Patronymic = model.Patronymic ?? string.Empty,
                PhotoPath = "/Data/UserImages/User-avatar.svg.png",
                SchoolId = invitation.SchoolId
            };
            _context.Teacher.Add(teacher);
            await _context.SaveChangesAsync();

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                TeacherId = teacher.Id,
                SchoolId = invitation.SchoolId
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                invitation.IsUsed = true;
                _context.Invitation.Update(invitation);
                await _context.SaveChangesAsync();

                await _userManager.AddToRoleAsync(user, "Teacher");
                await _signInManager.SignInAsync(user, isPersistent: false);
                return RedirectToAction("Index", "Home");
            }

            // Rollback
            _context.Teacher.Remove(teacher);
            await _context.SaveChangesAsync();

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return View(model);
        }

        // ===================== Login / Logout =====================

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var result = await _signInManager.PasswordSignInAsync(
                model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);

            if (result.Succeeded)
            {
                if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                    return Redirect(model.ReturnUrl);

                return RedirectToAction("Index", "Home");
            }

            if (result.IsLockedOut)
                return View("Lockout");

            ModelState.AddModelError(string.Empty, "Невірний email або пароль");
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
