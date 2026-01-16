using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Diska.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Threading.Tasks;
using System.Linq;
using System;
using System.Collections.Generic;

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
                .Include(d => d.Category)
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
            ModelState.Remove("Product");
            ModelState.Remove("Category");

            if (ModelState.IsValid)
            {
                // قائمة المنتجات التي سيطبق عليها العرض
                var productsToUpdate = new List<Product>();

                if (model.ProductId.HasValue)
                {
                    var p = await _context.Products.FindAsync(model.ProductId);
                    if (p != null) productsToUpdate.Add(p);
                }
                else if (model.CategoryId.HasValue)
                {
                    productsToUpdate = await _context.Products
                        .Where(p => p.CategoryId == model.CategoryId)
                        .ToListAsync();
                }

                // تطبيق الخصم على المنتجات
                foreach (var product in productsToUpdate)
                {
                    // 1. حساب السعر الجديد
                    decimal newPrice;
                    if (model.IsPercentage)
                        newPrice = product.Price - (product.Price * (model.DiscountValue / 100));
                    else
                        newPrice = product.Price - model.DiscountValue;

                    // 2. تحديث المنتج (إذا كان العرض نشطاً)
                    if (model.IsActive)
                    {
                        // حفظ السعر القديم إذا لم يكن محفوظاً (لتجنب ضياع السعر الأساسي)
                        if (product.OldPrice == null || product.OldPrice == 0)
                        {
                            product.OldPrice = product.Price;
                        }

                        // اعتماد السعر الجديد ليظهر في الموقع
                        product.Price = newPrice;
                        _context.Products.Update(product);
                    }

                    // (اختياري) حفظ سعر العرض في موديل الصفقة للعرض فقط
                    // في حالة القسم، هذا السعر قد يكون غير دقيق لأنه يختلف من منتج لآخر، لكنه مؤشر
                    model.DealPrice = newPrice;
                }

                _context.GroupDeals.Add(model);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"تم إضافة العرض وتحديث {productsToUpdate.Count} منتج.";
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

            PrepareDropdowns(deal.ProductId, deal.CategoryId);
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
                    // أ) استرجاع النسخة القديمة من العرض (قبل التعديل) لإلغاء تأثيرها
                    var oldDeal = await _context.GroupDeals.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);
                    if (oldDeal == null) return NotFound();

                    // --- خطوة 1: استرجاع الأسعار الأصلية (Revert Old Deal) ---
                    var productsToRevert = new List<Product>();
                    if (oldDeal.ProductId.HasValue)
                    {
                        var p = await _context.Products.FindAsync(oldDeal.ProductId);
                        if (p != null) productsToRevert.Add(p);
                    }
                    else if (oldDeal.CategoryId.HasValue)
                    {
                        productsToRevert = await _context.Products.Where(p => p.CategoryId == oldDeal.CategoryId).ToListAsync();
                    }

                    foreach (var p in productsToRevert)
                    {
                        // إذا كان للمنتج سعر قديم محفوظ، نسترجعه
                        if (p.OldPrice != null && p.OldPrice > 0)
                        {
                            p.Price = p.OldPrice.Value;
                            p.OldPrice = null;
                            _context.Products.Update(p);
                        }
                    }
                    // حفظ التغييرات (إعادة الأسعار لطبيعتها) قبل تطبيق العرض الجديد
                    await _context.SaveChangesAsync();


                    // --- خطوة 2: تطبيق العرض الجديد (Apply New Deal) ---
                    if (model.IsActive)
                    {
                        var productsToApply = new List<Product>();
                        if (model.ProductId.HasValue)
                        {
                            // نعيد جلب المنتج لضمان أحدث بيانات بعد الـ Revert
                            var p = await _context.Products.FindAsync(model.ProductId);
                            if (p != null) productsToApply.Add(p);
                        }
                        else if (model.CategoryId.HasValue)
                        {
                            productsToApply = await _context.Products.Where(p => p.CategoryId == model.CategoryId).ToListAsync();
                        }

                        foreach (var p in productsToApply)
                        {
                            decimal discountAmount = model.IsPercentage
                                ? p.Price * (model.DiscountValue / 100)
                                : model.DiscountValue;

                            // السعر الحالي (الذي هو الأصلي الآن بعد الـ Revert) يصبح هو القديم
                            p.OldPrice = p.Price;
                            p.Price = p.Price - discountAmount;

                            _context.Products.Update(p);
                        }
                    }

                    _context.GroupDeals.Update(model);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "تم تحديث العرض وتعديل أسعار المنتجات.";
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

        // 6. Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var deal = await _context.GroupDeals.FindAsync(id);
            if (deal != null)
            {
                // استرجاع الأسعار الأصلية عند الحذف
                var productsToRevert = new List<Product>();

                if (deal.ProductId.HasValue)
                {
                    var p = await _context.Products.FindAsync(deal.ProductId);
                    if (p != null) productsToRevert.Add(p);
                }
                else if (deal.CategoryId.HasValue)
                {
                    productsToRevert = await _context.Products.Where(p => p.CategoryId == deal.CategoryId).ToListAsync();
                }

                foreach (var p in productsToRevert)
                {
                    if (p.OldPrice != null && p.OldPrice > 0)
                    {
                        p.Price = p.OldPrice.Value;
                        p.OldPrice = null;
                        _context.Products.Update(p);
                    }
                }

                _context.GroupDeals.Remove(deal);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حذف العرض واستعادة الأسعار الأصلية.";
            }
            return RedirectToAction(nameof(Index));
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