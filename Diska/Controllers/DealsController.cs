using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Diska.Models;
using Microsoft.EntityFrameworkCore;
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

        public IActionResult Index()
        {
            var deals = _context.GroupDeals
                .Include(d => d.Product)
                .Where(d => d.IsActive && d.EndDate > DateTime.Now)
                .OrderBy(d => d.EndDate)
                .ToList();

            return View(deals);
        }


        [HttpPost]
        [Authorize]
        public async Task<IActionResult> JoinDeal(int dealId, int quantity)
        {
            var deal = await _context.GroupDeals.Include(d => d.Product).FirstOrDefaultAsync(d => d.Id == dealId);
            if (deal == null) return Json(new { success = false, message = "الصفقة غير موجودة" });

            if (deal.ReservedQuantity + quantity > deal.TargetQuantity)
            {
                return Json(new { success = false, message = "الكمية المطلوبة تتجاوز المتبقي من الهدف" });
            }

            deal.ReservedQuantity += quantity;



            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "تم حجز الكمية بنجاح!", newProgress = (deal.ReservedQuantity / (double)deal.TargetQuantity) * 100 });
        }
    }
}