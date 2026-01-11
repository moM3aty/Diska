using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Diska.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;

namespace Diska.Controllers
{
    [Authorize]
    public class WishlistController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public WishlistController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // 1. عرض قائمة المفضلة
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var items = await _context.WishlistItems
                .Include(w => w.Product)
                .ThenInclude(p => p.Category) // نحتاج القسم للعرض
                .Include(w => w.Product.Merchant) // نحتاج التاجر للعرض
                .Where(w => w.UserId == user.Id)
                .ToListAsync();

            return View(items);
        }

        // 2. إضافة/إزالة (AJAX Toggle) - تستخدم لزر القلب في المنتجات
        [HttpPost]
        public async Task<IActionResult> Toggle(int productId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var item = await _context.WishlistItems
                .FirstOrDefaultAsync(w => w.UserId == user.Id && w.ProductId == productId);

            if (item != null)
            {
                // موجود بالفعل -> حذف
                _context.WishlistItems.Remove(item);
                await _context.SaveChangesAsync();
                return Json(new { success = true, status = "removed", message = "تم الحذف من المفضلة" });
            }
            else
            {
                // غير موجود -> إضافة
                _context.WishlistItems.Add(new WishlistItem { UserId = user.Id, ProductId = productId });
                await _context.SaveChangesAsync();
                return Json(new { success = true, status = "added", message = "تمت الإضافة للمفضلة" });
            }
        }

        // 3. حذف عنصر (POST Form) - تستخدم في صفحة المفضلة
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var item = await _context.WishlistItems.FirstOrDefaultAsync(w => w.Id == id && w.UserId == user.Id);

            if (item != null)
            {
                _context.WishlistItems.Remove(item);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حذف المنتج من المفضلة.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}