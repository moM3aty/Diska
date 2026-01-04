using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Microsoft.EntityFrameworkCore;
using Diska.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace Diska.Controllers
{
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

        // عرض الطلبات التي تحتوي على منتجات هذا التاجر
        public async Task<IActionResult> Index(string status = "All")
        {
            var user = await _userManager.GetUserAsync(User);

            // جلب بنود الطلبات (OrderItems) المرتبطة بمنتجات التاجر الحالي
            var query = _context.OrderItems
                .Include(oi => oi.Order)
                .Include(oi => oi.Product)
                .Where(oi => oi.Product.MerchantId == user.Id);

            if (status != "All")
            {
                query = query.Where(oi => oi.Order.Status == status);
            }

            var merchantOrders = await query
                .OrderByDescending(oi => oi.Order.OrderDate)
                .ToListAsync();

            return View(merchantOrders);
        }

        // تفاصيل الطلب من وجهة نظر التاجر
        public async Task<IActionResult> Details(int orderId)
        {
            var user = await _userManager.GetUserAsync(User);

            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null) return NotFound();

            // تصفية المنتجات لتعرض فقط منتجات هذا التاجر داخل الطلب
            order.OrderItems = order.OrderItems.Where(oi => oi.Product.MerchantId == user.Id).ToList();

            if (!order.OrderItems.Any()) return Forbid(); // ليس له صلاحية رؤية طلب لا يخصه

            return View(order);
        }
    }
}