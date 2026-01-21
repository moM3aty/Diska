using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Diska.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using Diska.Services;
using System.Linq;
using System.Threading.Tasks;

namespace Diska.Areas.Merchant.Controllers
{
    [Area("Merchant")]
    [Authorize(Roles = "Merchant")]
    public class DealController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notificationService;

        public DealController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, INotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _notificationService = notificationService;
        }

        // عرض صفقات التاجر وحالتها
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var deals = await _context.GroupDeals
                .Include(d => d.Product)
                .Include(d => d.Category)
                .Where(d => d.Product.MerchantId == user.Id || (d.Category != null && d.Product == null)) // افتراضاً الصفقات مرتبطة بمنتجات التاجر
                .OrderByDescending(d => d.StartDate)
                .ToListAsync();

            return View(deals);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var user = await _userManager.GetUserAsync(User);
            // جلب منتجات التاجر فقط
            ViewBag.Products = new SelectList(_context.Products.Where(p => p.MerchantId == user.Id), "Id", "Name");
            // جلب الأقسام
            ViewBag.Categories = new SelectList(_context.Categories.Where(c => c.ParentId == null), "Id", "Name");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(GroupDeal deal)
        {
            var user = await _userManager.GetUserAsync(User);

            // التحقق من صحة المنتج إذا تم اختياره
            if (deal.ProductId.HasValue)
            {
                var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == deal.ProductId && p.MerchantId == user.Id);
                if (product == null)
                {
                    ModelState.AddModelError("ProductId", "المنتج غير صالح أو لا تملكه.");
                }
            }
            ModelState.Remove("Product");
            ModelState.Remove("Category");
            if (ModelState.IsValid)
            {
                deal.ReservedQuantity = 0;

                // التأكد من عدم تعارض المواعيد
                if (deal.EndDate <= deal.StartDate)
                {
                    TempData["Error"] = "تاريخ الانتهاء يجب أن يكون بعد تاريخ البدء.";
                    return RedirectToAction(nameof(Create));
                }

                _context.GroupDeals.Add(deal);
                await _context.SaveChangesAsync();

                // إشعار للإدارة
                await _notificationService.NotifyAdminsAsync("صفقة جديدة للمراجعة", $"التاجر {user.ShopName} أضاف صفقة جديدة: {deal.Title}", "Deal");

                TempData["Success"] = "تم إنشاء الصفقة وإرسالها للمراجعة من قبل الإدارة.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Products = new SelectList(_context.Products.Where(p => p.MerchantId == user.Id), "Id", "Name");
            ViewBag.Categories = new SelectList(_context.Categories.Where(c => c.ParentId == null), "Id", "Name");
            return View(deal);
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var deal = await _context.GroupDeals
                .Include(d => d.Product)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (deal != null && (deal.Product?.MerchantId == user.Id || deal.Product == null))
            {
                _context.GroupDeals.Remove(deal);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حذف الصفقة.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}