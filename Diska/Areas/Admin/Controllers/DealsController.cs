using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Diska.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Threading.Tasks;
using System.Linq;
using System;

namespace Diska.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class DealsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DealsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. Index
        public async Task<IActionResult> Index()
        {
            var deals = await _context.GroupDeals
                .Include(d => d.Product)
                .OrderByDescending(d => d.EndDate)
                .ToListAsync();
            return View(deals);
        }

        // 2. Create (GET)
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

        // 3. Create (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(GroupDeal model)
        {
            // تنظيف التحقق للحقول الاختيارية
            ModelState.Remove("Product");
            ModelState.Remove("Category");

            if (ModelState.IsValid)
            {
                // حساب السعر بعد الخصم للعرض (اختياري، يمكن حسابه ديناميكياً)
                var product = await _context.Products.FindAsync(model.ProductId);
                if (product != null)
                {
                    if (model.IsPercentage)
                    {
                        model.DealPrice = product.Price - (product.Price * (model.DiscountValue / 100));
                    }
                    else
                    {
                        model.DealPrice = product.Price - model.DiscountValue;
                    }
                }

                _context.GroupDeals.Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم إضافة العرض بنجاح";
                return RedirectToAction(nameof(Index));
            }

            PrepareDropdowns();
            return View(model);
        }

        // 4. Edit (GET)
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var deal = await _context.GroupDeals.FindAsync(id);
            if (deal == null) return NotFound();

            PrepareDropdowns(deal.ProductId);
            return View(deal);
        }

        // 5. Edit (POST)
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
                    // إعادة حساب السعر في حال تغير الخصم
                    var product = await _context.Products.FindAsync(model.ProductId);
                    if (product != null)
                    {
                        if (model.IsPercentage)
                        {
                            model.DealPrice = product.Price - (product.Price * (model.DiscountValue / 100));
                        }
                        else
                        {
                            model.DealPrice = product.Price - model.DiscountValue;
                        }
                    }

                    _context.Update(model);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "تم تعديل العرض بنجاح";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.GroupDeals.Any(e => e.Id == id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }

            PrepareDropdowns(model.ProductId);
            return View(model);
        }

        // 6. Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var deal = await _context.GroupDeals.FindAsync(id);
            if (deal != null)
            {
                _context.GroupDeals.Remove(deal);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حذف العرض";
            }
            return RedirectToAction(nameof(Index));
        }

        private void PrepareDropdowns(int? selectedProduct = null)
        {
            // جلب المنتجات النشطة فقط
            var products = _context.Products
                .Where(p => p.Status == "Active")
                .Select(p => new { p.Id, Name = p.Name + " (" + p.Price + " ج.م)" })
                .ToList();

            ViewBag.ProductId = new SelectList(products, "Id", "Name", selectedProduct);
        }
    }
}