using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Diska.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Diska.Controllers
{
    public class WishlistController : Controller
    {
        private readonly ApplicationDbContext _context;
        // تصحيح: استخدام ApplicationUser
        private readonly UserManager<ApplicationUser> _userManager;

        public WishlistController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
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
                    var items = _context.WishlistItems
                        .Where(w => w.UserId == user.Id)
                        .Include(w => w.Product)
                        .ToList();
                    // عرض صفحة المفضلة الخاصة بالمستخدم المسجل (يمكنك إنشاء View باسم IndexDB أو استخدام Index وتمرير الموديل)
                    // هنا سنعيد نفس الـ View وسنقوم بتعديلها لاحقاً لتقبل الموديل، أو نتركها تعتمد على الـ JS حالياً
                    // للأمان، سنمرر البيانات للـ View
                    return View(items);
                }
            }
            return View(new List<WishlistItem>());
        }

        [HttpPost]
        public async Task<IActionResult> Toggle(int productId)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Json(new { success = false, message = "يجب تسجيل الدخول أولاً" });
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new { success = false, message = "خطأ في المستخدم" });

            var existingItem = _context.WishlistItems.FirstOrDefault(w => w.UserId == user.Id && w.ProductId == productId);

            if (existingItem != null)
            {
                _context.WishlistItems.Remove(existingItem);
                await _context.SaveChangesAsync();
                return Json(new { success = true, action = "removed" });
            }
            else
            {
                var item = new WishlistItem { UserId = user.Id, ProductId = productId };
                _context.WishlistItems.Add(item);
                await _context.SaveChangesAsync();
                return Json(new { success = true, action = "added" });
            }
        }
    }
}