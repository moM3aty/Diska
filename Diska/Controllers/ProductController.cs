using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Microsoft.EntityFrameworkCore;

namespace Diska.Controllers
{
    public class ProductController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProductController(ApplicationDbContext context)
        {
            _context = context;
        }

        // تفاصيل المنتج
        public async Task<IActionResult> Details(int id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.PriceTiers.OrderBy(t => t.MinQuantity)) // ترتيب الشرائح
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null) return NotFound();

            // اقتراح منتجات مشابهة من نفس القسم
            ViewBag.RelatedProducts = await _context.Products
                .Where(p => p.CategoryId == product.CategoryId && p.Id != product.Id)
                .Take(4)
                .ToListAsync();

            return View(product);
        }

        // عرض المنتجات حسب القسم
        public async Task<IActionResult> Category(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound();

            ViewBag.CategoryName = System.Globalization.CultureInfo.CurrentCulture.Name.StartsWith("ar") ? category.Name : (category.NameEn ?? category.Name);

            var products = await _context.Products
                .Where(p => p.CategoryId == id && p.StockQuantity > 0)
                .OrderByDescending(p => p.Id)
                .ToListAsync();

            return View(products);
        }
    }
}