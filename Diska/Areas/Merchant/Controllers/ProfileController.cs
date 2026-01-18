using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Microsoft.AspNetCore.Identity;
using Diska.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace Diska.Areas.Merchant.Controllers
{
    [Area("Merchant")]
    [Authorize(Roles = "Merchant")]
    public class ProfileController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public ProfileController(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(string FullName, string ShopName, string CommercialRegister, string CurrentPassword, string NewPassword)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            // تحديث البيانات الأساسية
            user.FullName = FullName;
            user.ShopName = ShopName;
            user.CommercialRegister = CommercialRegister;

            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                TempData["Error"] = "حدث خطأ أثناء تحديث البيانات.";
                return RedirectToAction(nameof(Index));
            }

            // تحديث كلمة المرور (إذا تم إدخالها)
            if (!string.IsNullOrEmpty(CurrentPassword) && !string.IsNullOrEmpty(NewPassword))
            {
                var changePassResult = await _userManager.ChangePasswordAsync(user, CurrentPassword, NewPassword);
                if (!changePassResult.Succeeded)
                {
                    TempData["Error"] = "كلمة المرور الحالية غير صحيحة أو الجديدة لا تطابق الشروط.";
                    return RedirectToAction(nameof(Index));
                }
                TempData["Success"] = "تم تحديث البيانات وكلمة المرور بنجاح.";
            }
            else
            {
                TempData["Success"] = "تم تحديث البيانات بنجاح.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}