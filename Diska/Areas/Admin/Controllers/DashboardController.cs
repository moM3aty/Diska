using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Microsoft.EntityFrameworkCore;
using Diska.Models;
using Microsoft.AspNetCore.Identity;
using Diska.Services;

namespace Diska.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notificationService;

        public DashboardController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, INotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _notificationService = notificationService;
        }

        // --- الرئيسية ---
        public async Task<IActionResult> Index()
        {
            var today = DateTime.Today;
            ViewBag.DailySales = await _context.Orders
                .Where(o => o.OrderDate.Date == today && o.Status != "Cancelled")
                .SumAsync(o => (decimal?)o.TotalAmount) ?? 0;

            ViewBag.PendingOrders = await _context.Orders.CountAsync(o => o.Status == "Pending");
            ViewBag.TotalMerchants = await _userManager.GetUsersInRoleAsync("Merchant").ContinueWith(t => t.Result.Count);
            ViewBag.LowStockItems = await _context.Products.CountAsync(p => p.StockQuantity < 10);

            var recentOrders = await _context.Orders
                .Include(o => o.User)
                .OrderByDescending(o => o.OrderDate)
                .Take(10)
                .ToListAsync();

            return View(recentOrders);
        }

        // --- الطلبات ---
        public async Task<IActionResult> Orders(string status = "All")
        {
            var query = _context.Orders.Include(o => o.User).AsQueryable();
            if (status != "All") query = query.Where(o => o.Status == status);
            return View(await query.OrderByDescending(o => o.OrderDate).ToListAsync());
        }

        public async Task<IActionResult> OrderDetails(int id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();
            return View(order);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateOrderStatus(int id, string status)
        {
            var order = await _context.Orders.Include(o => o.User).FirstOrDefaultAsync(o => o.Id == id);
            if (order != null)
            {
                order.Status = status;
                await _context.SaveChangesAsync();

                // إشعار المستخدم
                string msg = $"تم تحديث حالة طلبك #{id} إلى: {status}";
                await _notificationService.NotifyUserAsync(order.UserId, "تحديث الطلب", msg, "Order", $"/Order/Track/{id}");
            }
            return RedirectToAction(nameof(Orders));
        }

        // --- التجار ---
        public async Task<IActionResult> Merchants()
        {
            var merchants = await _userManager.GetUsersInRoleAsync("Merchant");
            return View(merchants);
        }

        [HttpPost]
        public async Task<IActionResult> ToggleMerchantStatus(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                user.IsVerifiedMerchant = !user.IsVerifiedMerchant;
                await _userManager.UpdateAsync(user);
            }
            return RedirectToAction(nameof(Merchants));
        }

        // --- المنتجات ---
        public async Task<IActionResult> Products()
        {
            var products = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Merchant)
                .OrderByDescending(p => p.Id)
                .ToListAsync();
            return View(products);
        }

        // --- التصنيفات ---
        public async Task<IActionResult> Categories()
        {
            return View(await _context.Categories.ToListAsync());
        }

        [HttpPost]
        public async Task<IActionResult> CreateCategory(Category category)
        {
            if (ModelState.IsValid)
            {
                _context.Categories.Add(category);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Categories));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var cat = await _context.Categories.FindAsync(id);
            if (cat != null)
            {
                _context.Categories.Remove(cat);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Categories));
        }

        // --- الرسائل ---
        public async Task<IActionResult> Messages()
        {
            return View(await _context.ContactMessages.OrderByDescending(m => m.DateSent).ToListAsync());
        }

        [HttpPost]
        public async Task<IActionResult> DeleteMessage(int id)
        {
            var msg = await _context.ContactMessages.FindAsync(id);
            if (msg != null)
            {
                _context.ContactMessages.Remove(msg);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Messages));
        }
    }
}