using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Diska.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;

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

        public async Task<IActionResult> Index()
        {
            var deals = await _context.GroupDeals
                .Include(d => d.Product)
                .Include(d => d.Category)
                .OrderByDescending(d => d.EndDate)
                .ToListAsync();
            return View(deals);
        }

        [HttpGet]
        public IActionResult Create()
        {
            PrepareViewBags();
            return View(new GroupDeal
            {
                StartDate = DateTime.Now,
                EndDate = DateTime.Now.AddDays(7),
                TargetQuantity = 10,
                IsActive = true
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(GroupDeal deal, string ApplyTo)
        {
            // تنظيف الـ Validation بناءً على الاختيار
            if (ApplyTo == "Category") ModelState.Remove("ProductId");
            else ModelState.Remove("CategoryId");

            ModelState.Remove("Product");
            ModelState.Remove("Category");

            if (ModelState.IsValid)
            {
                // ضبط الحقول الفارغة
                if (ApplyTo == "Category") deal.ProductId = null;
                else deal.CategoryId = null;

                // إذا كان خصم ثابت، نستخدم DiscountValue كـ DealPrice أو العكس حسب منطق العمل
                // هنا سنفترض أن DealPrice هو السعر النهائي إذا كان منتج، و DiscountValue هي القيمة المدخلة

                deal.ReservedQuantity = 0;
                _context.GroupDeals.Add(deal);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم إنشاء الصفقة بنجاح";
                return RedirectToAction(nameof(Index));
            }

            PrepareViewBags();
            return View(deal);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var deal = await _context.GroupDeals.FindAsync(id);
            if (deal == null) return NotFound();

            PrepareViewBags();
            return View(deal);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, GroupDeal deal, string ApplyTo)
        {
            if (id != deal.Id) return NotFound();

            if (ApplyTo == "Category") ModelState.Remove("ProductId");
            else ModelState.Remove("CategoryId");
            ModelState.Remove("Product");
            ModelState.Remove("Category");

            if (ModelState.IsValid)
            {
                try
                {
                    if (ApplyTo == "Category") deal.ProductId = null;
                    else deal.CategoryId = null;

                    _context.Update(deal);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "تم تحديث الصفقة";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.GroupDeals.Any(e => e.Id == id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            PrepareViewBags();
            return View(deal);
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var deal = await _context.GroupDeals.FindAsync(id);
            if (deal != null)
            {
                _context.GroupDeals.Remove(deal);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حذف الصفقة";
            }
            return RedirectToAction(nameof(Index));
        }

        private void PrepareViewBags()
        {
            ViewBag.Products = new SelectList(_context.Products.Where(p => p.IsActive), "Id", "Name");
            ViewBag.Categories = new SelectList(_context.Categories.Where(c => c.IsActive), "Id", "Name");
        }
    }
}