using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Diska.Models;

namespace Diska.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ReviewsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReviewsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // عرض التقييمات مع البحث والفلترة
        public async Task<IActionResult> Index(string search = "", int? rating = null, string status = "All")
        {
            var query = _context.ProductReviews
                .Include(r => r.Product)
                .Include(r => r.User)
                .AsQueryable();

            // بحث
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(r => r.Product.Name.Contains(search) || r.User.FullName.Contains(search) || r.Comment.Contains(search));
            }

            // فلترة بالتقييم
            if (rating.HasValue)
            {
                query = query.Where(r => r.Rating == rating.Value);
            }

            // فلترة بالحالة
            if (status == "Visible")
            {
                query = query.Where(r => r.IsVisible);
            }
            else if (status == "Hidden")
            {
                query = query.Where(r => !r.IsVisible);
            }

            var reviews = await query.OrderByDescending(r => r.CreatedAt).ToListAsync();

            ViewBag.CurrentSearch = search;
            ViewBag.CurrentRating = rating;
            ViewBag.CurrentStatus = status;

            return View(reviews);
        }

        // تبديل حالة الظهور (موافقة / رفض)
        [HttpPost]
        public async Task<IActionResult> ToggleVisibility(int id)
        {
            var review = await _context.ProductReviews.FindAsync(id);
            if (review != null)
            {
                review.IsVisible = !review.IsVisible;
                await _context.SaveChangesAsync();
                TempData["Success"] = review.IsVisible ? "تم إظهار التقييم." : "تم إخفاء التقييم.";
            }
            return RedirectToAction(nameof(Index));
        }

        // حذف نهائي
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var review = await _context.ProductReviews.FindAsync(id);
            if (review != null)
            {
                _context.ProductReviews.Remove(review);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حذف التقييم نهائياً.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}