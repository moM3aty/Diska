using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Diska.Services;

namespace Diska.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class RestockController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;

        public RestockController(ApplicationDbContext context, INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        // 1. طلبات العملاء (Subscriptions)
        public async Task<IActionResult> Index()
        {
            var requests = await _context.RestockSubscriptions
                .Include(r => r.Product)
                .ThenInclude(p => p.Merchant)
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            ViewBag.ActiveTab = "Requests";
            return View(requests);
        }

        // 2. تنبيهات المخزون المنخفض (Low Stock Alerts)
        public async Task<IActionResult> LowStock()
        {
            var lowStockProducts = await _context.Products
                .Include(p => p.Merchant)
                .Include(p => p.Category)
                .Where(p => p.StockQuantity <= p.LowStockThreshold)
                .OrderBy(p => p.StockQuantity)
                .ToListAsync();

            ViewBag.ActiveTab = "Alerts";
            return View(lowStockProducts);
        }

        // إشعار العميل بتوفر المنتج
        [HttpPost]
        public async Task<IActionResult> MarkAsNotified(int id)
        {
            var req = await _context.RestockSubscriptions
                .Include(r => r.Product)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (req != null)
            {
                req.IsNotified = true;
                await _context.SaveChangesAsync();

                if (!string.IsNullOrEmpty(req.UserId))
                {
                    await _notificationService.NotifyUserAsync(req.UserId, "المنتج توفر!", $"المنتج {req.Product.Name} أصبح متاحاً الآن للشراء.", "Info", $"/Product/Details/{req.ProductId}");
                }
                TempData["Success"] = "تم إشعار العميل بنجاح.";
            }
            return RedirectToAction(nameof(Index));
        }

        // إرسال طلب توريد للتاجر (Supplier Assignment)
        [HttpPost]
        public async Task<IActionResult> NotifyMerchant(int productId, string message)
        {
            var product = await _context.Products.Include(p => p.Merchant).FirstOrDefaultAsync(p => p.Id == productId);
            if (product != null && product.Merchant != null)
            {
                string msgTitle = "طلب توريد مخزون";
                string msgBody = string.IsNullOrEmpty(message)
                    ? $"تنبيه: مخزون المنتج '{product.Name}' منخفض ({product.StockQuantity} قطعة). يرجى تزويد المخزون."
                    : message;

                // إرسال إشعار للنظام
                await _notificationService.NotifyUserAsync(product.MerchantId, msgTitle, msgBody, "Alert", "/Merchant/Restock/Index");
                await _context.SaveChangesAsync();
                TempData["Success"] = $"تم إرسال طلب التوريد للتاجر {product.Merchant.ShopName}.";
            }
            else
            {
                TempData["Error"] = "لم يتم العثور على التاجر أو المنتج.";
            }
            return RedirectToAction(nameof(LowStock));
        }

        // تحديث المخزون السريع (Stock Update Confirmation)
        [HttpPost]
        public async Task<IActionResult> QuickUpdateStock(int productId, int newQuantity)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product != null)
            {
                product.StockQuantity = newQuantity;
                await _context.SaveChangesAsync();

         

                TempData["Success"] = $"تم تحديث مخزون '{product.Name}' إلى {newQuantity}.";
            }
            return RedirectToAction(nameof(LowStock));
        }
    }
}