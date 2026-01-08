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
            ViewBag.Categories = await _context.Categories.ToListAsync();

            ViewBag.Banners = await _context.Banners
                .Where(b => b.IsActive)
                .OrderBy(b => b.DisplayOrder)
                .ToListAsync();

            var products = await _context.Products
                .Include(p => p.Category)
                .Where(p => p.StockQuantity > 0 && p.IsActive)
                .OrderByDescending(p => p.Id)
                .Take(12)
                .ToListAsync();

            return View(products);
        }

        public async Task<IActionResult> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return RedirectToAction(nameof(Index));

            ViewBag.SearchQuery = query;
            ViewBag.Categories = await _context.Categories.ToListAsync();

            var products = await _context.Products
                .Include(p => p.Category)
                .Where(p => (p.Name.Contains(query) || p.NameEn.Contains(query) || p.Description.Contains(query)) && p.IsActive)
                .Where(p => p.StockQuantity > 0)
                .ToListAsync();

            return View("Index", products);
        }

        public async Task<IActionResult> Deals()
        {
            var deals = await _context.GroupDeals
                .Include(d => d.Product)
                .Where(d => d.IsActive)
                .ToListAsync();
            return View(deals);
        }

        [Authorize]
        public IActionResult Notifications()
        {
            return RedirectToAction("Index", "Notification");
        }

        public IActionResult Privacy() => View();
        public IActionResult About() => View();
        public IActionResult Contact() => View();
        public IActionResult FAQ() => View();
        public IActionResult Policies() => View();
        public IActionResult Terms() => View(); 
        public IActionResult MerchantLanding() => View();
    }
}