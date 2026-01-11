using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Microsoft.EntityFrameworkCore;
using Diska.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace Diska.Areas.Merchant.Controllers
{
    [Area("Merchant")]
    [Authorize(Roles = "Merchant")]
    public class MerchantOrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public MerchantOrderController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // عرض الطلبات الواردة (التي تحتوي على منتجات التاجر فقط)
        public async Task<IActionResult> Index(string status = "All")
        {
            var user = await _userManager.GetUserAsync(User);

            // 1. جلب العناصر (OrderItems) المرتبطة بمنتجات التاجر
            var query = _context.OrderItems
                .Include(oi => oi.Order)
                .Include(oi => oi.Product)
                .Where(oi => oi.Product.MerchantId == user.Id);

            // 2. الفلترة
            if (status != "All")
            {
                query = query.Where(oi => oi.Order.Status == status);
            }

            // 3. الترتيب حسب الأحدث
            var merchantItems = await query
                .OrderByDescending(oi => oi.Order.OrderDate)
                .ToListAsync();

            return View(merchantItems);
        }

        // تفاصيل الطلب (رؤية جزئية للمنتجات الخاصة بالتاجر فقط)
        public async Task<IActionResult> Details(int orderId)
        {
            var user = await _userManager.GetUserAsync(User);

            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null) return NotFound();

            // فلترة القائمة لعرض منتجات هذا التاجر فقط داخل الطلب
            order.OrderItems = order.OrderItems
                .Where(oi => oi.Product != null && oi.Product.MerchantId == user.Id)
                .ToList();

            // إذا لم يكن للتاجر أي منتج في هذا الطلب، نمنع الوصول
            if (!order.OrderItems.Any()) return Forbid();

            return View(order);
        }
    }
}