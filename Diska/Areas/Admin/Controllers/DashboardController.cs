using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Diska.Data;
using Microsoft.EntityFrameworkCore;
using Diska.Models;
using Microsoft.AspNetCore.Identity;
using Diska.Services;

namespace Diska.Web.Areas.Admin.Controllers
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

        // --- الرئيسية (لوحة القيادة والإحصائيات) ---
        public async Task<IActionResult> Index()
        {
            var today = DateTime.Today;

            // 1. إحصائيات المبيعات والطلبات
            ViewBag.DailySales = await _context.Orders
                .Where(o => o.OrderDate.Date == today && o.Status != "Cancelled")
                .SumAsync(o => (decimal?)o.TotalAmount) ?? 0;

            ViewBag.TotalSales = await _context.Orders
                .Where(o => o.Status != "Cancelled")
                .SumAsync(o => (decimal?)o.TotalAmount) ?? 0;

            ViewBag.TotalOrders = await _context.Orders.CountAsync();
            ViewBag.PendingOrders = await _context.Orders.CountAsync(o => o.Status == "Pending");
            ViewBag.CompletedOrders = await _context.Orders.CountAsync(o => o.Status == "Delivered");

            // 2. إحصائيات النظام
            ViewBag.TotalMerchants = (await _userManager.GetUsersInRoleAsync("Merchant")).Count;
            ViewBag.LowStockItems = await _context.Products.CountAsync(p => p.StockQuantity < 10);
            ViewBag.PendingRequests = await _context.DealRequests.CountAsync(r => r.Status == "Pending");

            // 3. بيانات الرسم البياني (آخر 7 أيام)
            var last7Days = DateTime.Today.AddDays(-6);
            var salesData = await _context.Orders
                .Where(o => o.OrderDate >= last7Days && o.Status != "Cancelled")
                .GroupBy(o => o.OrderDate.Date)
                .Select(g => new { Date = g.Key, Total = g.Sum(x => x.TotalAmount) })
                .ToListAsync();

            // ملء الأيام التي لا يوجد فيها مبيعات بالصفر لضمان استمرارية الرسم البياني
            var chartLabels = new List<string>();
            var chartValues = new List<decimal>();

            for (int i = 0; i < 7; i++)
            {
                var date = last7Days.AddDays(i);
                var record = salesData.FirstOrDefault(x => x.Date == date);
                chartLabels.Add(date.ToString("dd/MM"));
                chartValues.Add(record?.Total ?? 0);
            }

            ViewBag.ChartLabels = chartLabels.ToArray();
            ViewBag.ChartValues = chartValues.ToArray();

            // 4. أحدث الطلبات للعرض في الجدول
            var recentOrders = await _context.Orders
                .Include(o => o.User)
                .OrderByDescending(o => o.OrderDate)
                .Take(10)
                .ToListAsync();

            return View(recentOrders);
        }

        // --- إدارة الطلبات ---
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

        public async Task<IActionResult> OrderDetails(int id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();
            return View(order);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateOrderStatus(int id, string status)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == id);
            if (order != null)
            {
                var oldStatus = order.Status;
                order.Status = status;
                await _context.SaveChangesAsync();

                if (oldStatus != status)
                {
                    string msg = $"تم تحديث حالة طلبك #{id} إلى: {status}";
                    // إشعار العميل
                    await _notificationService.NotifyUserAsync(order.UserId, "تحديث الطلب", msg, "Order", $"/Order/Track/{id}");
                }

                TempData["Success"] = "تم تحديث حالة الطلب بنجاح.";
            }
            return RedirectToAction(nameof(OrderDetails), new { id = id });
        }

        [HttpPost]
        public async Task<IActionResult> RefundUser(string userId, decimal amount, string reason)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                user.WalletBalance += amount;

                _context.WalletTransactions.Add(new WalletTransaction
                {
                    UserId = userId,
                    Amount = amount,
                    Type = "Refund",
                    Description = reason,
                    TransactionDate = DateTime.Now
                });

                await _context.SaveChangesAsync();
                await _userManager.UpdateAsync(user);

                await _notificationService.NotifyUserAsync(userId, "استرداد أموال", $"تم استرداد مبلغ {amount} ج.م إلى محفظتك. السبب: {reason}", "Wallet");

                TempData["Success"] = "تم استرداد المبلغ للمستخدم بنجاح.";
            }
            return RedirectToAction(nameof(Index)); // أو العودة للطلب
        }

        // --- إدارة التجار ---
        public async Task<IActionResult> Merchants()
        {
            var merchants = await _userManager.GetUsersInRoleAsync("Merchant");
            return View(merchants);
        }

        // --- إدارة الرسائل (Support) ---
        public async Task<IActionResult> Messages()
        {
            var messages = await _context.ContactMessages
                .OrderByDescending(m => m.DateSent)
                .ToListAsync();
            return View(messages);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteMessage(int id)
        {
            var msg = await _context.ContactMessages.FindAsync(id);
            if (msg != null)
            {
                _context.ContactMessages.Remove(msg);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حذف الرسالة.";
            }
            return RedirectToAction(nameof(Messages));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteMultipleMessages(List<int> selectedIds)
        {
            if (selectedIds != null && selectedIds.Any())
            {
                var messages = await _context.ContactMessages.Where(m => selectedIds.Contains(m.Id)).ToListAsync();
                _context.ContactMessages.RemoveRange(messages);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"تم حذف {messages.Count} رسالة.";
            }
            return RedirectToAction(nameof(Messages));
        }
    }
}