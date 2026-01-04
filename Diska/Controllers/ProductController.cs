using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Diska.Models;
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

        public IActionResult Details(int id)
        {
            var product = _context.Products.Include(p => p.Category).FirstOrDefault(p => p.Id == id);
            if (product == null) return NotFound();
            return View(product);
        }

        // الأكشن المفقود
        public IActionResult Category(int id, decimal? minPrice, decimal? maxPrice, string sort)
        {
            var category = _context.Categories.Find(id);
            if (category == null) return NotFound();

            var products = _context.Products.Where(p => p.CategoryId == id).AsQueryable();

            // الفلترة
            if (minPrice.HasValue) products = products.Where(p => p.Price >= minPrice.Value);
            if (maxPrice.HasValue) products = products.Where(p => p.Price <= maxPrice.Value);

            // الترتيب
            if (sort == "price_asc") products = products.OrderBy(p => p.Price);
            else if (sort == "price_desc") products = products.OrderByDescending(p => p.Price);
            else products = products.OrderByDescending(p => p.Id); // Default new

            ViewBag.CategoryName = category.Name;
            ViewBag.CategoryId = id;

            return View(products.ToList());
        }
    }
}