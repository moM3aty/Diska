using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Microsoft.EntityFrameworkCore;
using Diska.Models;

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

            // جلب المنتجات المشمولة في العرض
            var includedProducts = new List<Product>();

            if (deal.ProductId.HasValue)
            {
                // عرض على منتج محدد
                var product = await _context.Products
                    .Include(p => p.Merchant)
                    .Include(p => p.Category)
                    .FirstOrDefaultAsync(p => p.Id == deal.ProductId && p.Status == "Active");

                if (product != null) includedProducts.Add(product);
            }
            else if (deal.CategoryId.HasValue)
            {
                // عرض على قسم كامل
                includedProducts = await _context.Products
                    .Include(p => p.Merchant)
                    .Include(p => p.Category)
                    .Where(p => p.CategoryId == deal.CategoryId && p.Status == "Active")
                    .ToListAsync();
            }

            ViewBag.IncludedProducts = includedProducts;

            return View(deal);
        }
    }
}