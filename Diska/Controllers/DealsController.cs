using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Diska.Models;
using Microsoft.EntityFrameworkCore;

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
        public IActionResult Index()
        {
            // جلب الصفقات التي لم تنته مدتها بعد
            var activeDeals = _context.GroupDeals
                .Include(d => d.Product)
                .Where(d => d.IsActive && d.EndDate > DateTime.Now)
                .OrderBy(d => d.EndDate)
                .ToList();

            // إذا لم توجد صفقات، نقوم بإنشاء صفقة وهمية للعرض (للتجربة)
            if (!activeDeals.Any())
            {
                // هذا الكود للعرض فقط، في الواقع يجب أن تكون البيانات في الداتابيز
                ViewBag.DemoMode = true;
            }

            return View(activeDeals);
        }

        // الانضمام لصفقة (حجز كمية)
        [HttpPost]
        public async Task<IActionResult> JoinDeal(int dealId, int quantity)
        {
            var deal = await _context.GroupDeals.FindAsync(dealId);
            if (deal == null) return NotFound();

            if (User.Identity.IsAuthenticated)
            {
                deal.ReservedQuantity += quantity;
                // هنا يمكننا إنشاء طلب مبدئي (Pre-Order)
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "تم حجز الكمية بنجاح! سيتم التواصل معك عند اكتمال الهدف." });
            }

            return Json(new { success = false, message = "يجب تسجيل الدخول أولاً." });
        }
    }
}