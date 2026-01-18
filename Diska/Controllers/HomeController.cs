using Diska.Data;
using Diska.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Diska.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public HomeController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            if (User.Identity.IsAuthenticated)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
                    var userRole = await _userManager.IsInRoleAsync(user, "Merchant") ? "Merchant" : "Customer";

                    // البحث عن استبيان متاح
                    var pendingSurvey = await _context.Surveys
                        .Where(s => s.IsActive && s.EndDate > DateTime.Now && (s.TargetAudience == "All" || s.TargetAudience == userRole))
                        .Where(s => !_context.SurveyResponses.Any(r => r.SurveyId == s.Id && r.UserId == user.Id))
                        .FirstOrDefaultAsync();

                    if (pendingSurvey != null)
                    {
                        ViewBag.ShowSurveyPopup = true;
                        ViewBag.PopupSurveyId = pendingSurvey.Id;
                        ViewBag.PopupSurveyTitle = CultureInfo.CurrentCulture.Name.StartsWith("ar") ? pendingSurvey.Title : (pendingSurvey.TitleEn ?? pendingSurvey.Title);
                    }
                }
            }
            ViewBag.Categories = await _context.Categories
                .Where(c => c.IsActive && c.ParentId == null)
                .OrderBy(c => c.DisplayOrder)
                .Take(8)
                .ToListAsync();

            ViewBag.Banners = await _context.Banners
                .Where(b => b.IsActive && b.StartDate <= DateTime.Now && b.EndDate >= DateTime.Now)
                .OrderBy(b => b.Priority)
                .ToListAsync();

            var products = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Merchant)
                .Where(p => p.Status == "Active" && p.StockQuantity > 0)
                .OrderByDescending(p => p.Id)
                .Take(12)
                .ToListAsync();

            return View(products);
        }

        public async Task<IActionResult> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return RedirectToAction(nameof(Index));

            ViewBag.SearchQuery = query;

            var products = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Merchant)
                .Where(p => p.Status == "Active" &&
                            (p.Name.Contains(query) || p.NameEn.Contains(query) ||
                             p.Description.Contains(query) || p.SKU.Contains(query)))
                .ToListAsync();

            ViewBag.CategoryName = "نتائج البحث";
            return View("~/Views/Product/Index.cshtml", products);
        }

    

        public IActionResult About() => View();
        public IActionResult Contact() => View(); 
        public IActionResult FAQ() => View();
        public IActionResult Policies() => View();
        public IActionResult Privacy() => View();
        public IActionResult Terms() => View();

        public IActionResult MerchantLanding()
        {
            if (User.IsInRole("Merchant"))
            {
                return RedirectToAction("Index", "Dashboard", new { area = "Merchant" });
            }
            return View();
        }

        [HttpPost]
        public IActionResult SetLanguage(string culture, string returnUrl)
        {
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) }
            );

            return LocalRedirect(returnUrl ?? "/");
        }
    }
}