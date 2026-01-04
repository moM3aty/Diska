using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Microsoft.EntityFrameworkCore;
using Diska.Models;
using Microsoft.AspNetCore.Authorization;

namespace Diska.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // جلب الأقسام للعرض
            ViewBag.Categories = await _context.Categories.ToListAsync();

            // جلب المنتجات المميزة (مثلاً أحدث 12 منتج متوفر)
            var products = await _context.Products
                .Include(p => p.Category)
                .Where(p => p.StockQuantity > 0 && p.ExpiryDate > DateTime.Now)
                .OrderByDescending(p => p.Id)
                .Take(12)
                .ToListAsync();

            return View(products);
        }

        public async Task<IActionResult> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return RedirectToAction(nameof(Index));

            ViewBag.SearchQuery = query;

            var products = await _context.Products
                .Include(p => p.Category)
                .Where(p => p.Name.Contains(query) || p.NameEn.Contains(query) || p.Description.Contains(query))
                .Where(p => p.StockQuantity > 0)
                .ToListAsync();

            return View("Index", products); // إعادة استخدام نفس الـ View لعرض النتائج
        }

        public IActionResult Privacy() => View();
        public IActionResult About() => View();
        public IActionResult Contact() => View();
    }
}