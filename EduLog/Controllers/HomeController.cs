using System.Diagnostics;
using EduLog.Models;
using Microsoft.AspNetCore.Mvc;

namespace EduLog.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            // Authenticated users land on their role-specific dashboard,
            // not the marketing/landing page.
            if (User.Identity?.IsAuthenticated == true)
            {
                if (User.IsInRole("Student"))
                    return RedirectToAction("Dashboard", "Student");
                if (User.IsInRole("Admin"))
                    return RedirectToAction("Index", "Admin");
                if (User.IsInRole("Teacher"))
                    return RedirectToAction("spreader", "Journal");
            }

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
