using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace EduLog.Services
{
    public sealed class MailtrapEmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpFactory;

        public MailtrapEmailService(IConfiguration configuration, IHttpClientFactory httpFactory)
        {
            _configuration = configuration;
            _httpFactory = httpFactory;
        }

        public async Task SendInvitationAsync(string toEmail, string invitationLink, CancellationToken ct = default)
        {
            var apiKey = _configuration["Email:ApiKey"];
            var senderAddress = _configuration["Email:SenderAddress"];

            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(senderAddress))
            {
                throw new ApplicationException("Не налаштовано параметри Email (ApiKey/SenderAddress). Зверніться до адміністратора.");
            }

            var sandboxIdValue = _configuration["Email:SandboxId"];
            var useTest = int.TryParse(sandboxIdValue, out var sandboxId) && sandboxId > 0;

            var http = _httpFactory.CreateClient();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            // Build Mailtrap test/send endpoints
            // Test endpoint: https://send.api.mailtrap.io/api/send (with sandbox query)
            // Mailtrap API docs vary; we construct a JSON body compatible with Mailtrap Send API

            var payload = new
            {
                sender = new { email = senderAddress, name = "EduLog" },
                to = new[] { new { email = toEmail } },
                subject = "Запрошення до EduLog",
                html = $"<div style='font-family: Arial, sans-serif; color: #1f2937;'><h2>Вас запрошено до EduLog</h2><p>Натисніть кнопку нижче, щоб завершити реєстрацію:</p><p><a href='{invitationLink}' style='display:inline-block;padding:10px 18px;background:#0d6efd;color:#ffffff;text-decoration:none;border-radius:6px;'>Зареєструватися</a></p><p>Або відкрийте це посилання вручну:</p><p><a href='{invitationLink}'>{invitationLink}</a></p></div>"
            };

            try
            {
                HttpResponseMessage resp;
                if (useTest)
                {
                    var url = $"https://send.api.mailtrap.io/api/send?sandbox_id={sandboxId}";
                    resp = await http.PostAsJsonAsync(url, payload, ct);
                }
                else
                {
                    var url = "https://send.api.mailtrap.io/api/send";
                    resp = await http.PostAsJsonAsync(url, payload, ct);
                }

                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync(ct);
                    throw new ApplicationException($"Mailtrap responded with {(int)resp.StatusCode}: {body}");
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ApplicationException("Не вдалося надіслати запрошення на email. Спробуйте пізніше.", ex);
            }
        }
    }
}
