using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Microsoft.EntityFrameworkCore;
using Diska.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Diska.Controllers
{
    public class DealsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DealsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // عرض قائمة الصفقات والعروض النشطة
        public async Task<IActionResult> Index()
        {
            var deals = await _context.GroupDeals
                .Include(d => d.Product)
                .Include(d => d.Category)
                .Where(d => d.IsActive && d.EndDate > DateTime.Now)
                .OrderBy(d => d.EndDate)
                .ToListAsync();

            return View(deals);
        }

        // تفاصيل الصفقة والمنتجات المشمولة فيها
        public async Task<IActionResult> Details(int id)
        {
            var deal = await _context.GroupDeals
                .Include(d => d.Product)
                .Include(d => d.Category)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (deal == null || !deal.IsActive || deal.EndDate < DateTime.Now)
            {
                return NotFound();
            }

         
            var includedProducts = new List<Product>();

            if (deal.ProductId.HasValue)
            {
                // حالة 1: عرض على منتج محدد
                var product = await _context.Products
                    .Include(p => p.Merchant)
                    .Include(p => p.Category)
                    .FirstOrDefaultAsync(p => p.Id == deal.ProductId && p.Status == "Active");

                if (product != null) includedProducts.Add(product);
            }
            else if (deal.CategoryId.HasValue)
            {
                // حالة 2: عرض على قسم كامل
                includedProducts = await _context.Products
                    .Include(p => p.Merchant)
                    .Include(p => p.Category)
                    .Where(p => p.CategoryId == deal.CategoryId && p.Status == "Active")
                    .ToListAsync();
            }

            // تمرير المنتجات للـ View
            // الـ View يجب أن تعرض item.Price كسعر حالي، و item.OldPrice كسعر سابق (إذا وجد)
            ViewBag.IncludedProducts = includedProducts;

            return View(deal);
        }
    }
}