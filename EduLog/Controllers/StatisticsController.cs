using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using EduLog.Data;
using EduLog.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduLog.Controllers
{
    [Authorize]
    public class StatisticsController : Controller
    {
        private readonly EduLogContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public StatisticsController(EduLogContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private async Task<Teacher?> GetCurrentTeacherAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.TeacherId == null) return null;
            return _context.Teacher.FirstOrDefault(t => t.Id == user.TeacherId);
        }

        // Initial shell with filter dropdowns. Data is loaded via AJAX from Data().
        public async Task<IActionResult> Index(int? classId)
        {
            var teacher = await GetCurrentTeacherAsync();
            if (teacher == null) return RedirectToAction("Index", "Profile");

            var classes = _context.Class
                .Where(c => c.ClassSubjects.Any(cs => cs.Subject.TeacherId == teacher.Id))
                .OrderBy(c => c.Name)
                .ToList();

            var selectedClass = classId.HasValue
                ? classes.FirstOrDefault(c => c.Id == classId)
                : classes.FirstOrDefault();

            var subjectsForClass = selectedClass == null
                ? new List<Subject>()
                : _context.ClassSubject
                    .Where(cs => cs.ClassId == selectedClass.Id && cs.Subject.TeacherId == teacher.Id)
                    .Select(cs => cs.Subject)
                    .OrderBy(s => s.Name)
                    .ToList();

            var years = _context.AcademicYear
                .OrderByDescending(y => y.StartDate)
                .ToList();

            ViewBag.Classes = classes;
            ViewBag.SelectedClass = selectedClass;
            ViewBag.Subjects = subjectsForClass;
            ViewBag.AcademicYears = years;

            return View();
        }

        // AJAX: return subjects for a class (filtered by current teacher).
        [HttpGet]
        public async Task<IActionResult> SubjectsForClass(int classId)
        {
            var teacher = await GetCurrentTeacherAsync();
            if (teacher == null) return Forbid();

            var subjects = _context.ClassSubject
                .Where(cs => cs.ClassId == classId && cs.Subject.TeacherId == teacher.Id)
                .Select(cs => new { id = cs.Subject.Id, name = cs.Subject.Name })
                .OrderBy(s => s.name)
                .ToList();
            return Json(subjects);
        }

        // AJAX: aggregate statistics for filters.
        [HttpGet]
        public async Task<IActionResult> Data(int classId, int? subjectId, DateTime? dateFrom, DateTime? dateTo, int? academicYearId)
        {
            var teacher = await GetCurrentTeacherAsync();
            if (teacher == null) return Forbid();

            var aggregate = await BuildStatsAsync(teacher, classId, subjectId, dateFrom, dateTo, academicYearId);
            return Json(aggregate);
        }

        // CSV download of the students table for current filters.
        [HttpGet]
        public async Task<IActionResult> ExportCsv(int classId, int? subjectId, DateTime? dateFrom, DateTime? dateTo, int? academicYearId)
        {
            var teacher = await GetCurrentTeacherAsync();
            if (teacher == null) return Forbid();

            var data = await BuildStatsAsync(teacher, classId, subjectId, dateFrom, dateTo, academicYearId);

            var sb = new StringBuilder();
            sb.Append('﻿'); // UTF-8 BOM for Excel
            sb.AppendLine("Ім'я;Середня оцінка;Кількість оцінок;Відвідуваність (%);Тренд");
            foreach (var s in data.Students)
            {
                string trend = s.Trend switch { 1 => "↑", -1 => "↓", _ => "→" };
                sb.AppendLine($"{EscapeCsv(s.Name)};{s.Avg.ToString("0.0", CultureInfo.InvariantCulture)};{s.GradeCount};{s.AttendancePct.ToString("0.0", CultureInfo.InvariantCulture)};{trend}");
            }
            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var fileName = $"statistics_{classId}_{DateTime.Now:yyyy-MM-dd}.csv";
            return File(bytes, "text/csv; charset=utf-8", fileName);
        }

        private static string EscapeCsv(string s) =>
            s.Contains(';') || s.Contains('"') || s.Contains('\n')
                ? "\"" + s.Replace("\"", "\"\"") + "\""
                : s;

        // Legacy Excel export (kept).
        [HttpGet]
        public async Task<IActionResult> ExportExcel(int classId)
        {
            var teacher = await GetCurrentTeacherAsync();
            if (teacher == null) return RedirectToAction("Index", "Profile");

            var cls = _context.Class.FirstOrDefault(c => c.Id == classId);
            if (cls == null) return NotFound();

            var students = _context.Student
                .Where(s => s.ClassId == classId)
                .OrderBy(s => s.Surname).ThenBy(s => s.Name)
                .ToList();

            var subjects = _context.ClassSubject
                .Where(cs => cs.ClassId == classId && cs.Subject.TeacherId == teacher.Id)
                .Select(cs => cs.Subject)
                .ToList();

            var studentIds = students.Select(s => s.Id).ToList();
            var subjectIds = subjects.Select(s => s.Id).ToList();

            var allGrades = _context.Grade
                .Where(g => studentIds.Contains(g.StudentId) && subjectIds.Contains(g.SubjectId) && g.Value != null && g.Value > 0)
                .ToList();

            var now = DateTime.Now;
            var yearStart = new DateTime(now.Month >= 9 ? now.Year : now.Year - 1, 9, 1);
            var allAbsences = _context.Absence
                .Where(a => studentIds.Contains(a.StudentId) && subjectIds.Contains(a.SubjectId) && a.Date >= yearStart)
                .ToList();

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add($"Звіт {cls.Name}");
            ws.Cell(1, 1).Value = $"Звіт: клас {cls.Name}";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;
            ws.Range(1, 1, 1, subjects.Count + 3).Merge();

            int row = 3;
            ws.Cell(row, 1).Value = "№";
            ws.Cell(row, 2).Value = "Учень";
            for (int i = 0; i < subjects.Count; i++)
                ws.Cell(row, 3 + i).Value = subjects[i].Name;
            ws.Cell(row, 3 + subjects.Count).Value = "Середній бал";
            ws.Cell(row, 4 + subjects.Count).Value = "Пропуски";

            var headerRange = ws.Range(row, 1, row, 4 + subjects.Count);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
            headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            row = 4;
            int num = 1;
            foreach (var st in students)
            {
                ws.Cell(row, 1).Value = num++;
                ws.Cell(row, 2).Value = $"{st.Surname} {st.Name} {st.Patronymic}";

                var studentGrades = allGrades.Where(g => g.StudentId == st.Id).ToList();
                double totalAvg = 0;
                int subCount = 0;

                for (int i = 0; i < subjects.Count; i++)
                {
                    var sg = studentGrades.Where(g => g.SubjectId == subjects[i].Id).ToList();
                    if (sg.Any())
                    {
                        var avg = Math.Round(sg.Average(g => g.Value ?? 0), 1);
                        ws.Cell(row, 3 + i).Value = avg;
                        totalAvg += avg;
                        subCount++;
                    }
                    else
                    {
                        ws.Cell(row, 3 + i).Value = "—";
                    }
                }

                ws.Cell(row, 3 + subjects.Count).Value = subCount > 0 ? Math.Round(totalAvg / subCount, 1) : 0;
                ws.Cell(row, 4 + subjects.Count).Value = allAbsences.Count(a => a.StudentId == st.Id);
                row++;
            }

            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;
            var fileName = $"Zvit_{cls.Name}_{DateTime.Now:yyyy-MM-dd}.xlsx";
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        // Експорт звіту адаптивного навчання (BKT) по класу.
        // Рядки — учні, стовпці — TopicTag, у комірках — ProbabilityLearned у %, з кольоровою підсвіткою.
        [HttpGet]
        public async Task<IActionResult> ExportAdaptiveReport(int classId)
        {
            var teacher = await GetCurrentTeacherAsync();
            if (teacher == null) return RedirectToAction("Index", "Profile");

            var cls = await _context.Class.FirstOrDefaultAsync(c => c.Id == classId);
            if (cls == null) return NotFound();

            var students = await _context.Student
                .Where(s => s.ClassId == classId)
                .OrderBy(s => s.Surname).ThenBy(s => s.Name)
                .ToListAsync();

            // Предмети класу, які веде поточний учитель — щоб не зливати "чужі" теми.
            var subjectIds = await _context.ClassSubject
                .Where(cs => cs.ClassId == classId && cs.Subject.TeacherId == teacher.Id)
                .Select(cs => cs.SubjectId)
                .ToListAsync();

            var studentIds = students.Select(s => s.Id).ToList();

            // Усі стани знань: фільтруємо в БД, агрегуємо у пам'яті.
            var states = await _context.StudentKnowledgeState
                .Where(s => studentIds.Contains(s.StudentId) && subjectIds.Contains(s.SubjectId))
                .ToListAsync();

            // Унікальні TopicTag, що реально мають хоч одне спостереження по цьому класу.
            var topics = states
                .Select(s => s.TopicTag)
                .Distinct()
                .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // (StudentId, TopicTag) → середнє pL (по предметах, якщо одна тема в кількох).
            var lookup = states
                .GroupBy(s => new { s.StudentId, s.TopicTag })
                .ToDictionary(g => g.Key, g => g.Average(x => x.ProbabilityLearned));

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add($"Адаптивне ДЗ {cls.Name}");

            // Заголовок-рядок.
            ws.Cell(1, 1).Value = $"Динаміка успішності — {cls.Name}";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;
            int titleSpan = Math.Max(2, topics.Count + 2);
            ws.Range(1, 1, 1, titleSpan).Merge();

            // Шапка таблиці.
            int headerRow = 3;
            ws.Cell(headerRow, 1).Value = "№";
            ws.Cell(headerRow, 2).Value = "Учень";
            for (int i = 0; i < topics.Count; i++)
                ws.Cell(headerRow, 3 + i).Value = topics[i];

            var headerRange = ws.Range(headerRow, 1, headerRow, 2 + topics.Count);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
            headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            // Кольорова шкала згідно з ТЗ.
            var green = XLColor.FromHtml("#C6EFCE");
            var yellow = XLColor.FromHtml("#FFEB9C");
            var red = XLColor.FromHtml("#FFC7CE");

            int row = headerRow + 1;
            int num = 1;
            foreach (var st in students)
            {
                ws.Cell(row, 1).Value = num++;
                ws.Cell(row, 2).Value = $"{st.Surname} {st.Name} {st.Patronymic}".Trim();

                for (int i = 0; i < topics.Count; i++)
                {
                    var key = new { StudentId = st.Id, TopicTag = topics[i] };
                    if (lookup.TryGetValue(key, out var pl))
                    {
                        var percent = Math.Round(pl * 100.0, 0);
                        var cell = ws.Cell(row, 3 + i);
                        cell.Value = percent;
                        cell.Style.NumberFormat.Format = "0\"%\"";

                        if (percent >= 80) cell.Style.Fill.BackgroundColor = green;
                        else if (percent >= 40) cell.Style.Fill.BackgroundColor = yellow;
                        else cell.Style.Fill.BackgroundColor = red;
                    }
                    else
                    {
                        ws.Cell(row, 3 + i).Value = "—";
                    }
                }
                row++;
            }

            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;
            var fileName = $"Adaptive_{cls.Name}_{DateTime.Now:yyyy-MM-dd}.xlsx";
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        // ───────── Aggregation ─────────

        private class StatsResult
        {
            public TilesDto Tiles { get; set; } = new();
            public LineChartDto MonthlyAvg { get; set; } = new();
            public int[] Histogram { get; set; } = new int[12];
            public AttendanceDto Attendance { get; set; } = new();
            public List<StudentRowDto> Students { get; set; } = new();
        }
        private class TilesDto
        {
            public double AvgGrade { get; set; }
            public int High { get; set; }   // >=10
            public int Mid { get; set; }    // 7..9
            public int Low { get; set; }    // <7
            public double AttendancePct { get; set; }
            public int HwSubmitted { get; set; }
            public int HwNotSubmitted { get; set; }
        }
        private class LineChartDto
        {
            public List<string> Labels { get; set; } = new();
            public List<double> Data { get; set; } = new();
        }
        private class AttendanceDto
        {
            public int Present { get; set; }
            public int Absent { get; set; }
        }
        private class StudentRowDto
        {
            public int StudentId { get; set; }
            public string Name { get; set; } = string.Empty;
            public double Avg { get; set; }
            public int GradeCount { get; set; }
            public double AttendancePct { get; set; }
            public int Trend { get; set; }
        }

        private async Task<StatsResult> BuildStatsAsync(Teacher teacher, int classId, int? subjectId,
            DateTime? dateFrom, DateTime? dateTo, int? academicYearId)
        {
            // Resolve effective date range from explicit dates / academic year / current academic year.
            if (academicYearId.HasValue)
            {
                var year = await _context.AcademicYear.FirstOrDefaultAsync(y => y.Id == academicYearId.Value);
                if (year != null)
                {
                    dateFrom ??= year.StartDate;
                    dateTo ??= year.EndDate;
                }
            }
            if (!dateFrom.HasValue || !dateTo.HasValue)
            {
                var now = DateTime.Now;
                dateFrom ??= new DateTime(now.Month >= 9 ? now.Year : now.Year - 1, 9, 1);
                dateTo ??= now;
            }
            var from = dateFrom.Value.Date;
            var to = dateTo.Value.Date.AddDays(1).AddTicks(-1);

            var students = await _context.Student
                .Where(s => s.ClassId == classId)
                .OrderBy(s => s.Surname).ThenBy(s => s.Name)
                .ToListAsync();
            var studentIds = students.Select(s => s.Id).ToList();

            List<int> subjectIds;
            if (subjectId.HasValue)
            {
                subjectIds = new List<int> { subjectId.Value };
            }
            else
            {
                subjectIds = await _context.ClassSubject
                    .Where(cs => cs.ClassId == classId && cs.Subject.TeacherId == teacher.Id)
                    .Select(cs => cs.SubjectId)
                    .ToListAsync();
            }

            var grades = await _context.Grade
                .Where(g => studentIds.Contains(g.StudentId)
                            && subjectIds.Contains(g.SubjectId)
                            && g.Value != null && g.Value > 0
                            && g.Date >= from && g.Date <= to)
                .ToListAsync();

            var absences = await _context.Absence
                .Where(a => studentIds.Contains(a.StudentId)
                            && subjectIds.Contains(a.SubjectId)
                            && a.Date >= from && a.Date <= to)
                .ToListAsync();

            // Homework submissions in window
            var hwMaterialIds = await _context.LessonMaterial
                .Where(m => m.ClassSubjectClassId == classId
                            && subjectIds.Contains(m.ClassSubjectSubjectId)
                            && m.Type == MaterialType.Homework
                            && m.Date >= from && m.Date <= to)
                .Select(m => m.Id)
                .ToListAsync();

            int hwExpected = hwMaterialIds.Count * students.Count;
            int hwSubmitted = await _context.HomeworkSubmission
                .CountAsync(hs => hwMaterialIds.Contains(hs.LessonMaterialId)
                                  && studentIds.Contains(hs.StudentId)
                                  && hs.Status != SubmissionStatus.NotSubmitted);
            int hwNot = Math.Max(0, hwExpected - hwSubmitted);

            // Class avg
            double classAvg = grades.Any() ? Math.Round(grades.Average(g => g.Value ?? 0), 2) : 0;

            // Per-student aggregations
            var studentRows = new List<StudentRowDto>();
            foreach (var st in students)
            {
                var sg = grades.Where(g => g.StudentId == st.Id).ToList();
                double avg = sg.Any() ? Math.Round(sg.Average(g => g.Value ?? 0), 2) : 0;

                // Attendance pct per student: rough — share of (class lessons in window) without absence.
                int absCount = absences.Count(a => a.StudentId == st.Id);
                int classDays = (int)((to - from).TotalDays / 7 * 5);
                if (classDays <= 0) classDays = 1;
                int slots = classDays * Math.Max(1, subjectIds.Count);
                double attPct = slots > 0 ? Math.Round((1.0 - (double)absCount / slots) * 100, 1) : 100;
                if (attPct < 0) attPct = 0;
                if (attPct > 100) attPct = 100;

                int trend = 0;
                var ordered = sg.OrderBy(g => g.Date).ThenBy(g => g.Id).Select(g => (double)(g.Value ?? 0)).ToList();
                if (ordered.Count >= 4)
                {
                    int half = ordered.Count / 2;
                    double newest = ordered.Skip(ordered.Count - half).Average();
                    double oldest = ordered.Take(half).Average();
                    if (newest - oldest >= 0.5) trend = 1;
                    else if (oldest - newest >= 0.5) trend = -1;
                }
                else if (ordered.Count >= 2)
                {
                    double newest = ordered[^1];
                    double oldest = ordered[0];
                    if (newest - oldest >= 0.5) trend = 1;
                    else if (oldest - newest >= 0.5) trend = -1;
                }

                studentRows.Add(new StudentRowDto
                {
                    StudentId = st.Id,
                    Name = $"{st.Surname} {st.Name}",
                    Avg = avg,
                    GradeCount = sg.Count,
                    AttendancePct = attPct,
                    Trend = trend
                });
            }

            int high = studentRows.Count(r => r.Avg >= 10);
            int mid = studentRows.Count(r => r.Avg >= 7 && r.Avg < 10);
            int low = studentRows.Count(r => r.Avg < 7 && r.GradeCount > 0);

            // Class-level attendance
            int totalAbs = absences.Count;
            int totalClassDays = (int)((to - from).TotalDays / 7 * 5);
            if (totalClassDays <= 0) totalClassDays = 1;
            int totalSlots = totalClassDays * students.Count * Math.Max(1, subjectIds.Count);
            double classAttPct = totalSlots > 0 ? Math.Round((1.0 - (double)totalAbs / totalSlots) * 100, 1) : 100;
            if (classAttPct < 0) classAttPct = 0;
            if (classAttPct > 100) classAttPct = 100;

            // Histogram 1..12
            var hist = new int[12];
            foreach (var g in grades)
            {
                int v = g.Value ?? 0;
                if (v >= 1 && v <= 12) hist[v - 1]++;
            }

            // Monthly line: months in range, avg per month
            var monthly = new LineChartDto();
            var cursor = new DateTime(from.Year, from.Month, 1);
            var endCursor = new DateTime(to.Year, to.Month, 1);
            while (cursor <= endCursor)
            {
                var m = cursor;
                var inMonth = grades.Where(g => g.Date.Year == m.Year && g.Date.Month == m.Month).ToList();
                monthly.Labels.Add(m.ToString("MM.yyyy"));
                monthly.Data.Add(inMonth.Any() ? Math.Round(inMonth.Average(g => g.Value ?? 0), 2) : 0);
                cursor = cursor.AddMonths(1);
            }

            return new StatsResult
            {
                Tiles = new TilesDto
                {
                    AvgGrade = classAvg,
                    High = high, Mid = mid, Low = low,
                    AttendancePct = classAttPct,
                    HwSubmitted = hwSubmitted,
                    HwNotSubmitted = hwNot
                },
                MonthlyAvg = monthly,
                Histogram = hist,
                Attendance = new AttendanceDto
                {
                    Present = Math.Max(0, totalSlots - totalAbs),
                    Absent = totalAbs
                },
                Students = studentRows
            };
        }
    }
}
