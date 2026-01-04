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
        private readonly UserManager<ApplicationUser> _userManager;

        public DashboardController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            // Bird's-eye view stats
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

        public async Task<IActionResult> Orders(string status = "All")
        {
            var query = _context.Orders.Include(o => o.User).AsQueryable();

            if (status != "All")
            {
                query = query.Where(o => o.Status == status);
            }

            var orders = await query.OrderByDescending(o => o.OrderDate).ToListAsync();
            return View(orders);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateOrderStatus(int id, string status)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order != null)
            {
                order.Status = status;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Orders));
        }

        [HttpPost]
        public async Task<IActionResult> RefundUser(string userId, decimal amount, string reason)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null && amount > 0)
            {
                user.WalletBalance += amount;
                await _userManager.UpdateAsync(user);

                // Optional: Log transaction here
            }
            return RedirectToAction(nameof(Index));
        }

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
    }
}