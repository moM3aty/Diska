using Diska.Data;
using Diska.Models;
using Diska.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        // 1. الصفحة الرئيسية
        public async Task<IActionResult> Index()
        {
            ViewBag.TotalSales = await _context.Orders.Where(o => o.Status != "Cancelled").SumAsync(o => (decimal?)o.TotalAmount) ?? 0;
            ViewBag.TotalOrders = await _context.Orders.CountAsync();
            ViewBag.PendingOrders = await _context.Orders.CountAsync(o => o.Status == "Pending");
            ViewBag.CompletedOrders = await _context.Orders.CountAsync(o => o.Status == "Delivered");
            ViewBag.TotalMerchants = await _context.Users.CountAsync(u => u.IsVerifiedMerchant);
            ViewBag.LowStockItems = await _context.Products.CountAsync(p => p.StockQuantity <= p.LowStockThreshold);
            ViewBag.PendingRequests = await _context.DealRequests.CountAsync(r => r.Status == "Pending");

            var last7Days = DateTime.Now.AddDays(-6).Date;
            var salesData = await _context.Orders
                .Where(o => o.OrderDate >= last7Days && o.Status != "Cancelled")
                .GroupBy(o => o.OrderDate.Date)
                .Select(g => new { Date = g.Key, Total = g.Sum(o => o.TotalAmount) })
                .ToListAsync();

            var labels = new List<string>();
            var values = new List<decimal>();

            for (int i = 0; i < 7; i++)
            {
                var date = last7Days.AddDays(i);
                labels.Add(date.ToString("dd MMM"));
                var record = salesData.FirstOrDefault(s => s.Date == date);
                values.Add(record?.Total ?? 0);
            }

            ViewBag.ChartLabels = labels;
            ViewBag.ChartValues = values;

            var recentOrders = await _context.Orders
                .OrderByDescending(o => o.OrderDate)
                .Take(5)
                .ToListAsync();

            return View(recentOrders);
        }

        // 2. إدارة الطلبات
        public async Task<IActionResult> Orders(string status, string q, DateTime? date)
        {
            var query = _context.Orders.AsQueryable();

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(o => o.Status == status);
            }

            if (!string.IsNullOrEmpty(q))
            {
                query = query.Where(o => o.Id.ToString().Contains(q) || o.CustomerName.Contains(q) || o.Phone.Contains(q));
            }

            if (date.HasValue)
            {
                query = query.Where(o => o.OrderDate.Date == date.Value.Date);
            }

            var orders = await query.OrderByDescending(o => o.OrderDate).ToListAsync();
            return View(orders);
        }

        public async Task<IActionResult> OrderDetails(int id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product) // جلب المنتج
                    .ThenInclude(p => p.ProductColors) // إضافة: جلب قائمة ألوان المنتج (للمرجع)
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();

            return View(order);
        }

        // 4. تحديث الحالة
        [HttpPost]
        public async Task<IActionResult> UpdateOrderStatus(int id, string status)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();

            order.Status = status;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"تم تحديث حالة الطلب #{id} إلى {status}";
            return RedirectToAction(nameof(OrderDetails), new { id = id });
        }
        [HttpPost]
        public async Task<IActionResult> ExportOrdersToExcel(string status, string q, DateTime? date)
        {
            var query = _context.Orders.AsQueryable();
            if (!string.IsNullOrEmpty(status)) query = query.Where(o => o.Status == status);
            if (!string.IsNullOrEmpty(q)) query = query.Where(o => o.Id.ToString().Contains(q) || o.CustomerName.Contains(q));
            if (date.HasValue) query = query.Where(o => o.OrderDate.Date == date.Value.Date);

            var orders = await query.OrderByDescending(o => o.OrderDate).ToListAsync();
            var builder = new StringBuilder();
            builder.Append('\uFEFF');
            builder.AppendLine("رقم الطلب,العميل,الهاتف,التاريخ,الحالة,طريقة الدفع,الإجمالي,العنوان");
            foreach (var o in orders)
            {
                var line = string.Join(",", o.Id, EscapeCsv(o.CustomerName), EscapeCsv(o.Phone), o.OrderDate.ToString("yyyy-MM-dd HH:mm"), o.Status, o.PaymentMethod, o.TotalAmount, EscapeCsv($"{o.Governorate} - {o.City} - {o.Address}"));
                builder.AppendLine(line);
            }
            return File(Encoding.UTF8.GetBytes(builder.ToString()), "text/csv", $"orders_report_{DateTime.Now:yyyyMMdd}.csv");
        }

        private string EscapeCsv(string field)
        {
            if (string.IsNullOrEmpty(field)) return "";
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        public async Task<IActionResult> Merchants(string q)
        {


            var merchants = await _userManager.GetUsersInRoleAsync("Merchant");

            // تحويل لـ List للفلترة
            var merchantsList = merchants.AsQueryable();

            if (!string.IsNullOrEmpty(q))
            {
                merchantsList = merchantsList.Where(m =>
                    (m.ShopName != null && m.ShopName.Contains(q)) ||
                    (m.FullName != null && m.FullName.Contains(q)) ||
                    (m.Email != null && m.Email.Contains(q)) ||
                    (m.PhoneNumber != null && m.PhoneNumber.Contains(q))
                );
            }

            // إحصائيات سريعة للصفحة
            ViewBag.TotalCount = merchantsList.Count();
            ViewBag.VerifiedCount = merchantsList.Count(m => m.IsVerifiedMerchant);
            ViewBag.TotalBalance = merchantsList.Sum(m => m.WalletBalance);

            return View(merchantsList.ToList());
        }

        // أكشن لتوثيق التاجر سريعاً
        [HttpPost]
        public async Task<IActionResult> VerifyMerchant(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                user.IsVerifiedMerchant = !user.IsVerifiedMerchant; // Toggle
                await _userManager.UpdateAsync(user);
                TempData["Success"] = user.IsVerifiedMerchant ? "تم توثيق التاجر بنجاح" : "تم إلغاء توثيق التاجر";
            }
            return RedirectToAction(nameof(Merchants));
        }

        public async Task<IActionResult> Messages()
        {
            var messages = await _context.ContactMessages.OrderByDescending(m => m.DateSent).ToListAsync();
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
    }
}
