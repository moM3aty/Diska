using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

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

        public async Task<IActionResult> Index()
        {
            // إحصائيات عامة
            var totalOrders = await _context.Orders.CountAsync();
            var totalSales = await _context.Orders
                .Where(o => o.Status != "Cancelled")
                .SumAsync(o => (decimal?)o.TotalAmount) ?? 0;

            var completedOrders = await _context.Orders.CountAsync(o => o.Status == "Delivered");
            var pendingOrders = await _context.Orders.CountAsync(o => o.Status == "Pending");

            // المنتجات الأكثر مبيعاً
            var topProducts = await _context.OrderItems
                .Include(oi => oi.Product)
                .GroupBy(oi => oi.ProductId)
                .Select(g => new
                {
                    ProductName = g.First().Product.Name,
                    SoldQuantity = g.Sum(x => x.Quantity),
                    TotalRevenue = g.Sum(x => x.Quantity * x.UnitPrice)
                })
                .OrderByDescending(x => x.SoldQuantity)
                .Take(5)
                .ToListAsync();

            ViewBag.TotalOrders = totalOrders;
            ViewBag.TotalSales = totalSales;
            ViewBag.CompletedOrders = completedOrders;
            ViewBag.PendingOrders = pendingOrders;
            ViewBag.TopProducts = topProducts;

            return View();
        }
    }
}