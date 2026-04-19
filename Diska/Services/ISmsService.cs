using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Diska.Services
{
    // 1. الواجهة (Interface)
    public interface ISmsService
    {
        Task<bool> SendOtpAsync(string phoneNumber, string otpCode);
        Task<bool> SendSmsAsync(string phoneNumber, string message);
    }

    // 2. الكلاس المنفذ (Implementation)
    public class WhySmsService : ISmsService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<WhySmsService> _logger;

        public WhySmsService(HttpClient httpClient, IConfiguration configuration, ILogger<WhySmsService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<bool> SendOtpAsync(string phoneNumber, string otpCode)
        {
            string message = $"رمز التحقق الخاص بك في منصة ديسكا هو: {otpCode}";
            return await SendSmsAsync(phoneNumber, message);
        }

        public async Task<bool> SendSmsAsync(string phoneNumber, string message)
        {
            try
            {
                // جلب الإعدادات من appsettings.json
                var baseUrl = _configuration["WhySmsSettings:BaseUrl"];
                var apiToken = _configuration["WhySmsSettings:ApiToken"];
                var senderId = _configuration["WhySmsSettings:SenderId"];

                // تنسيق رقم الهاتف (يجب أن يبدأ بكود الدولة لمصر 20)
                if (phoneNumber.StartsWith("01"))
                {
                    phoneNumber = "2" + phoneNumber;
                }

                // تجهيز البيانات حسب الـ Documentation الخاص بـ WhySMS
                var payload = new
                {
                    recipient = phoneNumber,
                    sender_id = senderId,
                    type = "plain",
                    message = message
                };

                string jsonPayload = JsonSerializer.Serialize(payload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                // إعداد الـ Headers (Bearer Token)
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // إرسال الطلب (POST)
                var response = await _httpClient.PostAsync($"{baseUrl}send", content);
                var responseString = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    // فحص الرد للتأكد من نجاح الإرسال من طرف WhySMS
                    using var doc = JsonDocument.Parse(responseString);
                    if (doc.RootElement.TryGetProperty("status", out var statusProp) && statusProp.GetString() == "success")
                    {
                        return true;
                    }

                    _logger.LogWarning($"SMS API returned non-success status: {responseString}");
                    return false;
                }
                else
                {
                    _logger.LogError($"SMS API Error: {response.StatusCode} - {responseString}");
                    return false;
                }
            }
            catch (System.Exception ex)
            {
                _logger.LogError($"Failed to send SMS: {ex.Message}");
                return false;
            }
        }
    }
}