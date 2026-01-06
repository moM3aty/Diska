using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Microsoft.EntityFrameworkCore;
using Diska.Models; // Ensure model namespace is included

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
                .Include(p => p.PriceTiers.OrderBy(t => t.MinQuantity))
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null) return NotFound();

            // جلب التقييمات
            ViewBag.Reviews = await _context.ProductReviews
                .Where(r => r.ProductId == id)
                .Include(r => r.User)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            // حساب المتوسط
            var ratings = await _context.ProductReviews.Where(r => r.ProductId == id).Select(r => r.Rating).ToListAsync();
            ViewBag.AverageRating = ratings.Any() ? ratings.Average() : 0;
            ViewBag.ReviewCount = ratings.Count;

            ViewBag.RelatedProducts = await _context.Products
                .Where(p => p.CategoryId == product.CategoryId && p.Id != product.Id)
                .Take(4)
                .ToListAsync();

            return View(product);
        }

        // عرض القسم مع الفلتر (تم التحديث لتفعيل فلتر الماركة)
        public async Task<IActionResult> Category(int id, decimal? minPrice, decimal? maxPrice, string sort, List<string> brands)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound();

            ViewBag.CategoryName = System.Globalization.CultureInfo.CurrentCulture.Name.StartsWith("ar") ? category.Name : (category.NameEn ?? category.Name);
            ViewBag.CurrentSort = sort;
            ViewBag.CategoryId = id;

            var query = _context.Products
                .Include(p => p.Merchant) // لتصفية الماركات (اسم المحل)
                .Where(p => p.CategoryId == id && p.IsActive && p.StockQuantity > 0);

            // تصفية بالسعر
            if (minPrice.HasValue) query = query.Where(p => p.Price >= minPrice.Value);
            if (maxPrice.HasValue) query = query.Where(p => p.Price <= maxPrice.Value);

            // تصفية بالماركة (اسم التاجر أو براند في الاسم)
            if (brands != null && brands.Any())
            {
                // بحث بسيط: هل اسم المنتج أو اسم التاجر يحتوي على الكلمة
                query = query.Where(p => brands.Contains(p.Merchant.ShopName) || brands.Any(b => p.Name.Contains(b)));
            }

            // الترتيب
            query = sort switch
            {
                "price_asc" => query.OrderBy(p => p.Price),
                "price_desc" => query.OrderByDescending(p => p.Price),
                _ => query.OrderByDescending(p => p.Id)
            };

            var products = await query.ToListAsync();

            // جلب قائمة الماركات المتاحة في هذا القسم للفلتر
            ViewBag.AvailableBrands = await _context.Products
                .Where(p => p.CategoryId == id)
                .Select(p => p.Merchant.ShopName)
                .Distinct()
                .ToListAsync();

            return View(products);
        }
    }
}