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
                IsActive = true,
                IsPercentage = true,
                TargetQuantity = 10
            });
        }

        // 3. Create (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(GroupDeal model, string ApplyTo)
        {
            ModelState.Remove("Product");
            ModelState.Remove("Category");

            // منطق تحديد النطاق (هام جداً)
            if (ApplyTo == "Category")
            {
                model.ProductId = null;
                ModelState.Remove("ProductId");
                model.DealPrice = 0; // لا يوجد سعر محدد للقسم ككل
            }
            else
            {
                model.CategoryId = null;
                ModelState.Remove("CategoryId");
            }

            if (ModelState.IsValid)
            {
                // حساب السعر للمنتج فقط
                if (model.ProductId.HasValue)
                {
                    var product = await _context.Products.FindAsync(model.ProductId);
                    if (product != null)
                    {
                        if (model.IsPercentage)
                            model.DealPrice = product.Price - (product.Price * (model.DiscountValue / 100));
                        else
                            model.DealPrice = product.Price - model.DiscountValue;
                    }
                }

                _context.GroupDeals.Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم إضافة العرض بنجاح";
                return RedirectToAction(nameof(Index));
            }

            PrepareDropdowns(model.ProductId, model.CategoryId);
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
        public async Task<IActionResult> Edit(int id, GroupDeal model, string ApplyTo)
        {
            if (id != model.Id) return NotFound();

            ModelState.Remove("Product");
            ModelState.Remove("Category");

            // تصحيح البيانات بناءً على الاختيار
            if (ApplyTo == "Category")
            {
                model.ProductId = null;
                ModelState.Remove("ProductId");
                model.DealPrice = 0;
            }
            else
            {
                model.CategoryId = null;
                ModelState.Remove("CategoryId");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // إعادة الحساب عند التعديل
                    if (model.ProductId.HasValue)
                    {
                        var product = await _context.Products.FindAsync(model.ProductId);
                        if (product != null)
                        {
                            if (model.IsPercentage)
                                model.DealPrice = product.Price - (product.Price * (model.DiscountValue / 100));
                            else
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
                _context.GroupDeals.Remove(deal);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حذف العرض";
            }
            return RedirectToAction(nameof(Index));
        }

        private void PrepareDropdowns(int? selectedProduct = null, int? selectedCategory = null)
        {
            // تحسين العرض في القائمة: الاسم + السعر
            var products = _context.Products
                .Where(p => p.Status == "Active")
                .Select(p => new { p.Id, Name = $"{p.Name} | {p.Price} EGP" })
                .ToList();

            var categories = _context.Categories
                .Select(c => new { c.Id, Name = c.Name })
                .ToList();

            ViewBag.ProductId = new SelectList(products, "Id", "Name", selectedProduct);
            ViewBag.CategoryId = new SelectList(categories, "Id", "Name", selectedCategory);
        }
    }
}