using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Diska.Models;
using Microsoft.AspNetCore.Authorization;

namespace Diska.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;

        public AccountController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
        }

        [HttpGet]
        public IActionResult Login(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return User.Identity.IsAuthenticated ? RedirectToAction("Index", "Home") : View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string phone, string password, bool rememberMe, string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (string.IsNullOrEmpty(phone) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "من فضلك أدخل رقم الهاتف وكلمة المرور";
                return View();
            }

            var user = await _userManager.FindByNameAsync(phone);
            if (user != null)
            {
                var result = await _signInManager.PasswordSignInAsync(user, password, rememberMe, false);
                if (result.Succeeded)
                {
                    if (await _userManager.IsInRoleAsync(user, "Admin"))
                    {
                        return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
                    }

                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        return Redirect(returnUrl);
                    }
                    return RedirectToAction("Index", "Home");
                }
            }

            ViewBag.Error = "بيانات الدخول غير صحيحة";
            return View();
        }

        [HttpGet]
        public IActionResult Signup() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Signup(string fullName, string shopName, string phone, string password, string type, string commercialReg)
        {
            var existingUser = await _userManager.FindByNameAsync(phone);
            if (existingUser != null)
            {
                ViewBag.Error = "رقم الهاتف مسجل مسبقاً";
                return View();
            }

            var user = new ApplicationUser
            {
                UserName = phone,
                PhoneNumber = phone,
                FullName = fullName,
                ShopName = type == "Merchant" ? shopName : null,
                CommercialRegister = type == "Merchant" ? commercialReg : null,
                Email = $"{phone}@diska.local",
                IsVerifiedMerchant = false
            };

            var result = await _userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                string role = type == "Merchant" ? "Merchant" : "Customer";
                await _userManager.AddToRoleAsync(user, role);
                await _signInManager.SignInAsync(user, isPersistent: true);

                if (role == "Merchant") return RedirectToAction("Index", "Merchant");
                return RedirectToAction("Index", "Home");
            }

            ViewBag.Error = string.Join(", ", result.Errors.Select(e => e.Description));
            return View();
        }

        [Authorize]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            // إحصائيات وهمية للتجربة
            ViewBag.OrdersCount = 12;
            ViewBag.PendingCount = 2;
            ViewBag.WishlistCount = 5;
            return View(user);
        }

        [Authorize]
        public async Task<IActionResult> Settings()
        {
            var user = await _userManager.GetUserAsync(User);
            return View(user);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> UpdateProfile(string FullName, string ShopName, string CommercialRegister)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            user.FullName = FullName;
            if (User.IsInRole("Merchant"))
            {
                user.ShopName = ShopName;
                user.CommercialRegister = CommercialRegister;
            }

            await _userManager.UpdateAsync(user);
            TempData["Success"] = "تم تحديث البيانات بنجاح";
            return RedirectToAction(nameof(Settings));
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> ChangePassword(string CurrentPassword, string NewPassword)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var result = await _userManager.ChangePasswordAsync(user, CurrentPassword, NewPassword);
            if (result.Succeeded)
            {
                TempData["Success"] = "تم تغيير كلمة المرور بنجاح";
            }
            else
            {
                TempData["Error"] = "كلمة المرور الحالية غير صحيحة أو الجديدة لا تطابق الشروط";
            }
            return RedirectToAction(nameof(Settings));
        }

        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }
    }
}