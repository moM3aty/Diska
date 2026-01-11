using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Diska.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Diska.Models;

namespace Diska.Web.Areas.Merchant.Controllers
{
    [Area("Merchant")]
    [Authorize(Roles = "Merchant")] // حماية صارمة
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
            var user = await _userManager.GetUserAsync(User);

            // 1. Data Isolation: جلب البيانات الخاصة بالتاجر فقط

            // المنتجات
            var myProducts = await _context.Products.Where(p => p.MerchantId == user.Id).ToListAsync();
            ViewBag.TotalProducts = myProducts.Count;
            ViewBag.ActiveProducts = myProducts.Count(p => p.Status == "Active");
            ViewBag.LowStock = myProducts.Count(p => p.StockQuantity < 10);

            // الطلبات (التي تحتوي على منتجات التاجر)
            // ملاحظة: الطلب قد يحتوي منتجات لعدة تجار، نحسب فقط قيمة منتجات هذا التاجر
            var myOrderItems = await _context.OrderItems
                .Include(oi => oi.Order)
                .Where(oi => oi.Product.MerchantId == user.Id && oi.Order.Status != "Cancelled")
                .ToListAsync();

            ViewBag.TotalSales = myOrderItems.Sum(oi => oi.UnitPrice * oi.Quantity);
            ViewBag.TotalOrders = myOrderItems.Select(oi => oi.OrderId).Distinct().Count();

            // المحفظة
            ViewBag.WalletBalance = user.WalletBalance;

            // آخر 5 طلبات
            var recentOrders = myOrderItems
                .OrderByDescending(oi => oi.Order.OrderDate)
                .Take(5)
                .Select(oi => new {
                    oi.Order.Id,
                    oi.Order.CustomerName,
                    oi.Order.OrderDate,
                    Status = oi.Order.Status,
                    Total = oi.UnitPrice * oi.Quantity // قيمة الجزء الخاص بالتاجر فقط
                })
                .Distinct()
                .ToList();

            ViewBag.RecentOrders = recentOrders;

            return View();
        }
    }
}