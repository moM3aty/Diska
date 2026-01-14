using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Microsoft.EntityFrameworkCore;
using Diska.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace Diska.Controllers
{
    [Authorize]
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public OrderController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // عرض قائمة الطلبات مع الفلترة والتقسيم
        public async Task<IActionResult> Index(string status = "all", int page = 1)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            int pageSize = 10; // عدد الطلبات في الصفحة
            var query = _context.Orders
                .Include(o => o.OrderItems)
                .Where(o => o.UserId == user.Id)
                .AsQueryable();

            // 1. الفلترة
            switch (status.ToLower())
            {
                case "active":
                    query = query.Where(o => o.Status != "Delivered" && o.Status != "Cancelled");
                    break;
                case "completed":
                    query = query.Where(o => o.Status == "Delivered");
                    break;
                case "cancelled":
                    query = query.Where(o => o.Status == "Cancelled");
                    break;
                default: // "all"
                    break;
            }

            // 2. الترتيب والتقسيم
            int totalItems = await query.CountAsync();
            var orders = await query
                .OrderByDescending(o => o.OrderDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // 3. تمرير بيانات التصفح للفيو
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            ViewBag.CurrentStatus = status;

            // إحصائيات سريعة للـ Header
            var allUserOrders = _context.Orders.Where(o => o.UserId == user.Id);
            ViewBag.TotalOrdersCount = await allUserOrders.CountAsync();
            ViewBag.ActiveOrdersCount = await allUserOrders.CountAsync(o => o.Status != "Delivered" && o.Status != "Cancelled");
            ViewBag.TotalSpent = await allUserOrders.Where(o => o.Status != "Cancelled").SumAsync(o => (decimal?)o.TotalAmount) ?? 0;

            return View(orders);
        }

        // تفاصيل الطلب
        public async Task<IActionResult> Details(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == user.Id);

            if (order == null) return NotFound();

            return View(order);
        }
    }
}