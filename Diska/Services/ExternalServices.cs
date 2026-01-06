using Microsoft.Extensions.Configuration;

namespace Diska.Services
{
    // --- 1. Shipping Interface ---
    public interface IShippingService
    {
        decimal CalculateCost(string governorate, string city);
        Task<string> CreateShipmentAsync(int orderId, string customerName, string phone, string address, decimal amount);
        Task<string> TrackShipmentAsync(string trackingNumber);
    }

    // --- 2. Payment Interface ---
    public interface IPaymentService
    {
        Task<string> InitiatePaymentAsync(decimal amount, string currency, object orderData);
        Task<bool> VerifyPaymentAsync(string transactionId);
    }

    // --- Implementation (Shipping) ---
    public class ShippingService : IShippingService
    {
        private readonly IConfiguration _config;

        public ShippingService(IConfiguration config)
        {
            _config = config;
        }

        public decimal CalculateCost(string governorate, string city)
        {
            // حالياً: نستخدم الأسعار الثابتة من الإعدادات
            // مستقبلاً: هنا تضع كود API شركة الشحن (مثل Bosta Calculate API)

            if (string.IsNullOrEmpty(governorate)) return 0;

            if (governorate.Contains("القاهرة") || governorate.Contains("الجيزة") || governorate.Contains("Cairo") || governorate.Contains("Giza"))
            {
                return _config.GetValue<decimal>("ShippingSettings:FixedRateCairo");
            }
            else if (governorate.Contains("الإسكندرية") || governorate.Contains("Alexandria"))
            {
                return _config.GetValue<decimal>("ShippingSettings:FixedRateAlex");
            }

            return _config.GetValue<decimal>("ShippingSettings:FixedRateOther");
        }

        public async Task<string> CreateShipmentAsync(int orderId, string customerName, string phone, string address, decimal amount)
        {
            // حالياً: نرجع رقم بوليصة وهمي
            // مستقبلاً: POST Request لشركة الشحن لإنشاء البوليصة
            await Task.Delay(100); // محاكاة اتصال
            return $"AWB-{DateTime.Now.Year}-{orderId}";
        }

        public async Task<string> TrackShipmentAsync(string trackingNumber)
        {
            // مستقبلاً: GET Request لحالة الشحنة
            return "On the way";
        }
    }

    // --- Implementation (Payment) ---
    public class PaymentService : IPaymentService
    {
        private readonly IConfiguration _config;

        public PaymentService(IConfiguration config)
        {
            _config = config;
        }

        public async Task<string> InitiatePaymentAsync(decimal amount, string currency, object orderData)
        {
            // هنا يتم الربط مع Paymob أو Stripe
            // الخطوات مستقبلاً:
            // 1. Auth Request (Get Token)
            // 2. Order Registration API
            // 3. Payment Key Request
            // 4. Return Iframe URL

            await Task.Delay(100);
            return "https://payment-gateway-url.com/pay?token=xyz"; // رابط وهمي
        }

        public async Task<bool> VerifyPaymentAsync(string transactionId)
        {
            // التحقق من نجاح الدفع
            return true;
        }
    }
}