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
        private readonly UserManager<IdentityUser> _userManager;

        public WishlistController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // عرض المفضلة (من الداتابيز للمستخدم المسجل، أو View عادي للزائر)
        public async Task<IActionResult> Index()
        {
            if (User.Identity.IsAuthenticated)
            {
                var user = await _userManager.GetUserAsync(User);
                var items = _context.WishlistItems
                    .Where(w => w.UserId == user.Id)
                    .Include(w => w.Product)
                    .ToList();
                return View("IndexDB", items); // فيو جديد للمستخدمين المسجلين
            }
            return View(); // الفيو القديم المعتمد على LocalStorage للزوار
        }

        // إضافة/حذف منتج (AJAX)
        [HttpPost]
        public async Task<IActionResult> Toggle(int productId)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Json(new { success = false, message = "يجب تسجيل الدخول أولاً" });
            }

            var user = await _userManager.GetUserAsync(User);
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