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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage(ContactMessage model)
        {
            if (ModelState.IsValid)
            {
                model.DateSent = DateTime.Now;
                _context.ContactMessages.Add(model);
                await _context.SaveChangesAsync();

                // يمكن إضافة إشعار للأدمن هنا

                TempData["Success"] = "تم إرسال رسالتك بنجاح، سيتواصل معك فريق الدعم قريباً.";
                return RedirectToAction("Contact", "Home");
            }

            TempData["Error"] = "حدث خطأ، يرجى التأكد من البيانات.";
            return RedirectToAction("Contact", "Home");
        }
    }
}