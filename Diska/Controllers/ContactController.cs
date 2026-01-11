using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Diska.Models;
using System;
using System.Threading.Tasks;

namespace Diska.Controllers
{
    public class ContactController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ContactController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. عرض صفحة التواصل (Static Info + Form)
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        // 2. استقبال الرسالة (Form Action)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage(ContactMessage model)
        {
            if (ModelState.IsValid)
            {
                model.DateSent = DateTime.Now;
                _context.ContactMessages.Add(model);
                await _context.SaveChangesAsync();

                // يمكن إضافة كود لإرسال إيميل للإدارة هنا

                TempData["Success"] = "تم إرسال رسالتك بنجاح! شكراً لتواصلك معنا.";
                return RedirectToAction(nameof(Index));
            }

            TempData["Error"] = "عفواً، يرجى التأكد من صحة البيانات المدخلة.";
            return View("Index", model); // إعادة عرض الصفحة مع الأخطاء
        }
    }
}