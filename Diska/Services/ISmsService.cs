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
    public interface ISmsService
    {
        Task<(bool IsSuccess, string Message)> SendOtpAsync(string phoneNumber, string otpCode);
        Task<(bool IsSuccess, string Message)> SendSmsAsync(string phoneNumber, string message);
    }

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
                // 1. جلب الإعدادات
                // تأكد من أن الرابط ينتهي بـ / (slash)
                var baseUrl = _configuration["WhySmsSettings:BaseUrl"] ?? "https://bulk.whysms.com/api/v3/sms/";
                var apiToken = _configuration["WhySmsSettings:ApiToken"] ?? "1138|UXdBboZ1il3eys99Ik1n1KBI4VyqqvGAknKV1fMj9905ebde";
                var senderId = _configuration["WhySmsSettings:SenderId"] ?? "WhySMS Test";

                // 2. تنظيف رقم الهاتف
                phoneNumber = NormalizePhoneNumber(phoneNumber);

                // 3. تجهيز البيانات (Payload)
                var payload = new
                {
                    recipient = phoneNumber,
                    sender_id = senderId,
                    type = "plain",
                    message = message
                };

                string jsonPayload = JsonSerializer.Serialize(payload);

                // 4. بناء الطلب بطريقة آمنة جداً (HttpRequestMessage)
                // دمج baseUrl مع كلمة send بشكل صحيح
                string requestUrl = baseUrl.EndsWith("/") ? $"{baseUrl}send" : $"{baseUrl}/send";

                var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken.Trim());
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                // ==============================================================
                // طباعة في الشاشة السوداء (Console) لمعرفة ماذا يحدث بالضبط
                Console.WriteLine("========================================");
                Console.WriteLine($"[SMS DEBUG] URL: {requestUrl}");
                Console.WriteLine($"[SMS DEBUG] Token: Bearer {apiToken.Trim()}");
                Console.WriteLine($"[SMS DEBUG] Payload: {jsonPayload}");
                Console.WriteLine("========================================");
                // ==============================================================

                // 5. إرسال الطلب
                var response = await _httpClient.SendAsync(request);
                var responseString = await response.Content.ReadAsStringAsync();

                // طباعة رد الشركة
                Console.WriteLine($"[SMS RESPONSE] Status: {response.StatusCode}");
                Console.WriteLine($"[SMS RESPONSE] Body: {responseString}");
                Console.WriteLine("========================================");

                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(responseString);
                    if (doc.RootElement.TryGetProperty("status", out var statusProp) && statusProp.GetString() == "success")
                    {
                        return (true, "تم الإرسال بنجاح");
                    }

                    string apiError = doc.RootElement.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : responseString;
                    return (false, $"تم الرفض من الشركة: {apiError}");
                }
                else
                {
                    // لو الخطأ 401 أو 404 أو 500
                    return (false, $"فشل الاتصال: {(int)response.StatusCode} - {responseString}");
                }
            }
            catch (Exception ex)
            {
                // لو فشل محلي قبل أن يخرج للانترنت (مثل مشكلة الـ SSL)
                Console.WriteLine($"[SMS LOCAL CRASH] {ex.Message}");
                return (false, $"خطأ محلي في السيرفر: {ex.Message}");
            }
        }

        private string NormalizePhoneNumber(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return phone;
            string[] arabicDigits = { "٠", "١", "٢", "٣", "٤", "٥", "٦", "٧", "٨", "٩" };
            for (int i = 0; i < arabicDigits.Length; i++) { phone = phone.Replace(arabicDigits[i], i.ToString()); }
            phone = phone.Replace(" ", "").Replace("+", "");
            if (phone.StartsWith("01") && phone.Length == 11) { phone = "2" + phone; }
            return phone;
        }
    }
}