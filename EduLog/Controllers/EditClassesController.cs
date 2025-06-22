using EduLog.Data;
using EduLog.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduLog.Controllers
{
    public class EditClassesController : Controller
    {
        readonly EduLogContext _context;

        public EditClassesController(EduLogContext context)
        {
            _context = context;
        }

        public IActionResult Index(int classId)
        {
            var cls = _context.Class.Find(classId);
            if (cls == null)
                return NotFound();

            var teacher = _context.Teacher.FirstOrDefault();
            List<Subject> AllSubjects = _context.Subject
                .Where(s => s.TeacherId == teacher.Id)
                .ToList();

            // Отримуємо предмети, які прив'язані до цього класу через ClassSubject
            List<Subject> ClassSubjects = _context.ClassSubject
                .Where(cs => cs.ClassId == cls.Id)
                .Include(cs => cs.Subject)
                .Select(cs => cs.Subject)
                .ToList();

            List<Student> ListStudents = _context.Student
                .Where(s => s.ClassId == classId)
                .ToList();

            ViewData["AllSubjects"] = AllSubjects;
            ViewData["ClassSubjects"] = ClassSubjects;
            ViewData["ClassStudents"] = ListStudents;
            return View(cls);
        }

        public IActionResult SelectClasses()
        {
            var teacher = _context.Teacher.FirstOrDefault();

            if (teacher == null)
                return RedirectToAction("Index", "Profile");

            var ListSubjects = _context.Subject
                .Where(s => s.TeacherId == teacher.Id)
                .ToList();

            var classes = _context.Class
                .Where(c => c.TeacherId == teacher.Id)
                .ToList();

            return View(classes);
        }

        public IActionResult EditClasses(string name)
        {
            var teacher = _context.Teacher.FirstOrDefault();
            if (teacher == null)
                return RedirectToAction("Index", "Profile");
            var classes = _context.Class
                .Where(c => c.TeacherId == teacher.Id)
                .ToList();

            return View(classes);
        }

        [HttpPost]
        public IActionResult EditName(int id, string name)
        {
            var cls = _context.Class.Find(id);
            if (cls == null)
                return NotFound();

            cls.Name = name;
            _context.Update(cls);
            _context.SaveChanges();

            return RedirectToAction("Index", new { classId = id });
        }

        [HttpPost]
        public IActionResult DeleteSubject(int classId, int subjectId)
        {
            var classSubject = _context.ClassSubject
                .FirstOrDefault(cs => cs.ClassId == classId && cs.SubjectId == subjectId);
            if (classSubject == null)
                return NotFound();

            _context.ClassSubject.Remove(classSubject);
            _context.SaveChanges();

            return RedirectToAction("Index", new { classId });
        }

        [HttpPost]
        public IActionResult BindSubject(int classId, int subjectId)
        {
            // Перевіряємо, чи вже існує такий зв'язок
            var exists = _context.ClassSubject
                .Any(cs => cs.ClassId == classId && cs.SubjectId == subjectId);
            if (!exists)
            {
                var classSubject = new ClassSubject
                {
                    ClassId = classId,
                    SubjectId = subjectId
                };
                _context.ClassSubject.Add(classSubject);
                _context.SaveChanges();
            }

            return RedirectToAction("Index", new { classId });
        }

        public IActionResult EditStudents(int classId)
        {
            var cls = _context.Class.Find(classId);
            if (cls == null)
                return NotFound();

            var students = _context.Student
                .Where(s => s.ClassId == classId)
                .ToList();

            ViewData["Class"] = cls;
            return View(students);
        }

        [HttpPost]
        public IActionResult EditStudent(int id, string name, string surname, string patronymic, int classId)
        {
            var student = _context.Student.Find(id);
            if (student == null)
                return NotFound();

            student.Name = name;
            student.Surname = surname;
            student.Patronymic = patronymic;
            _context.Update(student);
            _context.SaveChanges();

            return RedirectToAction("EditStudents", new { classId });
        }

        [HttpPost]
        public IActionResult DeleteStudent(int id, int classId)
        {
            var student = _context.Student.Find(id);
            if (student == null)
                return NotFound();

            _context.Student.Remove(student);
            _context.SaveChanges();

            return RedirectToAction("EditStudents", new { classId });
        }

        [HttpPost]
        public IActionResult AddStudent(int classId, string surname, string name, string patronymic)
        {
            if (string.IsNullOrWhiteSpace(surname) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(patronymic))
                return RedirectToAction("EditStudents", new { classId });

            var student = new Student
            {
                ClassId = classId,
                Surname = surname,
                Name = name,
                Patronymic = patronymic
            };
            _context.Student.Add(student);
            _context.SaveChanges();

            return RedirectToAction("EditStudents", new { classId });
        }

        public IActionResult CreateClass()
        {
            return View();
        }

        [HttpPost]
        public IActionResult CreateClass(string className)
        {
            if (string.IsNullOrWhiteSpace(className))
                return View();

            var teacher = _context.Teacher.FirstOrDefault();
            if (teacher == null)
                return RedirectToAction("SelectClasses");

            if (_context.Class.Any(c => c.Name == className && c.TeacherId == teacher.Id))
            {
                ViewBag.Error = "Клас з такою назвою вже існує.";
                return View();
            }

            var newClass = new Class
            {
                Name = className,
                TeacherId = teacher.Id
            };
            _context.Class.Add(newClass);
            _context.SaveChanges();

            return RedirectToAction("Index", new { classId = newClass.Id });
        }

        [HttpPost]
        public IActionResult ImportClasses(IFormFile importFile)
        {
            var teacher = _context.Teacher.FirstOrDefault();
            if (teacher == null || importFile == null || importFile.Length == 0)
                return RedirectToAction("SelectClasses");

            using (var reader = new StreamReader(importFile.OpenReadStream()))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine()?.Trim();
                    if (!string.IsNullOrWhiteSpace(line) &&
                        !_context.Class.Any(c => c.Name == line && c.TeacherId == teacher.Id))
                    {
                        _context.Class.Add(new Class { Name = line, TeacherId = teacher.Id });
                    }
                }
                _context.SaveChanges();
            }

            return RedirectToAction("SelectClasses");
        }
    }
}