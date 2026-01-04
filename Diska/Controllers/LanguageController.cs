using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace Diska.Controllers
{
    // متحكم للتبديل بين العربية والإنجليزية
    public class LanguageController : Controller
    {
        [HttpPost]
        public IActionResult SetLanguage(string culture, string returnUrl)
        {
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) }
            );

            // تعديل بسيط: إذا لم يكن هناك ReturnUrl عد للرئيسية
            // في التطبيق، سنقوم بالتحويل بين Routes (مثل Index-en) إذا كنت تستخدم صفحات منفصلة
            // أو الاعتماد على الـ View Localization إذا كنت تستخدم ملفات Resx.
            // بما أنك طلبت التبديل بين ملفات (index.html <-> index-en.html) في الفرونت، سنستخدم الجافاسكريبت المرفق.
            // هذا الكنترولر هنا لتهيئة بيئة الـ .NET Core لضبط التواريخ والعملات.

            return LocalRedirect(returnUrl ?? "/");
        }
    }
}