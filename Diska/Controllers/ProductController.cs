using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Microsoft.EntityFrameworkCore;
using Diska.Models;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.AspNetCore.Identity;

namespace Diska.Controllers
{
    public class ProductController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ProductController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // 1. صفحة عرض المنتجات (الكتالوج / البحث / الفلترة)
        public async Task<IActionResult> Index(
            string query,            // للبحث
            int? categoryId,         // فلترة بالقسم
            decimal? minPrice,       // أقل سعر
            decimal? maxPrice,       // أعلى سعر
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

            // 2. القسم (التعديل الأساسي هنا)
            if (categoryId.HasValue)
            {
                // جلب القسم مع أبنائه لمعرفة كل الـ IDs التابعة له
                var category = await _context.Categories
                    .Include(c => c.Children)
                    .FirstOrDefaultAsync(c => c.Id == categoryId);

                if (category != null)
                {
                    // قائمة تحتوي على ID القسم المختار + IDs كل الأقسام الفرعية
                    var targetCategoryIds = new List<int> { category.Id };

                    if (category.Children != null && category.Children.Any())
                    {
                        targetCategoryIds.AddRange(category.Children.Select(c => c.Id));
                    }

                    // جلب المنتجات التي تقع في أي من هذه الأقسام
                    productsQuery = productsQuery.Where(p => targetCategoryIds.Contains(p.CategoryId));

                    ViewBag.CategoryName = CultureInfo.CurrentCulture.Name.StartsWith("ar") ? category.Name : category.NameEn;
                    ViewBag.CategoryId = categoryId;
                }
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
                "newest" => productsQuery.OrderByDescending(p => p.Id),
                _ => productsQuery.OrderByDescending(p => p.Id) // الافتراضي
            };

            var products = await productsQuery.ToListAsync();

            // --- تجهيز البيانات للواجهة ---

            // جلب الأقسام الرئيسية مع أبنائها لعرض الشجرة في السايدبار
            ViewBag.Categories = await _context.Categories
                .Where(c => c.IsActive && c.ParentId == null)
                .Include(c => c.Children) // مهم جداً لعرض الأبناء في القائمة
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();

            // قائمة التجار المتاحين
            ViewBag.AvailableMerchants = products
                .Select(p => p.Merchant.ShopName)
                .Distinct()
                .Where(m => !string.IsNullOrEmpty(m))
                .ToList();

            ViewBag.CurrentSort = sort;

            // Wishlist Check
            var user = await _userManager.GetUserAsync(User);
            var wishlistIds = new List<int>();
            if (user != null)
            {
                wishlistIds = await _context.WishlistItems.Where(w => w.UserId == user.Id).Select(w => w.ProductId).ToListAsync();
            }
            ViewBag.WishlistIds = wishlistIds;

            return View(products);
        }

        // 2. البحث
        public IActionResult Search(string query)
        {
            return RedirectToAction(nameof(Index), new { query = query });
        }

        // 3. التفاصيل
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

            // Related Products
            ViewBag.RelatedProducts = await _context.Products
                .Where(p => p.CategoryId == product.CategoryId && p.Id != product.Id && p.Status == "Active")
                .Take(4)
                .ToListAsync();

            // Reviews
            var reviews = await _context.ProductReviews
                .Include(r => r.User)
                .Where(r => r.ProductId == id && r.IsVisible)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            ViewBag.Reviews = reviews;
            ViewBag.ReviewCount = reviews.Count;
            ViewBag.AverageRating = reviews.Any() ? reviews.Average(r => r.Rating) : 0;

            // Wishlist Check
            var user = await _userManager.GetUserAsync(User);
            bool isWishlisted = false;
            if (user != null)
            {
                isWishlisted = await _context.WishlistItems.AnyAsync(w => w.UserId == user.Id && w.ProductId == id);
            }
            ViewBag.IsWishlisted = isWishlisted;

            return View(product);
        }
    }
}