using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Microsoft.EntityFrameworkCore;
using Diska.Models;
using Microsoft.AspNetCore.Authorization;

namespace Diska.Controllers
{
    public class DealsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DealsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // عرض الصفقات النشطة
        public async Task<IActionResult> Index()
        {
            var deals = await _context.GroupDeals
                .Include(d => d.Product)
                .Where(d => d.EndDate > DateTime.Now)
                .OrderBy(d => d.EndDate)
                .ToListAsync();

            return View(deals);
        }

        // الانضمام لصفقة (حجز كمية)
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> JoinDeal(int dealId, int quantity)
        {
            var deal = await _context.GroupDeals.FindAsync(dealId);
            if (deal == null || !deal.IsActive)
            {
                return Json(new { success = false, message = "العرض غير متاح حالياً" });
            }

            if (deal.ReservedQuantity + quantity > deal.TargetQuantity)
            {
                return Json(new { success = false, message = $"الكمية المتبقية في العرض هي {deal.TargetQuantity - deal.ReservedQuantity} فقط" });
            }

            // تحديث الكمية المحجوزة
            deal.ReservedQuantity += quantity;

            // هنا يمكن إضافة منطق لإنشاء "طلب مؤجل" أو حجز رصيد
            // للتبسيط سنكتفي بتحديث العداد

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "تم حجز الكمية بنجاح!",
                newProgress = (double)deal.ReservedQuantity / deal.TargetQuantity * 100,
                reserved = deal.ReservedQuantity
            });
        }
    }
}