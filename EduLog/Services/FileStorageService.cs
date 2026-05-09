namespace EduLog.Services
{
    public class FileStorageService : IFileStorageService
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<FileStorageService> _logger;

        // Files matching these extensions are rejected outright (executable / scripting).
        private static readonly HashSet<string> _blockedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".dll", ".bat", ".cmd", ".com", ".msi", ".sh", ".ps1",
            ".vbs", ".js", ".jse", ".wsf", ".wsh",
            ".html", ".htm", ".svg" // svg can carry scripts
        };

        // 25 MB
        private const long MaxFileSize = 25 * 1024 * 1024;

        public FileStorageService(IWebHostEnvironment env, ILogger<FileStorageService> logger)
        {
            _env = env;
            _logger = logger;
        }

        public async Task<StoredFile?> SaveAsync(IFormFile file, string subfolder, CancellationToken cancellationToken = default)
        {
            if (file == null || file.Length == 0) return null;
            if (file.Length > MaxFileSize)
            {
                throw new InvalidOperationException("Файл занадто великий (максимум 25 МБ).");
            }

            var originalName = Path.GetFileName(file.FileName);
            var ext = Path.GetExtension(originalName);

            if (_blockedExtensions.Contains(ext))
            {
                throw new InvalidOperationException($"Тип файлу {ext} заборонений з міркувань безпеки.");
            }

            // Sanitize subfolder: only allow letters/digits/dash/underscore
            var safeSub = new string(subfolder.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray());
            if (string.IsNullOrEmpty(safeSub)) safeSub = "misc";

            var folder = Path.Combine(_env.WebRootPath, "uploads", safeSub);
            Directory.CreateDirectory(folder);

            var uniqueName = $"{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(folder, uniqueName);

            await using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            return new StoredFile
            {
                RelativePath = $"/uploads/{safeSub}/{uniqueName}",
                OriginalFileName = originalName
            };
        }

        public void Delete(string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return;
            if (!relativePath.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase)) return;

            try
            {
                // Strip leading slash and combine with WebRootPath
                var trimmed = relativePath.TrimStart('/');
                var fullPath = Path.Combine(_env.WebRootPath, trimmed.Replace('/', Path.DirectorySeparatorChar));

                // Defense-in-depth: ensure the resolved path stays within /uploads
                var uploadsRoot = Path.GetFullPath(Path.Combine(_env.WebRootPath, "uploads"));
                var resolved = Path.GetFullPath(fullPath);
                if (!resolved.StartsWith(uploadsRoot, StringComparison.OrdinalIgnoreCase)) return;

                if (File.Exists(resolved)) File.Delete(resolved);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete file at {Path}", relativePath);
            }
        }
    }
}
