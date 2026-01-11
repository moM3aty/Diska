using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Diska.Models;
using Diska.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;
using System.Linq;
using System;

namespace Diska.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _context;

        public ProfileController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
        }

        // 1. عرض الصفحة الرئيسية للملف الشخصي (Dashboard)
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            // --- الإحصائيات الأساسية ---
            ViewBag.OrdersCount = await _context.Orders.CountAsync(o => o.UserId == user.Id);
            ViewBag.PendingCount = await _context.Orders.CountAsync(o => o.UserId == user.Id && o.Status == "Pending");
            ViewBag.WishlistCount = await _context.WishlistItems.CountAsync(w => w.UserId == user.Id);
            ViewBag.AddressesCount = await _context.UserAddresses.CountAsync(a => a.UserId == user.Id);

            // --- إضافات لوحة العميل (Dashboard Stats) ---

            // 1. إجمالي المصروفات
            ViewBag.TotalSpent = await _context.Orders
                .Where(o => o.UserId == user.Id && o.Status != "Cancelled")
                .SumAsync(o => (decimal?)o.TotalAmount) ?? 0;

            // 2. العروض النشطة (للعرض في السلايدر أو القائمة الجانبية)
            ViewBag.ActiveDeals = await _context.GroupDeals
                .Include(d => d.Product)
                .Where(d => d.IsActive && d.EndDate > DateTime.Now)
                .OrderBy(d => d.EndDate)
                .Take(3)
                .ToListAsync();

            // --- البيانات الجدولية ---

            // جلب آخر 5 طلبات
            ViewBag.RecentOrders = await _context.Orders
                .Where(o => o.UserId == user.Id)
                .OrderByDescending(o => o.OrderDate)
                .Take(5)
                .ToListAsync();

            // التحقق إذا كان تاجر لعرض حقل اسم المحل
            ViewBag.IsMerchant = await _userManager.IsInRoleAsync(user, "Merchant");

            return View(user);
        }

        // 2. تحديث البيانات الشخصية
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(string fullName, string shopName)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            user.FullName = fullName;

            // تحديث اسم المحل للتجار فقط
            if (!string.IsNullOrEmpty(shopName) && await _userManager.IsInRoleAsync(user, "Merchant"))
            {
                user.ShopName = shopName;
            }

            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                TempData["Success"] = "تم تحديث البيانات الشخصية بنجاح.";
            }
            else
            {
                TempData["Error"] = "حدث خطأ أثناء التحديث.";
            }

            return RedirectToAction(nameof(Index));
        }

        // 3. تغيير كلمة المرور
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            if (newPassword != confirmPassword)
            {
                TempData["Error"] = "كلمة المرور الجديدة وتأكيدها غير متطابقين.";
                return RedirectToAction(nameof(Index));
            }

            var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);

            if (result.Succeeded)
            {
                await _signInManager.RefreshSignInAsync(user); // إعادة تسجيل الدخول للحفاظ على الجلسة
                TempData["Success"] = "تم تغيير كلمة المرور بنجاح.";
            }
            else
            {
                // عرض أول خطأ فقط للتبسيط
                var error = result.Errors.FirstOrDefault()?.Description ?? "فشل تغيير كلمة المرور";
                TempData["Error"] = error;
            }

            return RedirectToAction(nameof(Index));
        }
    }
}