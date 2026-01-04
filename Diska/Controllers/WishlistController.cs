using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Diska.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

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

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var items = await _context.WishlistItems
                .Where(w => w.UserId == user.Id)
                .Include(w => w.Product)
                .ToListAsync();

            return View(items);
        }

        [HttpPost]
        public async Task<IActionResult> Toggle(int productId)
        {
            var user = await _userManager.GetUserAsync(User);
            var exists = await _context.WishlistItems
                .FirstOrDefaultAsync(w => w.UserId == user.Id && w.ProductId == productId);

            if (exists != null)
            {
                _context.WishlistItems.Remove(exists);
                await _context.SaveChangesAsync();
                return Json(new { success = true, status = "removed", message = "تم الحذف من المفضلة" });
            }
            else
            {
                var item = new WishlistItem { UserId = user.Id, ProductId = productId };
                _context.WishlistItems.Add(item);
                await _context.SaveChangesAsync();
                return Json(new { success = true, status = "added", message = "تمت الإضافة للمفضلة" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Remove(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var item = await _context.WishlistItems.FirstOrDefaultAsync(w => w.Id == id && w.UserId == user.Id);
            if (item != null)
            {
                _context.WishlistItems.Remove(item);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}