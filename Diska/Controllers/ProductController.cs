using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Microsoft.EntityFrameworkCore;
using Diska.Models;

namespace Diska.Controllers
{
    public class ProductController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProductController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. صفحة عرض المنتجات (الكتالوج / البحث / الفلترة)
        public async Task<IActionResult> Index(
            string query,           // للبحث
            int? categoryId,        // فلترة بالقسم
            decimal? minPrice,      // أقل سعر
            decimal? maxPrice,      // أعلى سعر
            List<string> merchants, // فلترة بالتجار
            string sort)            // الترتيب
        {
            var productsQuery = _context.Products
                .Include(p => p.Category)
                .Include(p => p.Merchant)
                .Where(p => p.Status == "Active");

            // --- الفلاتر ---

            // 1. البحث
            if (!string.IsNullOrEmpty(query))
            {
                productsQuery = productsQuery.Where(p =>
                    p.Name.Contains(query) ||
                    p.NameEn.Contains(query) ||
                    p.Description.Contains(query) ||
                    p.SKU.Contains(query));
                ViewBag.SearchQuery = query;
            }

            // 2. القسم
            if (categoryId.HasValue)
            {
                productsQuery = productsQuery.Where(p => p.CategoryId == categoryId);
                var category = await _context.Categories.FindAsync(categoryId);
                ViewBag.CategoryName = Thread.CurrentThread.CurrentCulture.Name.StartsWith("ar") ? category?.Name : category?.NameEn;
                ViewBag.CategoryId = categoryId;
            }

            // 3. السعر
            if (minPrice.HasValue) productsQuery = productsQuery.Where(p => p.Price >= minPrice.Value);
            if (maxPrice.HasValue) productsQuery = productsQuery.Where(p => p.Price <= maxPrice.Value);

            // 4. التجار
            if (merchants != null && merchants.Any())
            {
                productsQuery = productsQuery.Where(p => merchants.Contains(p.Merchant.ShopName));
            }

            // --- الترتيب ---
            productsQuery = sort switch
            {
                "price_asc" => productsQuery.OrderBy(p => p.Price),
                "price_desc" => productsQuery.OrderByDescending(p => p.Price),
                "newest" => productsQuery.OrderByDescending(p => p.Id), // أو CreatedAt
                _ => productsQuery.OrderByDescending(p => p.Id) // الافتراضي
            };

            var products = await productsQuery.ToListAsync();

            // --- تجهيز البيانات للواجهة (Dropdowns & Filters) ---

            // قائمة الأقسام للسايدبار
            ViewBag.Categories = await _context.Categories
                .Where(c => c.IsActive && c.ParentId == null)
                .ToListAsync();

            // قائمة التجار المتاحين في النتائج الحالية (للفلترة الذكية)
            ViewBag.AvailableMerchants = products
                .Select(p => p.Merchant.ShopName)
                .Distinct()
                .ToList();

            ViewBag.CurrentSort = sort;

            return View(products);
        }

        // 2. أكشن البحث المباشر (يوجه للـ Index)
        public IActionResult Search(string query)
        {
            return RedirectToAction(nameof(Index), new { query = query });
        }

        // 3. تفاصيل المنتج
        public async Task<IActionResult> Details(int id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Merchant)
                .Include(p => p.Images)
                .Include(p => p.ProductColors)
                .Include(p => p.PriceTiers.OrderBy(t => t.MinQuantity))
                .FirstOrDefaultAsync(p => p.Id == id && p.Status == "Active");

            if (product == null) return NotFound();

            // منتجات ذات صلة (نفس القسم)
            ViewBag.RelatedProducts = await _context.Products
                .Where(p => p.CategoryId == product.CategoryId && p.Id != product.Id && p.Status == "Active")
                .Take(4)
                .ToListAsync();

            // جلب التقييمات
            var reviews = await _context.ProductReviews
                .Include(r => r.User)
                .Where(r => r.ProductId == id && r.IsVisible)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            ViewBag.Reviews = reviews;
            ViewBag.ReviewCount = reviews.Count;
            ViewBag.AverageRating = reviews.Any() ? reviews.Average(r => r.Rating) : 0;

            return View(product);
        }
    }
}