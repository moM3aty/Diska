using System.Collections.Generic;

namespace Diska.Services
{
    public interface IShippingService
    {
        decimal CalculateCost(string governorate, string city);
    }

    public class ShippingService : IShippingService
    {
        private readonly Dictionary<string, decimal> _govRates = new()
        {
            { "Cairo", 50 },
            { "Giza", 50 },
            { "Alexandria", 70 },
            { "Delta", 65 },
            { "Canal", 75 },
            { "Upper Egypt", 100 }
        };

        public decimal CalculateCost(string governorate, string city)
        {
            // منطق بسيط: البحث عن المحافظة، إذا لم توجد نستخدم سعر افتراضي
            // في التطبيق الحقيقي، هذه البيانات تأتي من قاعدة البيانات

            if (string.IsNullOrEmpty(governorate)) return 0;

            // تطبيع الاسم (بحث مرن)
            var key = _govRates.Keys.FirstOrDefault(k => k.Contains(governorate, StringComparison.OrdinalIgnoreCase))
                      ?? "Cairo"; // Fallback to base rate if not found logic

            // مثال: إذا كانت القاهرة أو الجيزة 50، غير ذلك 80
            if (governorate.Contains("Cairo") || governorate.Contains("Giza") || governorate.Contains("القاهرة") || governorate.Contains("الجيزة"))
                return 50;

            if (governorate.Contains("Alex") || governorate.Contains("الإسكندرية"))
                return 70;

            return 85; // سعر موحد للمحافظات الأخرى للتبسيط
        }
    }
}