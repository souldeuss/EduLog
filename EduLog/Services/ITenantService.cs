namespace EduLog.Services
{
    public interface ITenantService
    {
        int? SchoolId { get; }
    }

    public class TenantService : ITenantService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public TenantService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public int? SchoolId
        {
            get
            {
                var claim = _httpContextAccessor.HttpContext?.User?.FindFirst("SchoolId");
                return claim != null && int.TryParse(claim.Value, out var id) ? id : null;
            }
        }
    }
}
