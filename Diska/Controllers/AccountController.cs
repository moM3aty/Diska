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
        public IActionResult Login()
        {
            if (User.Identity.IsAuthenticated) return RedirectToAction("Index", "Home");
            return View();
        }

        // تسجيل الدخول برقم الهاتف
        [HttpPost]
        public async Task<IActionResult> Login(string phone, string password)
        {
            if (string.IsNullOrEmpty(phone) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "يرجى إدخال رقم الهاتف وكلمة المرور";
                return View();
            }

            // 1. التأكد أن المستخدم موجود أصلاً برقم الهاتف
            // نستخدم FindByNameAsync لأننا جعلنا الـ UserName هو نفسه رقم الهاتف عند التسجيل
            var user = await _userManager.FindByNameAsync(phone);

            if (user == null)
            {
                ViewBag.Error = "هذا الرقم غير مسجل لدينا، يرجى إنشاء حساب جديد.";
                return View();
            }

            // 2. محاولة تسجيل الدخول
            var result = await _signInManager.PasswordSignInAsync(user, password, isPersistent: true, lockoutOnFailure: false);

            if (result.Succeeded)
            {
                if (await _userManager.IsInRoleAsync(user, "Admin"))
                    return RedirectToAction("Index", "Dashboard", new { area = "Admin" });

                return RedirectToAction("Index", "Home");
            }

            ViewBag.Error = "كلمة المرور غير صحيحة";
            return View();
        }

        [HttpGet]
        public IActionResult Signup() => View();

        // التسجيل برقم الهاتف كاسم مستخدم
        [HttpPost]
        public async Task<IActionResult> Signup(string fullName, string shopName, string phone, string password)
        {
            // التحقق من وجود الرقم مسبقاً
            var existingUser = await _userManager.FindByNameAsync(phone);
            if (existingUser != null)
            {
                ViewBag.Error = "هذا الرقم مسجل بالفعل، يرجى تسجيل الدخول.";
                return View();
            }

            var user = new ApplicationUser
            {
                // أهم خطوة: جعل اسم المستخدم هو رقم الهاتف
                UserName = phone,
                PhoneNumber = phone,
                FullName = fullName,
                ShopName = shopName,
                WalletBalance = 0,
                Email = $"{phone}@diska.local" // بريد وهمي لأن Identity يطلبه
            };

            var result = await _userManager.CreateAsync(user, password);

            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "Merchant");
                await _signInManager.SignInAsync(user, isPersistent: true);
                return RedirectToAction("Index", "Home");
            }

            // عرض سبب فشل التسجيل بوضوح
            string errorMsg = "";
            foreach (var error in result.Errors)
            {
                // ترجمة بعض الأخطاء الشائعة
                if (error.Code.Contains("Password")) errorMsg += "كلمة المرور يجب أن تكون 6 خانات على الأقل. ";
                else errorMsg += error.Description + " ";
            }

            ViewBag.Error = errorMsg;
            return View();
        }

        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        // ... (باقي الدوال Profile و Settings كما هي) ...
        [Authorize]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login");
            return View(user);
        }

        [Authorize]
        public async Task<IActionResult> Settings()
        {
            var user = await _userManager.GetUserAsync(User);
            return View(user);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> UpdateProfile(ApplicationUser model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                user.FullName = model.FullName;
                user.ShopName = model.ShopName;
                user.CommercialRegister = model.CommercialRegister;
                await _userManager.UpdateAsync(user);
                return RedirectToAction("Profile");
            }
            return View("Settings", model);
        }
    }
}