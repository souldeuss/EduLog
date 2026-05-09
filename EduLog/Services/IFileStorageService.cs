namespace EduLog.Services
{
    public class StoredFile
    {
        public string RelativePath { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
    }

    public interface IFileStorageService
    {
        Task<StoredFile?> SaveAsync(IFormFile file, string subfolder, CancellationToken cancellationToken = default);
        void Delete(string? relativePath);
    }
}
