using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using EduLog.Data;
using EduLog.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EduLog.Services
{
    public interface ISchedulerService
    {
        Task<ScheduleGenerationResult> GenerateScheduleAsync(
            int schoolId,
            int academicYearId,
            SchedulerMode mode,
            SchedulerConfigOptions options,
            CancellationToken cancellationToken = default);

        Task<bool> ValidateScheduleDataAsync(int schoolId, CancellationToken cancellationToken = default);

        Task<List<SchedulerModeInfo>> GetAvailableModesAsync(CancellationToken cancellationToken = default);

        Task<byte[]> ExportScheduleAsync(
            int schoolId,
            int academicYearId,
            string format = "json",
            CancellationToken cancellationToken = default);

        Task<List<ScheduleSlot>> ImportScheduleAsync(
            Stream fileStream,
            int schoolId,
            int academicYearId,
            CancellationToken cancellationToken = default);
    }

    public sealed class SchedulerService : ISchedulerService
    {
        private const string ApiPrefix = "api";
        private const string CoursesRoute = "/api/courses";
        private const string TeachersRoute = "/api/teachers";
        private const string GroupsRoute = "/api/groups";
        private const string ClassroomsRoute = "/api/classrooms";
        private const string TimeslotsRoute = "/api/timeslots";
        private const string AssignmentsRoute = "/api/schedule/assignments";
        private const string GenerateRoute = "/api/schedule/generate";
        private const string StatusRoute = "/api/schedule/status";
        private const string ExportRoute = "/api/schedule/export";
        private const string LessonsExportRoute = "/api/schedule/export/lessons/csv";
        private const string FilesRoute = "/api/schedule/files";
        private const string AssignmentImportRoute = "/api/schedule/assignments/import/csv";

        private const string SchoolIdClaimSeparator = "_";
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly HttpClient _httpClient;
        private readonly EduLogContext _context;
        private readonly ITenantService _tenantService;
        private readonly ILogger<SchedulerService> _logger;
        private readonly SchedulerApiOptions _options;

        public SchedulerService(
            HttpClient httpClient,
            EduLogContext context,
            ITenantService tenantService,
            ILogger<SchedulerService> logger,
            IOptions<SchedulerApiOptions> options)
        {
            _httpClient = httpClient;
            _context = context;
            _tenantService = tenantService;
            _logger = logger;
            _options = options.Value;
        }

        public async Task<ScheduleGenerationResult> GenerateScheduleAsync(
            int schoolId,
            int academicYearId,
            SchedulerMode mode,
            SchedulerConfigOptions options,
            CancellationToken cancellationToken = default)
        {
            if (!await ValidateScheduleDataAsync(schoolId, cancellationToken))
            {
                return new ScheduleGenerationResult
                {
                    Success = false,
                    Warnings = new List<string> { "Недостатньо даних для генерації розкладу." }
                };
            }

            var schoolPayload = await BuildSchoolPayloadAsync(schoolId, academicYearId, options, cancellationToken);
            var remoteMappings = await SyncReferenceDataAsync(schoolPayload, cancellationToken);
            await SyncAssignmentsAsync(schoolPayload, remoteMappings, cancellationToken);

            var remoteRequest = new RemoteScheduleGenerateRequest
            {
                Iterations = Math.Clamp(options.PlanningPeriodWeeks * 1000, 1000, 10000),
                UseExisting = mode == SchedulerMode.Append,
                PreserveLocked = mode != SchedulerMode.Dense,
                ModelVersion = null
            };

            var generationResponse = await PostJsonAsync<RemoteScheduleGenerationStatus>(
                GenerateRoute,
                remoteRequest,
                cancellationToken);

            var completedStatus = await WaitForGenerationCompletionAsync(generationResponse.Id, cancellationToken);
            if (!string.Equals(completedStatus.Status, "completed", StringComparison.OrdinalIgnoreCase))
            {
                return new ScheduleGenerationResult
                {
                    Success = false,
                    Warnings = new List<string> { completedStatus.ErrorMessage ?? "Генерацію не вдалося завершити." },
                    Statistics = new ScheduleStatisticsDto
                    {
                        HardViolations = completedStatus.HardViolations,
                        SoftViolations = completedStatus.SoftViolations
                    }
                };
            }

            var exportBytes = await DownloadCurrentScheduleBytesAsync("json", cancellationToken);
            return await BuildGenerationResultAsync(exportBytes, remoteMappings, schoolId, academicYearId, cancellationToken);
        }

        public async Task<bool> ValidateScheduleDataAsync(int schoolId, CancellationToken cancellationToken = default)
        {
            var schoolPayload = await BuildSchoolPayloadAsync(schoolId, academicYearId: 0, new SchedulerConfigOptions(), cancellationToken);
            var validation = ValidatePayload(schoolPayload);
            return validation.IsValid;
        }

        public Task<List<SchedulerModeInfo>> GetAvailableModesAsync(CancellationToken cancellationToken = default)
        {
            var modes = new List<SchedulerModeInfo>
            {
                new() { Mode = SchedulerMode.Balanced, Name = "Balanced", Description = "Balanced distribution across classes and teachers." },
                new() { Mode = SchedulerMode.Dense, Name = "Dense", Description = "Favors compact timetables with fewer gaps." },
                new() { Mode = SchedulerMode.Append, Name = "Append", Description = "Preserves existing slots and appends new ones." },
            };

            return Task.FromResult(modes);
        }

        public async Task<byte[]> ExportScheduleAsync(
            int schoolId,
            int academicYearId,
            string format = "json",
            CancellationToken cancellationToken = default)
        {
            _ = schoolId;
            _ = academicYearId;

            if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
            {
                return await _httpClient.GetByteArrayAsync(LessonsExportRoute, cancellationToken);
            }

            var exportResponse = await PostJsonAsync<RemoteScheduleExportResponse>(
                ExportRoute,
                new { description = $"EduLog export for school {schoolId}, year {academicYearId}" },
                cancellationToken);

            return await _httpClient.GetByteArrayAsync($"{FilesRoute}/{Uri.EscapeDataString(exportResponse.Filename)}/download", cancellationToken);
        }

        public async Task<List<ScheduleSlot>> ImportScheduleAsync(
            Stream fileStream,
            int schoolId,
            int academicYearId,
            CancellationToken cancellationToken = default)
        {
            if (fileStream == null)
            {
                throw new ArgumentNullException(nameof(fileStream));
            }

            using var memoryStream = new MemoryStream();
            await fileStream.CopyToAsync(memoryStream, cancellationToken);
            var bytes = memoryStream.ToArray();

            if (LooksLikeJson(bytes))
            {
                return await ParseExportPayloadAsync(bytes, schoolId, academicYearId, cancellationToken);
            }

            using var formData = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(bytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
            formData.Add(fileContent, "file", "schedule.csv");

            using var response = await _httpClient.PostAsync(AssignmentImportRoute, formData, cancellationToken);
            await EnsureSuccessAsync(response, "Імпорт CSV до Python API не вдалося виконати.", cancellationToken);

            var generated = await GenerateScheduleAsync(
                schoolId,
                academicYearId,
                SchedulerMode.Balanced,
                new SchedulerConfigOptions(),
                cancellationToken);

            return generated.Slots.Select(slot => new ScheduleSlot
            {
                SchoolId = slot.SchoolId,
                AcademicYearId = slot.AcademicYearId,
                ClassId = slot.ClassId,
                SubjectId = slot.SubjectId,
                TeacherId = slot.TeacherId,
                DayOfWeek = slot.DayOfWeek,
                LessonNumber = slot.LessonNumber,
                Room = slot.Room
            }).ToList();
        }

        private async Task<ScheduleGenerationResult> BuildGenerationResultAsync(
            byte[] exportBytes,
            RemoteIdMappings mappings,
            int schoolId,
            int academicYearId,
            CancellationToken cancellationToken)
        {
            using var document = JsonDocument.Parse(exportBytes);
            var root = document.RootElement;
            var result = new ScheduleGenerationResult
            {
                Success = true
            };

            if (root.TryGetProperty("meta", out var metaElement))
            {
                result.Statistics = new ScheduleStatisticsDto
                {
                    TotalSlots = metaElement.TryGetProperty("classes_count", out var countElement) ? countElement.GetInt32() : 0,
                    ScheduledSlots = metaElement.TryGetProperty("classes_count", out var scheduledElement) ? scheduledElement.GetInt32() : 0,
                    OverallScore = metaElement.TryGetProperty("overall_score", out var scoreElement) ? scoreElement.GetDouble() : 0,
                    OccupancyRate = metaElement.TryGetProperty("occupancy_rate", out var occupancyElement) ? occupancyElement.GetDouble() : 0,
                    HardViolations = metaElement.TryGetProperty("hard_violations", out var hardElement) ? hardElement.GetInt32() : 0,
                    SoftViolations = metaElement.TryGetProperty("soft_violations", out var softElement) ? softElement.GetInt32() : 0,
                };
            }

            if (!root.TryGetProperty("classes", out var classesElement) || classesElement.ValueKind != JsonValueKind.Array)
            {
                result.Success = false;
                result.Warnings.Add("Python API returned an empty export payload.");
                return result;
            }

            foreach (var item in classesElement.EnumerateArray())
            {
                var groupId = DecodeId(item, "group_code", RemotePrefixes.Group);
                var courseId = DecodeId(item, "course_code", RemotePrefixes.Course);
                var teacherId = ResolveTeacherId(item, mappings);

                if (!groupId.HasValue || !courseId.HasValue || !teacherId.HasValue)
                {
                    result.Warnings.Add("Skipped one exported slot because a local mapping could not be resolved.");
                    continue;
                }

                var dayOfWeek = item.TryGetProperty("day_of_week", out var dayElement) ? dayElement.GetInt32() + 1 : 1;
                var lessonNumber = item.TryGetProperty("period_number", out var periodElement) ? periodElement.GetInt32() : 1;

                result.Slots.Add(new ScheduleSlotDto
                {
                    SchoolId = schoolId,
                    AcademicYearId = academicYearId,
                    ClassId = groupId.Value,
                    SubjectId = courseId.Value,
                    TeacherId = teacherId.Value,
                    DayOfWeek = dayOfWeek,
                    LessonNumber = lessonNumber,
                    Room = item.TryGetProperty("classroom_code", out var roomElement) ? roomElement.GetString() : null
                });
            }

            if (result.Slots.Count == 0)
            {
                result.Success = false;
                result.Warnings.Add("Python API did not return any schedule slots.");
            }

            return result;
        }

        private async Task<List<ScheduleSlot>> ParseExportPayloadAsync(
            byte[] bytes,
            int schoolId,
            int academicYearId,
            CancellationToken cancellationToken)
        {
            _ = cancellationToken;

            var remoteMappings = await LoadRemoteMappingsAsync(cancellationToken);
            var result = await BuildGenerationResultAsync(bytes, remoteMappings, schoolId, academicYearId, cancellationToken);

            return result.Slots.Select(slot => new ScheduleSlot
            {
                SchoolId = slot.SchoolId,
                AcademicYearId = slot.AcademicYearId,
                ClassId = slot.ClassId,
                SubjectId = slot.SubjectId,
                TeacherId = slot.TeacherId,
                DayOfWeek = slot.DayOfWeek,
                LessonNumber = slot.LessonNumber,
                Room = slot.Room
            }).ToList();
        }

        private async Task<RemoteIdMappings> SyncReferenceDataAsync(
            SchoolPayload schoolPayload,
            CancellationToken cancellationToken)
        {
            var remoteMappings = new RemoteIdMappings();

            foreach (var teacher in schoolPayload.Teachers)
            {
                remoteMappings.TeacherIds[teacher.LocalId] = await EnsureRemoteTeacherAsync(teacher, cancellationToken);
            }

            foreach (var subject in schoolPayload.Subjects)
            {
                remoteMappings.CourseIds[subject.LocalId] = await EnsureRemoteCourseAsync(subject, cancellationToken);
            }

            foreach (var classItem in schoolPayload.Classes)
            {
                remoteMappings.GroupIds[classItem.LocalId] = await EnsureRemoteGroupAsync(classItem, cancellationToken);
            }

            foreach (var room in schoolPayload.Classrooms)
            {
                remoteMappings.ClassroomIds[room.LocalId] = await EnsureRemoteClassroomAsync(room, cancellationToken);
            }

            foreach (var timeslot in schoolPayload.Timeslots)
            {
                var key = $"{timeslot.DayOfWeek}:{timeslot.PeriodNumber}";
                remoteMappings.TimeslotIds[key] = await EnsureRemoteTimeslotAsync(timeslot, cancellationToken);
            }

            return remoteMappings;
        }

        private async Task SyncAssignmentsAsync(
            SchoolPayload schoolPayload,
            RemoteIdMappings mappings,
            CancellationToken cancellationToken)
        {
            foreach (var relation in schoolPayload.ClassSubjects)
            {
                if (!mappings.CourseIds.TryGetValue(relation.SubjectId, out var remoteCourseId) ||
                    !mappings.GroupIds.TryGetValue(relation.ClassId, out var remoteGroupId))
                {
                    continue;
                }

                var teacherId = relation.SubjectTeacherId ?? relation.ClassTeacherId;
                if (!teacherId.HasValue || !mappings.TeacherIds.TryGetValue(teacherId.Value, out var remoteTeacherId))
                {
                    continue;
                }

                await EnsureRemoteAssignmentAsync(remoteCourseId, remoteTeacherId, remoteGroupId, cancellationToken);
            }
        }

        private async Task<SchoolPayload> BuildSchoolPayloadAsync(
            int schoolId,
            int academicYearId,
            SchedulerConfigOptions options,
            CancellationToken cancellationToken)
        {
            _ = academicYearId;

            var classes = await _context.Class
                .Include(item => item.ClassSubjects)
                .ThenInclude(relation => relation.Subject)
                .Where(item => item.SchoolId == schoolId)
                .OrderBy(item => item.Name)
                .ToListAsync(cancellationToken);

            var subjects = await _context.Subject
                .Where(item => item.SchoolId == schoolId)
                .OrderBy(item => item.Name)
                .ToListAsync(cancellationToken);

            var teachers = await _context.Teacher
                .Where(item => item.SchoolId == schoolId)
                .OrderBy(item => item.Surname)
                .ThenBy(item => item.Name)
                .ToListAsync(cancellationToken);

            var classSubjects = await _context.ClassSubject
                .Include(item => item.Subject)
                .Where(item => item.SchoolId == schoolId)
                .ToListAsync(cancellationToken);

            var schoolPayload = new SchoolPayload
            {
                SchoolId = schoolId,
                AcademicYearId = academicYearId,
                Classes = classes.Select(item => new SchoolClassPayload
                {
                    LocalId = item.Id,
                    Code = BuildCode(RemotePrefixes.Group, item.Id),
                    Name = item.Name,
                    Year = ExtractYearFromClassName(item.Name),
                    StudentsCount = 25,
                    TeacherId = item.TeacherId
                }).ToList(),
                Subjects = subjects.Select(item => new SchoolSubjectPayload
                {
                    LocalId = item.Id,
                    Code = BuildCode(RemotePrefixes.Course, item.Id),
                    Name = item.Name,
                    Credits = 3,
                    HoursPerWeek = 2,
                    Difficulty = 1,
                    TeacherId = item.TeacherId
                }).ToList(),
                Teachers = teachers.Select(item => new SchoolTeacherPayload
                {
                    LocalId = item.Id,
                    Code = BuildCode(RemotePrefixes.Teacher, item.Id),
                    FullName = BuildTeacherFullName(item),
                    Department = null,
                    MaxHoursPerWeek = 18
                }).ToList(),
                Classrooms = BuildClassroomsPayload(classes.Count, teachers.Count),
                Timeslots = BuildTimeslotsPayload(options.MaxLessonsPerDay),
                ClassSubjects = classSubjects.Select(relation => new SchoolClassSubjectPayload
                {
                    ClassId = relation.ClassId,
                    SubjectId = relation.SubjectId,
                    SubjectTeacherId = relation.Subject?.TeacherId,
                    ClassTeacherId = classes.FirstOrDefault(item => item.Id == relation.ClassId)?.TeacherId
                }).ToList()
            };

            return schoolPayload;
        }

        private static List<SchoolClassroomPayload> BuildClassroomsPayload(int classCount, int teacherCount)
        {
            var rooms = new List<SchoolClassroomPayload>();
            var roomCount = Math.Max(Math.Max(classCount, teacherCount), 1);

            for (var index = 1; index <= roomCount; index++)
            {
                rooms.Add(new SchoolClassroomPayload
                {
                    LocalId = index,
                    Code = BuildCode(RemotePrefixes.Room, index),
                    Capacity = 35,
                    ClassroomType = "general",
                    HasProjector = true,
                    HasComputers = false
                });
            }

            return rooms;
        }

        private static List<SchoolTimeslotPayload> BuildTimeslotsPayload(int maxLessonsPerDay)
        {
            var timeslots = new List<SchoolTimeslotPayload>();
            var lessonsPerDay = Math.Clamp(maxLessonsPerDay, 1, 10);

            for (var day = 0; day < 5; day++)
            {
                for (var lesson = 1; lesson <= lessonsPerDay; lesson++)
                {
                    var startTime = new TimeOnly(8, 0).AddMinutes((lesson - 1) * 50);
                    var endTime = startTime.AddMinutes(45);
                    timeslots.Add(new SchoolTimeslotPayload
                    {
                        DayOfWeek = day,
                        PeriodNumber = lesson,
                        StartTime = startTime,
                        EndTime = endTime,
                        IsActive = true
                    });
                }
            }

            return timeslots;
        }

        private async Task<int> EnsureRemoteTeacherAsync(SchoolTeacherPayload teacher, CancellationToken cancellationToken)
        {
            var remoteTeachers = await GetRemoteListAsync<RemoteTeacherDto>(TeachersRoute, cancellationToken);
            var existing = remoteTeachers.FirstOrDefault(item => string.Equals(item.Code, teacher.Code, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                return existing.Id;
            }

            var created = await PostJsonAsync<RemoteTeacherDto>(TeachersRoute, new
            {
                code = teacher.Code,
                full_name = teacher.FullName,
                email = teacher.Email,
                department = teacher.Department,
                max_hours_per_week = teacher.MaxHoursPerWeek,
                preferred_days = (string?)null,
                avoid_early_slots = false,
                avoid_late_slots = false
            }, cancellationToken);

            return created.Id;
        }

        private async Task<int> EnsureRemoteCourseAsync(SchoolSubjectPayload subject, CancellationToken cancellationToken)
        {
            var remoteCourses = await GetRemoteListAsync<RemoteCourseDto>(CoursesRoute, cancellationToken);
            var existing = remoteCourses.FirstOrDefault(item => string.Equals(item.Code, subject.Code, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                return existing.Id;
            }

            var created = await PostJsonAsync<RemoteCourseDto>(CoursesRoute, new
            {
                code = subject.Code,
                name = subject.Name,
                credits = subject.Credits,
                hours_per_week = subject.HoursPerWeek,
                requires_lab = false,
                preferred_classroom_type = (string?)null,
                difficulty = subject.Difficulty
            }, cancellationToken);

            return created.Id;
        }

        private async Task<int> EnsureRemoteGroupAsync(SchoolClassPayload classItem, CancellationToken cancellationToken)
        {
            var remoteGroups = await GetRemoteListAsync<RemoteGroupDto>(GroupsRoute, cancellationToken);
            var existing = remoteGroups.FirstOrDefault(item => string.Equals(item.Code, classItem.Code, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                return existing.Id;
            }

            var created = await PostJsonAsync<RemoteGroupDto>(GroupsRoute, new
            {
                code = classItem.Code,
                year = classItem.Year,
                students_count = classItem.StudentsCount,
                specialization = classItem.Name
            }, cancellationToken);

            return created.Id;
        }

        private async Task<int> EnsureRemoteClassroomAsync(SchoolClassroomPayload room, CancellationToken cancellationToken)
        {
            var remoteClassrooms = await GetRemoteListAsync<RemoteClassroomDto>(ClassroomsRoute, cancellationToken);
            var existing = remoteClassrooms.FirstOrDefault(item => string.Equals(item.Code, room.Code, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                return existing.Id;
            }

            var created = await PostJsonAsync<RemoteClassroomDto>(ClassroomsRoute, new
            {
                code = room.Code,
                building = (string?)null,
                floor = (int?)null,
                capacity = room.Capacity,
                classroom_type = room.ClassroomType,
                has_projector = room.HasProjector,
                has_computers = room.HasComputers,
                latitude = (double?)null,
                longitude = (double?)null
            }, cancellationToken);

            return created.Id;
        }

        private async Task<int> EnsureRemoteTimeslotAsync(SchoolTimeslotPayload timeslot, CancellationToken cancellationToken)
        {
            var remoteTimeslots = await GetRemoteListAsync<RemoteTimeslotDto>(TimeslotsRoute, cancellationToken);
            var existing = remoteTimeslots.FirstOrDefault(item => item.DayOfWeek == timeslot.DayOfWeek && item.PeriodNumber == timeslot.PeriodNumber);
            if (existing != null)
            {
                return existing.Id;
            }

            var created = await PostJsonAsync<RemoteTimeslotDto>(TimeslotsRoute, new
            {
                day_of_week = timeslot.DayOfWeek,
                period_number = timeslot.PeriodNumber,
                start_time = timeslot.StartTime,
                end_time = timeslot.EndTime,
                is_active = timeslot.IsActive
            }, cancellationToken);

            return created.Id;
        }

        private async Task EnsureRemoteAssignmentAsync(int courseId, int teacherId, int groupId, CancellationToken cancellationToken)
        {
            using var content = JsonContent.Create(new
            {
                course_id = courseId,
                teacher_id = teacherId,
                group_id = groupId
            });

            using var response = await _httpClient.PostAsync(AssignmentsRoute, content, cancellationToken);
            await EnsureSuccessAsync(response, "Не вдалося створити призначення у Python API.", cancellationToken);
        }

        private async Task<List<T>> GetRemoteListAsync<T>(string route, CancellationToken cancellationToken)
        {
            return await _httpClient.GetFromJsonAsync<List<T>>(route, JsonOptions, cancellationToken) ?? new List<T>();
        }

        private async Task<T> PostJsonAsync<T>(string route, object payload, CancellationToken cancellationToken)
        {
            using var response = await _httpClient.PostAsJsonAsync(route, payload, JsonOptions, cancellationToken);
            await EnsureSuccessAsync(response, $"Python API request failed for {route}.", cancellationToken);

            var data = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
            if (data == null)
            {
                throw new HttpRequestException($"Python API returned an empty payload for {route}.");
            }

            return data;
        }

        private async Task<RemoteScheduleGenerationStatus> WaitForGenerationCompletionAsync(int generationId, CancellationToken cancellationToken)
        {
            var timeout = TimeSpan.FromSeconds(Math.Max(10, _options.TimeoutSeconds));
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            while (stopwatch.Elapsed < timeout)
            {
                var status = await _httpClient.GetFromJsonAsync<RemoteScheduleGenerationStatus>($"{StatusRoute}/{generationId}", JsonOptions, cancellationToken);
                if (status == null)
                {
                    throw new HttpRequestException("Python API did not return generation status.");
                }

                if (string.Equals(status.Status, "completed", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(status.Status, "failed", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(status.Status, "stopped", StringComparison.OrdinalIgnoreCase))
                {
                    return status;
                }

                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }

            throw new HttpRequestException("Python schedule generation timed out.");
        }

        private async Task<byte[]> DownloadCurrentScheduleBytesAsync(string format, CancellationToken cancellationToken)
        {
            var exportBytes = await ExportScheduleAsync(0, 0, format, cancellationToken);
            return exportBytes;
        }

        private async Task EnsureSuccessAsync(HttpResponseMessage response, string fallbackMessage, CancellationToken cancellationToken)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var message = ExtractFriendlyMessage(body, fallbackMessage);
            _logger.LogWarning("Scheduler API call failed: {StatusCode} {Message}", response.StatusCode, message);
            throw new HttpRequestException(message);
        }

        private static string ExtractFriendlyMessage(string? body, string fallbackMessage)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return fallbackMessage;
            }

            try
            {
                using var document = JsonDocument.Parse(body);
                if (document.RootElement.TryGetProperty("detail", out var detailElement) && detailElement.ValueKind == JsonValueKind.String)
                {
                    return detailElement.GetString() ?? fallbackMessage;
                }

                if (document.RootElement.TryGetProperty("message", out var messageElement) && messageElement.ValueKind == JsonValueKind.String)
                {
                    return messageElement.GetString() ?? fallbackMessage;
                }
            }
            catch (JsonException)
            {
            }

            return body.Trim();
        }

        private static bool LooksLikeJson(byte[] bytes)
        {
            if (bytes.Length == 0)
            {
                return false;
            }

            var text = Encoding.UTF8.GetString(bytes).TrimStart();
            return text.StartsWith("{", StringComparison.Ordinal) || text.StartsWith("[", StringComparison.Ordinal);
        }

        private async Task<RemoteIdMappings> LoadRemoteMappingsAsync(CancellationToken cancellationToken)
        {
            var teachers = await GetRemoteListAsync<RemoteTeacherDto>(TeachersRoute, cancellationToken);
            var courses = await GetRemoteListAsync<RemoteCourseDto>(CoursesRoute, cancellationToken);
            var groups = await GetRemoteListAsync<RemoteGroupDto>(GroupsRoute, cancellationToken);

            return new RemoteIdMappings
            {
                TeacherIds = teachers
                    .Where(item => item.Code != null)
                    .ToDictionary(item => ExtractLocalId(item.Code!, RemotePrefixes.Teacher) ?? item.Id, item => item.Id),
                CourseIds = courses
                    .Where(item => item.Code != null)
                    .ToDictionary(item => ExtractLocalId(item.Code!, RemotePrefixes.Course) ?? item.Id, item => item.Id),
                GroupIds = groups
                    .Where(item => item.Code != null)
                    .ToDictionary(item => ExtractLocalId(item.Code!, RemotePrefixes.Group) ?? item.Id, item => item.Id)
            };
        }

        private static SchedulerValidationSummary ValidatePayload(SchoolPayload payload)
        {
            var summary = new SchedulerValidationSummary { IsValid = true };

            if (payload.Classes.Count == 0)
            {
                summary.Messages.Add("No classes available.");
            }

            if (payload.Subjects.Count == 0)
            {
                summary.Messages.Add("No subjects available.");
            }

            if (payload.Teachers.Count == 0)
            {
                summary.Messages.Add("No teachers available.");
            }

            if (payload.ClassSubjects.Count == 0)
            {
                summary.Messages.Add("No class-subject relationships available.");
            }

            summary.IsValid = summary.Messages.Count == 0;
            return summary;
        }

        private static string BuildCode(string prefix, int id)
        {
            return $"{prefix}{id}";
        }

        private static string BuildTeacherFullName(Teacher teacher)
        {
            var parts = new[] { teacher.Surname, teacher.Name, teacher.Patronymic }
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item!.Trim());
            return string.Join(" ", parts);
        }

        private static int ExtractYearFromClassName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return 1;
            }

            var digits = new string(name.TakeWhile(char.IsDigit).ToArray());
            return int.TryParse(digits, out var year) && year > 0 ? Math.Clamp(year, 1, 6) : 1;
        }

        private static int? DecodeId(JsonElement item, string propertyName, string prefix)
        {
            if (!item.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return ExtractLocalId(property.GetString() ?? string.Empty, prefix);
        }

        private int? ResolveTeacherId(JsonElement item, RemoteIdMappings mappings)
        {
            if (item.TryGetProperty("teacher_id", out var teacherIdElement) && teacherIdElement.ValueKind == JsonValueKind.Number)
            {
                var remoteTeacherId = teacherIdElement.GetInt32();
                return mappings.TeacherIds.FirstOrDefault(pair => pair.Value == remoteTeacherId).Key;
            }

            if (item.TryGetProperty("teacher_name", out var teacherNameElement) && teacherNameElement.ValueKind == JsonValueKind.String)
            {
                var teacher = _context.Teacher
                    .AsNoTracking()
                    .FirstOrDefault(entity => BuildTeacherFullName(entity) == teacherNameElement.GetString());

                return teacher?.Id;
            }

            return null;
        }

        private static int? ExtractLocalId(string code, string prefix)
        {
            if (string.IsNullOrWhiteSpace(code) || !code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var rawId = code.Substring(prefix.Length);
            return int.TryParse(rawId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : null;
        }

        private sealed class RemoteIdMappings
        {
            public Dictionary<int, int> TeacherIds { get; set; } = new();
            public Dictionary<int, int> CourseIds { get; set; } = new();
            public Dictionary<int, int> GroupIds { get; set; } = new();
            public Dictionary<int, int> ClassroomIds { get; set; } = new();
            public Dictionary<string, int> TimeslotIds { get; set; } = new();
        }

        private static class RemotePrefixes
        {
            public const string Group = "cls_";
            public const string Course = "subj_";
            public const string Teacher = "tch_";
            public const string Room = "room_";
        }

        private sealed class SchoolPayload
        {
            public int SchoolId { get; set; }
            public int AcademicYearId { get; set; }
            public List<SchoolClassPayload> Classes { get; set; } = new();
            public List<SchoolSubjectPayload> Subjects { get; set; } = new();
            public List<SchoolTeacherPayload> Teachers { get; set; } = new();
            public List<SchoolClassroomPayload> Classrooms { get; set; } = new();
            public List<SchoolTimeslotPayload> Timeslots { get; set; } = new();
            public List<SchoolClassSubjectPayload> ClassSubjects { get; set; } = new();
        }

        private sealed class SchoolClassPayload
        {
            public int LocalId { get; set; }
            public string Code { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public int Year { get; set; }
            public int StudentsCount { get; set; }
            public int? TeacherId { get; set; }
        }

        private sealed class SchoolSubjectPayload
        {
            public int LocalId { get; set; }
            public string Code { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public int Credits { get; set; }
            public int HoursPerWeek { get; set; }
            public int Difficulty { get; set; }
            public int TeacherId { get; set; }
        }

        private sealed class SchoolTeacherPayload
        {
            public int LocalId { get; set; }
            public string Code { get; set; } = string.Empty;
            public string FullName { get; set; } = string.Empty;
            public string? Email { get; set; }
            public string? Department { get; set; }
            public int MaxHoursPerWeek { get; set; }
        }

        private sealed class SchoolClassroomPayload
        {
            public int LocalId { get; set; }
            public string Code { get; set; } = string.Empty;
            public int Capacity { get; set; }
            public string ClassroomType { get; set; } = "general";
            public bool HasProjector { get; set; }
            public bool HasComputers { get; set; }
        }

        private sealed class SchoolTimeslotPayload
        {
            public int DayOfWeek { get; set; }
            public int PeriodNumber { get; set; }
            public TimeOnly StartTime { get; set; }
            public TimeOnly EndTime { get; set; }
            public bool IsActive { get; set; }
        }

        private sealed class SchoolClassSubjectPayload
        {
            public int ClassId { get; set; }
            public int SubjectId { get; set; }
            public int? SubjectTeacherId { get; set; }
            public int? ClassTeacherId { get; set; }
        }

        private sealed class RemoteCourseDto
        {
            public int Id { get; set; }
            public string Code { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
        }

        private sealed class RemoteTeacherDto
        {
            public int Id { get; set; }
            public string Code { get; set; } = string.Empty;
            public string FullName { get; set; } = string.Empty;
        }

        private sealed class RemoteGroupDto
        {
            public int Id { get; set; }
            public string Code { get; set; } = string.Empty;
        }

        private sealed class RemoteClassroomDto
        {
            public int Id { get; set; }
            public string Code { get; set; } = string.Empty;
        }

        private sealed class RemoteTimeslotDto
        {
            public int Id { get; set; }
            public int DayOfWeek { get; set; }
            public int PeriodNumber { get; set; }
        }

        private sealed class RemoteScheduleGenerateRequest
        {
            public int Iterations { get; set; }
            public bool UseExisting { get; set; }
            public bool PreserveLocked { get; set; }
            public string? ModelVersion { get; set; }
        }

        private sealed class RemoteScheduleGenerationStatus
        {
            public int Id { get; set; }
            public string Status { get; set; } = string.Empty;
            public int Iterations { get; set; }
            public int CurrentIteration { get; set; }
            public double? FinalScore { get; set; }
            public int HardViolations { get; set; }
            public int SoftViolations { get; set; }
            public double? ExecutionTime { get; set; }
            public string? ErrorMessage { get; set; }
        }

        private sealed class RemoteScheduleExportResponse
        {
            public string Filename { get; set; } = string.Empty;
            public int ClassesCount { get; set; }
            public string Message { get; set; } = string.Empty;
        }
    }
}