using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Diska.Models;
using Diska.Data;
using Microsoft.EntityFrameworkCore;

namespace Diska.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public IActionResult Index()
        {
            // جلب المنتجات مع التصنيفات لعرضها
            var products = _context.Products.Include(p => p.Category).OrderByDescending(p => p.Id).Take(12).ToList();
            return View(products);
        }

        // دالة البحث الجديدة
        [HttpGet]
        public IActionResult Search(string query)
        {
            var products = _context.Products.Include(p => p.Category).AsQueryable();

            if (!string.IsNullOrEmpty(query))
            {
                // البحث في الاسم، الوصف، واسم التصنيف
                products = products.Where(p => p.Name.Contains(query) || p.Description.Contains(query) || p.Category.Name.Contains(query));
            }

            ViewBag.SearchQuery = query;
            return View(products.ToList());
        }

        // --- الصفحات الثابتة ---
        public IActionResult About() => View();
        public IActionResult FAQ() => View();
        public IActionResult Policies() => View();
        public IActionResult Deals() => View();
        public IActionResult Notifications() => View();

        // --- صفحة اتصل بنا (GET) ---
        [HttpGet]
        public IActionResult Contact()
        {
            return View();
        }

        // --- صفحة اتصل بنا (POST) لحفظ الرسالة ---
        [HttpPost]
        public IActionResult Contact(ContactMessage model)
        {
            if (ModelState.IsValid)
            {
                model.DateSent = DateTime.Now;
                _context.ContactMessages.Add(model);
                _context.SaveChanges();

                // رسالة نجاح للمستخدم (يمكن عرضها بـ ViewBag أو TempData)
                TempData["SuccessMessage"] = "تم استلام رسالتك بنجاح، شكراً لتواصلك معنا.";
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