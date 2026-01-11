using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Diska.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Diska.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class SearchController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SearchController(ApplicationDbContext context)
        {
            _context = context;
        }

        // البحث الشامل
        [HttpGet]
        public async Task<IActionResult> Index(string q)
        {
            // إذا كان البحث فارغاً، نعيد المستخدم للوحة القيادة أو نعرض صفحة فارغة
            if (string.IsNullOrWhiteSpace(q))
            {
                return RedirectToAction("Index", "Dashboard");
                // أو return View("Results", new GlobalSearchResultsViewModel { Query = "" });
            }

            var viewModel = new GlobalSearchResultsViewModel
            {
                Query = q
            };

            // 1. البحث في المنتجات (الاسم، الوصف، SKU)
            viewModel.Products = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Merchant)
                .Where(p => p.Name.Contains(q) || p.NameEn.Contains(q) || p.SKU.Contains(q) || p.Description.Contains(q))
                .Take(10) // نكتفي بأول 10 نتائج
                .ToListAsync();

            // 2. البحث في الطلبات (رقم الطلب، اسم العميل، الهاتف)
            if (int.TryParse(q, out int orderId))
            {
                // إذا كان البحث رقمياً، نبحث عن رقم الطلب بدقة
                viewModel.Orders = await _context.Orders
                    .Include(o => o.User)
                    .Where(o => o.Id == orderId)
                    .ToListAsync();
            }
            else
            {
                // بحث نصي في بيانات العميل
                viewModel.Orders = await _context.Orders
                    .Include(o => o.User)
                    .Where(o => o.CustomerName.Contains(q) || o.Phone.Contains(q) || o.Governorate.Contains(q))
                    .OrderByDescending(o => o.OrderDate)
                    .Take(10)
                    .ToListAsync();
            }

            // 3. البحث في المستخدمين (الاسم، الإيميل، الهاتف، اسم المتجر)
            viewModel.Users = await _context.Users
                .Where(u => u.FullName.Contains(q) || u.Email.Contains(q) || u.PhoneNumber.Contains(q) || u.ShopName.Contains(q))
                .Take(10)
                .ToListAsync();

            return View("Results", viewModel);
        }
    }

    // ViewModel لتجميع النتائج
    public class GlobalSearchResultsViewModel
    {
        public string Query { get; set; }
        public List<Product> Products { get; set; } = new List<Product>();
        public List<Order> Orders { get; set; } = new List<Order>();
        public List<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
    }
}