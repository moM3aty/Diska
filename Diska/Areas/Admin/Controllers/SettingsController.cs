using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
// يمكنك استخدام كائن بسيط للإعدادات أو حفظها في قاعدة البيانات
// هنا سنستخدم TempData للمحاكاة أو ربطها بملف appsettings.json في التطبيق الحقيقي

namespace Diska.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class SettingsController : Controller
    {
        private readonly IConfiguration _configuration;

        public SettingsController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet]
        public IActionResult Index()
        {
            // جلب الإعدادات الحالية (مثلاً من ملف التكوين أو قاعدة البيانات)
            var model = new SystemSettingsViewModel
            {
                SiteName = "DISKA B2B",
                SupportEmail = "support@diska.com",
                SupportPhone = "01000000000",
                ShippingBaseFee = 50,
                EnableMaintenanceMode = false
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Save(SystemSettingsViewModel model)
        {
            if (ModelState.IsValid)
            {
                // هنا يتم حفظ الإعدادات في قاعدة البيانات (Table: Settings)
                // أو تحديث ملف JSON

                TempData["Success"] = "تم حفظ إعدادات النظام بنجاح.";
                return RedirectToAction(nameof(Index));
            }
            return View("Index", model);
        }
    }

    public class SystemSettingsViewModel
    {
        public string SiteName { get; set; }
        public string SupportEmail { get; set; }
        public string SupportPhone { get; set; }
        public decimal ShippingBaseFee { get; set; }
        public bool EnableMaintenanceMode { get; set; }
        public string FacebookLink { get; set; }
        public string WhatsappNumber { get; set; }
    }
}