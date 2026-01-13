using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Diska.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using Diska.Models;

namespace Diska.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
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

        // 3. تفاصيل الطلب (تم التأكد من جلب OrderItems ببياناتها)
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
    }
}