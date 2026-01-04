using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Microsoft.EntityFrameworkCore;
using Diska.Models;
using Microsoft.AspNetCore.Identity;

namespace Diska.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public DashboardController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public IActionResult Index()
        {
            ViewBag.ProductsCount = _context.Products.Count();
            ViewBag.OrdersCount = _context.Orders.Count();
            ViewBag.UsersCount = _userManager.Users.Count();
            ViewBag.Revenue = _context.Orders.Sum(o => (decimal?)o.TotalAmount) ?? 0;
            var recentOrders = _context.Orders.OrderByDescending(o => o.OrderDate).Take(5).ToList();
            return View(recentOrders);
        }

        public IActionResult Products() => View(_context.Products.Include(p => p.Category).OrderByDescending(p => p.Id).ToList());
        public IActionResult Orders() => View(_context.Orders.OrderByDescending(o => o.OrderDate).ToList());

        // --- إضافة جديدة: تفاصيل الطلب ---
        public IActionResult OrderDetails(int id)
        {
            var order = _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefault(o => o.Id == id);

            if (order == null) return NotFound();

            return View(order);
        }

        [HttpPost]
        public IActionResult UpdateOrderStatus(int id, string status)
        {
            var order = _context.Orders.Find(id);
            if (order != null)
            {
                order.Status = status;
                _context.SaveChanges();
            }
            // إعادة توجيه لنفس صفحة التفاصيل إذا كنا فيها، أو لصفحة الطلبات
            var referer = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(referer)) return Redirect(referer);

            return RedirectToAction("Orders");
        }

        public IActionResult Users() => View(_userManager.Users.ToList());
        public IActionResult Categories() => View(_context.Categories.ToList());

        [HttpPost]
        public IActionResult CreateCategory(Category category) { if (ModelState.IsValid) { _context.Categories.Add(category); _context.SaveChanges(); } return RedirectToAction("Categories"); }
        [HttpPost]
        public IActionResult DeleteCategory(int id) { var cat = _context.Categories.Find(id); if (cat != null) { _context.Categories.Remove(cat); _context.SaveChanges(); } return RedirectToAction("Categories"); }

        public IActionResult Messages() => View(_context.ContactMessages.OrderByDescending(m => m.DateSent).ToList());
        [HttpPost]
        public IActionResult DeleteMessage(int id) { var msg = _context.ContactMessages.Find(id); if (msg != null) { _context.ContactMessages.Remove(msg); _context.SaveChanges(); } return RedirectToAction("Messages"); }
    }
}