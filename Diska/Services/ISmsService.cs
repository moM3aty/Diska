using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Diska.Services
{
    // 🚨 1. الانترفيس المحدث (يُرجع Tuple يحتوي على حالة النجاح والرسالة)
    public interface ISmsService
    {
        Task<(bool IsSuccess, string Message)> SendOtpAsync(string phoneNumber, string otpCode);
        Task<(bool IsSuccess, string Message)> SendSmsAsync(string phoneNumber, string message);
    }

    // 🚨 2. كلاس الخدمة المحدث
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

        public async Task<(bool IsSuccess, string Message)> SendOtpAsync(string phoneNumber, string otpCode)
        {
            string message = $"رمز التحقق الخاص بك في منصة ديسكا هو: {otpCode}";
            return await SendSmsAsync(phoneNumber, message);
        }

        public async Task<(bool IsSuccess, string Message)> SendSmsAsync(string phoneNumber, string message)
        {
            try
            {
                // الإعدادات (تأكد أنها موجودة في appsettings.json)
                var baseUrl = _configuration["WhySmsSettings:BaseUrl"] ?? "https://bulk.whysms.com/api/v3/sms/";
                var apiToken = _configuration["WhySmsSettings:ApiToken"] ?? "1138|UXdBboZ1il3eys99Ik1n1KBI4VyqqvGAknKV1fMj9905ebde";
                var senderId = _configuration["WhySmsSettings:SenderId"] ?? "WhySMS Test";

                // تنظيف الرقم (تحويله للأرقام الإنجليزية وإضافة 2 في البداية)
                phoneNumber = NormalizePhoneNumber(phoneNumber);

                var payload = new
                {
                    recipient = phoneNumber,
                    sender_id = senderId,
                    type = "plain",
                    message = message
                };

                string jsonPayload = JsonSerializer.Serialize(payload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // إرسال الطلب للشركة
                var response = await _httpClient.PostAsync($"{baseUrl}send", content);
                var responseString = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(responseString);

                    if (doc.RootElement.TryGetProperty("status", out var statusProp) && statusProp.GetString() == "success")
                    {
                        return (true, "تم الإرسال بنجاح");
                    }

                    // لو الرد مش success، نجيب الرسالة من الشركة
                    string apiError = doc.RootElement.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : responseString;
                    return (false, apiError);
                }
                else
                {
                    return (false, $"HTTP Error: {(int)response.StatusCode} - {responseString}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"SMS Exception: {ex.Message}");
                return (false, $"Exception: {ex.Message}");
            }
        }

        // دالة تنظيف رقم الهاتف وتجهيزه
        private string NormalizePhoneNumber(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return phone;

            // 1. تحويل الأرقام الهندية (العربية) إلى إنجليزية
            string[] arabicDigits = { "٠", "١", "٢", "٣", "٤", "٥", "٦", "٧", "٨", "٩" };
            for (int i = 0; i < arabicDigits.Length; i++)
            {
                phone = phone.Replace(arabicDigits[i], i.ToString());
            }

            // 2. إزالة المسافات وعلامة +
            phone = phone.Replace(" ", "").Replace("+", "");

            // 3. التأكد من كود مصر (إضافة 2 للرقم الذي يبدأ بـ 01)
            if (phone.StartsWith("01") && phone.Length == 11)
            {
                phone = "2" + phone;
            }

            return phone;
        }
    }
}