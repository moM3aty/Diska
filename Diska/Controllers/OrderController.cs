using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace Diska.Controllers
{
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OrderController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult MyOrders()
        {
            // في الوضع الفعلي نستخدم User.Identity.GetUserId()
            // هنا سنعرض كل الطلبات للتجربة أو طلبات مستخدم "Guest"

            var orders = _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .OrderByDescending(o => o.OrderDate)
                .ToList();

            return View(orders);
        }
    }
}