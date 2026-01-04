using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Diska.Data;
using Diska.Models; // تأكد من وجود هذا الـ Namespace إذا كنت تستخدم ViewModels مخصصة

namespace Diska.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ApplicationDbContext _context;

        public AccountController(
            SignInManager<IdentityUser> signInManager,
            UserManager<IdentityUser> userManager,
            ApplicationDbContext context)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _context = context;
        }

        // --- تسجيل الدخول ---
        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string phone, string password)
        {
            if (string.IsNullOrEmpty(phone) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "يرجى إدخال رقم الهاتف وكلمة المرور";
                return View();
            }

            // البحث عن المستخدم باستخدام رقم الهاتف (باعتباره اسم المستخدم في هذا السيناريو)
            // ملاحظة: في Identity الافتراضي، UserName هو الأساس. 
            // سنفترض هنا أن المستخدم سجل برقم هاتفه كـ UserName أو سنبحث عنه.

            var user = await _userManager.FindByNameAsync(phone);
            if (user == null)
            {
                // محاولة البحث بالبريد إذا لم ينجح الهاتف
                user = await _userManager.FindByEmailAsync(phone);
            }

            if (user != null)
            {
                var result = await _signInManager.PasswordSignInAsync(user, password, isPersistent: true, lockoutOnFailure: false);
                if (result.Succeeded)
                {
                    // التحقق من الدور (Role) للتوجيه المناسب
                    if (await _userManager.IsInRoleAsync(user, "Admin"))
                    {
                        return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
                    }
                    return RedirectToAction("Index", "Home");
                }
            }

            ViewBag.Error = "بيانات الدخول غير صحيحة";
            return View();
        }

        // --- إنشاء حساب جديد ---
        [HttpGet]
        public IActionResult Signup()
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Signup(string fullName, string shopName, string phone, string password, string confirmPassword)
        {
            if (password != confirmPassword)
            {
                ViewBag.Error = "كلمة المرور غير متطابقة";
                return View();
            }

            var user = new IdentityUser
            {
                UserName = phone, // استخدام رقم الهاتف كاسم مستخدم
                PhoneNumber = phone,
                Email = $"{phone}@diska.local" // بريد افتراضي إذا لم يوفره المستخدم
            };

            var result = await _userManager.CreateAsync(user, password);

            if (result.Succeeded)
            {
                // تعيين الدور الافتراضي كـ "Customer"
                await _userManager.AddToRoleAsync(user, "Customer");

                // يمكن حفظ البيانات الإضافية مثل (اسم المحل) في جدول منفصل أو Claims
                // للتسهيل هنا سنقوم بتسجيل الدخول مباشرة
                await _signInManager.SignInAsync(user, isPersistent: false);

                return RedirectToAction("Index", "Home");
            }

            foreach (var error in result.Errors)
            {
                // عرض الأخطاء القادمة من Identity (مثل: كلمة المرور ضعيفة)
                ModelState.AddModelError(string.Empty, error.Description);
            }

            // تجميع الأخطاء لعرضها في الـ View
            ViewBag.Error = string.Join(" - ", result.Errors.Select(e => e.Description));
            return View();
        }

        // --- الملف الشخصي ---
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login");

            // جلب بيانات حقيقية
            ViewBag.UserName = user.UserName;
            ViewBag.Phone = user.PhoneNumber;

            var roles = await _userManager.GetRolesAsync(user);
            ViewBag.UserType = roles.FirstOrDefault() ?? "مستخدم";

            return View();
        }

        // --- تسجيل الخروج ---
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }
    }
}