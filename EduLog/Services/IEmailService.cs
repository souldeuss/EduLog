namespace EduLog.Services
{
    public interface IEmailService
    {
        Task SendInvitationAsync(string toEmail, string invitationLink, CancellationToken ct = default);
    }
}
