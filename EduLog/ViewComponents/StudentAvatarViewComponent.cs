using EduLog.Data;
using EduLog.Models;
using EduLog.Models.StudentArea;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduLog.ViewComponents
{
    public class StudentAvatarViewComponent : ViewComponent
    {
        private readonly EduLogContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public StudentAvatarViewComponent(EduLogContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IViewComponentResult> InvokeAsync(string sizeClass = "student-avatar-sm")
        {
            var principal = (System.Security.Claims.ClaimsPrincipal)User;
            if (!principal.IsInRole("Student"))
                return Content(string.Empty);

            var user = await _userManager.GetUserAsync(principal);
            if (user == null) return Content(string.Empty);

            var student = await _context.Student
                .Where(s => s.ApplicationUserId == user.Id)
                .Select(s => new { s.AvatarPath, s.Surname, s.Name })
                .FirstOrDefaultAsync();

            if (student == null) return Content(string.Empty);

            var initials = $"{(student.Surname.Length > 0 ? student.Surname[0] : ' ')}" +
                           $"{(student.Name.Length > 0 ? student.Name[0] : ' ')}";

            return View("Default", new AvatarVm
            {
                AvatarPath = student.AvatarPath,
                Initials = initials.Trim().ToUpperInvariant(),
                SizeClass = sizeClass
            });
        }
    }
}
