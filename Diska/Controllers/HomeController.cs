using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Diska.Models;
using Diska.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace Diska.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _logger = logger;
            _context = context;
            _userManager = userManager;
        }

        public IActionResult Index()
        {
            // 1. جلب المنتجات (للعرض في الأسفل)
            var products = _context.Products.Include(p => p.Category).OrderByDescending(p => p.Id).Take(12).ToList();

            // 2. جلب الأقسام (للعرض في الأعلى) - الحل لمشكلة الروابط
            var categories = _context.Categories.ToList();
            ViewBag.Categories = categories;

            return View(products);
        }

        // --- باقي الكود كما هو ---
        public IActionResult MerchantLanding()
        {
            if (User.Identity.IsAuthenticated && User.IsInRole("Merchant"))
            {
                return RedirectToAction("Index", "Merchant");
            }
            return View();
        }

        [HttpGet]
        public IActionResult Search(string query)
        {
            var products = _context.Products.Include(p => p.Category).AsQueryable();

            if (!string.IsNullOrEmpty(query))
            {
                products = products.Where(p => p.Name.Contains(query) || p.Description.Contains(query) || p.Category.Name.Contains(query));
            }

            ViewBag.SearchQuery = query;
            return View(products.ToList());
        }

        public IActionResult Deals()
        {
            var deals = _context.GroupDeals
                .Include(d => d.Product)
                .Where(d => d.IsActive && d.EndDate > DateTime.Now)
                .OrderBy(d => d.EndDate)
                .ToList();

            return View(deals);
        }

        public IActionResult About() => View();
        public IActionResult FAQ() => View();
        public IActionResult Policies() => View();
        public IActionResult Notifications() => View();

        [HttpGet]
        public IActionResult Contact() => View();

        [HttpPost]
        public IActionResult Contact(ContactMessage model)
        {
            if (ModelState.IsValid)
            {
                model.DateSent = DateTime.Now;
                _context.ContactMessages.Add(model);
                _context.SaveChanges();
                TempData["SuccessMessage"] = "تم استلام رسالتك بنجاح";
                return RedirectToAction(nameof(Contact));
            }
            return View(model);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}