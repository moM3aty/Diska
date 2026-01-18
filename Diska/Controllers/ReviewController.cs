using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Diska.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;
using System.Linq;
using System;

namespace Diska.Controllers
{
    [Authorize]
    public class ReviewController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReviewController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // 1. عرض قائمة تقييمات العميل (MyReviews)
        public async Task<IActionResult> MyReviews()
        {
            var user = await _userManager.GetUserAsync(User);
            var reviews = await _context.ProductReviews
                .Include(r => r.Product)
                .Where(r => r.UserId == user.Id)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return View(reviews);
        }

        // 2. إضافة تقييم جديد (Create)
        [HttpGet]
        public async Task<IActionResult> Create(int productId)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);

            // التحقق مما إذا كان المستخدم قد قيم المنتج سابقاً
            var existingReview = await _context.ProductReviews
                .FirstOrDefaultAsync(r => r.ProductId == productId && r.UserId == user.Id);

            if (existingReview != null)
            {
                TempData["Error"] = "لقد قمت بتقييم هذا المنتج مسبقاً، يمكنك تعديل تقييمك.";
                return RedirectToAction("Edit", new { id = existingReview.Id });
            }

            ViewBag.ProductName = product.Name; // أو NameEn حسب اللغة في الفيو
            return View(new ProductReview { ProductId = productId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductReview model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                model.UserId = user.Id;
                model.CreatedAt = DateTime.Now;
                model.IsVisible = true; // يمكن جعلها false إذا كانت تتطلب موافقة

                _context.ProductReviews.Add(model);
                await _context.SaveChangesAsync();

                TempData["Success"] = "تم إضافة تقييمك بنجاح.";
                return RedirectToAction("Details", "Product", new { id = model.ProductId });
            }
            return View(model);
        }

        // 3. تعديل تقييم (Edit)
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var review = await _context.ProductReviews
                .Include(r => r.Product)
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == user.Id);

            if (review == null) return NotFound();

            return View(review);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ProductReview model)
        {
            if (id != model.Id) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            var reviewToUpdate = await _context.ProductReviews
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == user.Id);

            if (reviewToUpdate == null) return NotFound();
            ModelState.Remove("User");
            ModelState.Remove("UserId");
            ModelState.Remove("Product");
            if (ModelState.IsValid)
            {
                reviewToUpdate.Rating = model.Rating;
                reviewToUpdate.Comment = model.Comment;
                reviewToUpdate.CreatedAt = DateTime.Now; // تحديث التاريخ

                await _context.SaveChangesAsync();
                TempData["Success"] = "تم تعديل التقييم بنجاح.";
                return RedirectToAction(nameof(MyReviews));
            }
            return View(model);
        }

        // حذف تقييم (اختياري)
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var review = await _context.ProductReviews.FirstOrDefaultAsync(r => r.Id == id && r.UserId == user.Id);

            if (review != null)
            {
                _context.ProductReviews.Remove(review);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حذف التقييم.";
            }
            return RedirectToAction(nameof(MyReviews));
        }
    }
}