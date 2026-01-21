using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Diska.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Diska.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace Diska.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
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

        // 1. عرض الصفقات (Index)
        public async Task<IActionResult> Index(string status = "All")
        {
            var dealsQuery = _context.GroupDeals
                .Include(d => d.Product).ThenInclude(p => p.Merchant)
                .Include(d => d.Category)
                .AsQueryable();

            if (status != "All")
            {
                dealsQuery = dealsQuery.Where(d => d.Status == status);
            }

            var deals = await dealsQuery.OrderByDescending(d => d.Id).ToListAsync();

            ViewBag.CurrentStatus = status;
            return View(deals);
        }

        // 2. إنشاء صفقة (Create GET)
        [HttpGet]
        public IActionResult Create()
        {
            PrepareDropdowns();
            return View(new GroupDeal
            {
                StartDate = DateTime.Now,
                EndDate = DateTime.Now.AddDays(7),
                IsActive = true
            });
        }

        // 3. إنشاء صفقة (Create POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(GroupDeal model)
        {
            ModelState.Remove("Product");
            ModelState.Remove("Category");

            if (ModelState.IsValid)
            {
                // إذا أنشأ الأدمن الصفقة، فهي معتمدة تلقائياً
                model.Status = "Approved";

                if (model.IsActive)
                {
                    await ApplyDealPrices(model);
                }

                _context.GroupDeals.Add(model);
                await _context.SaveChangesAsync();

                TempData["Success"] = "تم إضافة العرض بنجاح.";
                return RedirectToAction(nameof(Index));
            }

            PrepareDropdowns();
            return View(model);
        }

        // 4. تعديل صفقة (Edit GET)
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var deal = await _context.GroupDeals.FindAsync(id);
            if (deal == null) return NotFound();

            PrepareDropdowns(deal.ProductId, deal.CategoryId);
            return View(deal);
        }

        // 5. تعديل صفقة (Edit POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, GroupDeal model)
        {
            if (id != model.Id) return NotFound();

            ModelState.Remove("Product");
            ModelState.Remove("Category");

            if (ModelState.IsValid)
            {
                try
                {
                    // استرجاع الصفقة القديمة لإلغاء تأثيرها أولاً
                    var oldDeal = await _context.GroupDeals.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);
                    if (oldDeal != null)
                    {
                        await RevertDealPrices(oldDeal);
                    }

                    // تطبيق التغييرات الجديدة
                    if (model.IsActive && model.Status == "Approved")
                    {
                        await ApplyDealPrices(model);
                    }

                    _context.GroupDeals.Update(model);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "تم تحديث العرض بنجاح.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.GroupDeals.Any(e => e.Id == id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }

            PrepareDropdowns(model.ProductId, model.CategoryId);
            return View(model);
        }

        // 6. الموافقة على صفقة (Approve) - للأدمن
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var deal = await _context.GroupDeals
                .Include(d => d.Product)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (deal == null) return NotFound();

            // تطبيق الأسعار عند الموافقة
            deal.Status = "Approved";
            deal.IsActive = true;
            await ApplyDealPrices(deal);

            _context.GroupDeals.Update(deal);
            await _context.SaveChangesAsync();

            // إشعار للتاجر
            string merchantId = deal.Product?.MerchantId;
            if (!string.IsNullOrEmpty(merchantId))
            {
                await _notificationService.NotifyUserAsync(merchantId, "تمت الموافقة ✅", $"تمت الموافقة على صفقتك '{deal.Title}' وتطبيق الخصومات.", "Deal");
            }

            TempData["Success"] = "تمت الموافقة وتطبيق الأسعار.";
            return RedirectToAction(nameof(Index), new { status = "Pending" });
        }

        // 7. رفض صفقة (Reject)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id)
        {
            var deal = await _context.GroupDeals.Include(d => d.Product).FirstOrDefaultAsync(d => d.Id == id);
            if (deal == null) return NotFound();

            // إذا كانت مفعلة سابقاً، نلغي تأثيرها
            if (deal.Status == "Approved" || deal.IsActive)
            {
                await RevertDealPrices(deal);
            }

            deal.Status = "Rejected";
            deal.IsActive = false;

            await _context.SaveChangesAsync();

            string merchantId = deal.Product?.MerchantId;
            if (!string.IsNullOrEmpty(merchantId))
            {
                await _notificationService.NotifyUserAsync(merchantId, "تم الرفض ❌", $"تم رفض صفقتك '{deal.Title}'.", "Deal");
            }

            TempData["Success"] = "تم رفض الصفقة.";
            return RedirectToAction(nameof(Index));
        }

        // 8. حذف صفقة (Delete)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var deal = await _context.GroupDeals.FindAsync(id);
            if (deal != null)
            {
                // استعادة الأسعار الأصلية قبل الحذف
                await RevertDealPrices(deal);

                _context.GroupDeals.Remove(deal);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حذف الصفقة واستعادة الأسعار الأصلية.";
            }
            return RedirectToAction(nameof(Index));
        }

        // --- دوال مساعدة (Helpers) ---

        // دالة لتطبيق الخصم على المنتجات
        private async Task ApplyDealPrices(GroupDeal deal)
        {
            var products = await GetProductsForDeal(deal);

            foreach (var p in products)
            {
                // حفظ السعر القديم فقط إذا لم يكن محفوظاً (لتجنب الكتابة عليه بخصم فوق خصم)
                if (p.OldPrice == null || p.OldPrice == 0)
                {
                    p.OldPrice = p.Price;
                }

                decimal discountAmount = deal.IsPercentage
                    ? p.OldPrice.Value * (deal.DiscountValue / 100)
                    : deal.DiscountValue;

                p.Price = p.OldPrice.Value - discountAmount;
                if (p.Price < 0) p.Price = 0; // حماية

                _context.Products.Update(p);
            }
            // ملاحظة: الحفظ يتم في الدالة المستدعية (Save outside loop usually better, but here we update context tracking)
        }

        // دالة لاستعادة الأسعار الأصلية
        private async Task RevertDealPrices(GroupDeal deal)
        {
            var products = await GetProductsForDeal(deal);

            foreach (var p in products)
            {
                if (p.OldPrice != null && p.OldPrice > 0)
                {
                    p.Price = p.OldPrice.Value;
                    p.OldPrice = null; // إزالة السعر القديم لأنه عاد لسعره الأصلي
                    _context.Products.Update(p);
                }
            }
        }

        private async Task<List<Product>> GetProductsForDeal(GroupDeal deal)
        {
            if (deal.ProductId.HasValue)
            {
                var p = await _context.Products.FindAsync(deal.ProductId);
                return p != null ? new List<Product> { p } : new List<Product>();
            }
            else if (deal.CategoryId.HasValue)
            {
                return await _context.Products.Where(p => p.CategoryId == deal.CategoryId).ToListAsync();
            }
            return new List<Product>();
        }

        private void PrepareDropdowns(int? selectedProduct = null, int? selectedCategory = null)
        {
            var products = _context.Products
                .Where(p => p.Status == "Active")
                .Select(p => new { p.Id, Name = p.Name + " (" + p.Price + " ج.م)" })
                .ToList();
            ViewBag.ProductId = new SelectList(products, "Id", "Name", selectedProduct);

            var categories = _context.Categories
                .Select(c => new { c.Id, c.Name })
                .ToList();
            ViewBag.CategoryId = new SelectList(categories, "Id", "Name", selectedCategory);
        }
    }
}