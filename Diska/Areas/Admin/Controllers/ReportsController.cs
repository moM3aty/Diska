using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Text;
using Diska.Models;

namespace Diska.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // لوحة التقارير الرئيسية
        public async Task<IActionResult> Index()
        {
            ViewBag.TotalSales = await _context.Orders.Where(o => o.Status != "Cancelled").SumAsync(o => (decimal?)o.TotalAmount) ?? 0;
            ViewBag.TotalOrders = await _context.Orders.CountAsync();
            ViewBag.TotalProducts = await _context.Products.CountAsync();

            // إحصائية المنتجات حسب الحالة
            ViewBag.ActiveProducts = await _context.Products.CountAsync(p => p.Status == "Active");
            ViewBag.LowStockProducts = await _context.Products.CountAsync(p => p.StockQuantity <= p.LowStockThreshold);

            return View();
        }

        // 1. تقرير المبيعات
        public async Task<IActionResult> Sales(DateTime? fromDate, DateTime? toDate, string status = "All")
        {
            var query = _context.Orders.Include(o => o.User).AsQueryable();

            if (fromDate.HasValue) query = query.Where(o => o.OrderDate >= fromDate.Value);
            if (toDate.HasValue) query = query.Where(o => o.OrderDate <= toDate.Value);
            if (status != "All") query = query.Where(o => o.Status == status);

            var orders = await query.OrderByDescending(o => o.OrderDate).ToListAsync();

            // بيانات للفلتر في الفيو
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
            ViewBag.Status = status;
            ViewBag.TotalRevenue = orders.Sum(o => o.TotalAmount);

            return View(orders);
        }

        // تصدير المبيعات (مع إصلاح اللغة العربية)
        [HttpPost]
        public async Task<IActionResult> ExportSales(DateTime? fromDate, DateTime? toDate, string status)
        {
            var query = _context.Orders.AsQueryable();
            if (fromDate.HasValue) query = query.Where(o => o.OrderDate >= fromDate.Value);
            if (toDate.HasValue) query = query.Where(o => o.OrderDate <= toDate.Value);
            if (!string.IsNullOrEmpty(status) && status != "All") query = query.Where(o => o.Status == status);

            var orders = await query.ToListAsync();
            var builder = new StringBuilder();
            builder.AppendLine("رقم الطلب,العميل,التاريخ,الحالة,طريقة الدفع,الإجمالي");

            foreach (var o in orders)
            {
                string cleanName = o.CustomerName?.Replace(",", " ") ?? "";
                builder.AppendLine($"{o.Id},{cleanName},{o.OrderDate},{o.Status},{o.PaymentMethod},{o.TotalAmount}");
            }

            var encoding = new UTF8Encoding(true);
            var preamble = encoding.GetPreamble();
            var content = encoding.GetBytes(builder.ToString());
            var result = preamble.Concat(content).ToArray();

            return File(result, "text/csv", $"sales_report_{DateTime.Now:yyyyMMdd}.csv");
        }

        // 2. تقرير المخزون
        public async Task<IActionResult> Inventory(string stockStatus = "All")
        {
            var query = _context.Products.Include(p => p.Category).Include(p => p.Merchant).AsQueryable();

            if (stockStatus == "Low") query = query.Where(p => p.StockQuantity <= p.LowStockThreshold && p.StockQuantity > 0);
            else if (stockStatus == "Out") query = query.Where(p => p.StockQuantity == 0);
            else if (stockStatus == "InStock") query = query.Where(p => p.StockQuantity > p.LowStockThreshold);

            var products = await query.OrderBy(p => p.StockQuantity).ToListAsync();
            ViewBag.StockStatus = stockStatus;

            return View(products);
        }

        // تصدير المخزون (مع إصلاح اللغة العربية)
        [HttpPost]
        public async Task<IActionResult> ExportInventory()
        {
            var products = await _context.Products.Include(p => p.Category).ToListAsync();
            var builder = new StringBuilder();
            builder.AppendLine("المعرف,المنتج,القسم,السعر,المخزون,الحالة");

            foreach (var p in products)
            {
                string cleanName = p.Name?.Replace(",", " ") ?? "";
                string cleanCat = p.Category?.Name?.Replace(",", " ") ?? "";
                builder.AppendLine($"{p.Id},{cleanName},{cleanCat},{p.Price},{p.StockQuantity},{p.Status}");
            }

            var encoding = new UTF8Encoding(true);
            var preamble = encoding.GetPreamble();
            var content = encoding.GetBytes(builder.ToString());
            var result = preamble.Concat(content).ToArray();

            return File(result, "text/csv", $"inventory_report_{DateTime.Now:yyyyMMdd}.csv");
        }

        // 3. تقرير النشاط (Activity Log)
        public async Task<IActionResult> Activity()
        {
            // دمج آخر الطلبات والمنتجات المضافة حديثاً كنشاط للنظام
            var recentOrders = await _context.Orders
                .OrderByDescending(o => o.OrderDate)
                .Take(20)
                .Select(o => new ActivityViewModel
                {
                    Type = "Order",
                    Title = $"طلب جديد #{o.Id}",
                    Description = $"العميل {o.CustomerName} قام بطلب بقيمة {o.TotalAmount}",
                    Date = o.OrderDate,
                    User = o.CustomerName
                }).ToListAsync();

            var recentProducts = await _context.Products
                .OrderByDescending(p => p.Id) // Assuming Id implies creation time if no CreatedAt
                .Take(10)
                .Select(p => new ActivityViewModel
                {
                    Type = "Product",
                    Title = "منتج جديد",
                    Description = $"تم إضافة المنتج: {p.Name}",
                    Date = DateTime.Now, // تقريبي لعدم وجود حقل CreatedAt
                    User = "التاجر/الأدمن"
                }).ToListAsync();

            var activities = recentOrders.Concat(recentProducts).OrderByDescending(a => a.Date).ToList();

            return View(activities);
        }
    }

    public class ActivityViewModel
    {
        public string Type { get; set; } // Order, Product, User
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime Date { get; set; }
        public string User { get; set; }
    }
}