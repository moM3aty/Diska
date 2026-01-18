using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Diska.Models;
using System.Threading.Tasks;
using System.Linq;
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

        // --- 1. Login ---
        [HttpGet]
        public IActionResult Login(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToRoleDashboard();
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string phone, string password, bool rememberMe, string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (string.IsNullOrEmpty(phone) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "من فضلك أدخل البيانات كاملة";
                return View();
            }

            // البحث عن المستخدم (الاسم المستخدم هو رقم الهاتف)
            var user = await _userManager.FindByNameAsync(phone) ?? _userManager.Users.FirstOrDefault(u => u.PhoneNumber == phone);

            if (user != null)
            {
                // التحقق من الحظر
                if (await _userManager.IsLockedOutAsync(user))
                {
                    ViewBag.Error = "هذا الحساب محظور مؤقتاً. يرجى التواصل مع الدعم.";
                    return View();
                }

                // محاولة الدخول
                var result = await _signInManager.PasswordSignInAsync(user, password, rememberMe, lockoutOnFailure: true);

                if (result.Succeeded)
                {
                    // التحقق الخاص بالتجار (Approval Check)
                    if (await _userManager.IsInRoleAsync(user, "Merchant"))
                    {
                        if (!user.IsVerifiedMerchant)
                        {
                            await _signInManager.SignOutAsync();
                            ViewBag.Error = "حساب التاجر الخاص بك قيد المراجعة ولم يتم تفعيله بعد.";
                            return View();
                        }
                    }

                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                        return Redirect(returnUrl);

                    return RedirectToRoleDashboard();
                }
            }

            ViewBag.Error = "بيانات الدخول غير صحيحة.";
            return View();
        }

        // --- 2. Register (Signup) ---
        [HttpGet]
        public IActionResult Signup() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        // تم إضافة taxCard هنا لاستقبال القيمة من الفورم وحل مشكلة قاعدة البيانات
        public async Task<IActionResult> Signup(string fullName, string shopName, string phone, string password, string type, string commercialReg, string taxCard)
        {
            // تنظيف المدخلات والتحقق من الهاتف
            phone = phone?.Trim();

            if (string.IsNullOrEmpty(phone))
            {
                ViewBag.Error = "رقم الهاتف مطلوب.";
                return View(); // أو return RedirectToAction إذا كان الطلب من مكان آخر
            }

            var existingUser = await _userManager.FindByNameAsync(phone);
            if (existingUser != null)
            {
                ViewBag.Error = "رقم الهاتف مسجل مسبقاً، حاول تسجيل الدخول.";
                return View();
            }

            // تحديد القيم الافتراضية بناءً على النوع
            string role = type == "Merchant" ? "Merchant" : "Customer";

            // إصلاح مشكلة SQL Error: تعيين قيم افتراضية للحقول الإجبارية في قاعدة البيانات
            // إذا لم يكن تاجر أو لم يرسل قيمة، نضع "000000" بدلاً من NULL
            string finalShopName = role == "Merchant" ? shopName : "عميل";
            string finalCommReg = !string.IsNullOrEmpty(commercialReg) ? commercialReg : "000000";
            string finalTaxCard = !string.IsNullOrEmpty(taxCard) ? taxCard : "000000";

            var user = new ApplicationUser
            {
                UserName = phone, // هذا يحل مشكلة Parameter 'userName' cannot be null
                PhoneNumber = phone,
                FullName = fullName,
                ShopName = finalShopName,
                CommercialRegister = finalCommReg,
                TaxCard = finalTaxCard, // تم إضافة هذا الحقل لحل مشكلة الإدخال في قاعدة البيانات
                IsVerifiedMerchant = false,
                Email = $"{phone}@diska.local",
                WalletBalance = 0
            };

            var result = await _userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, role);

                if (role == "Merchant")
                {
                    // التاجر لا يدخل مباشرة
                    // يمكن استخدام TempData لعرض رسالة نجاح في الصفحة التالية
                    TempData["Success"] = "تم تسجيل حساب التاجر بنجاح وهو قيد المراجعة.";
                    return RedirectToAction("Index", "Home"); // توجيه للصفحة الرئيسية بدلاً من صفحة غير موجودة
                }

                // العميل يدخل مباشرة
                await _signInManager.SignInAsync(user, isPersistent: true);
                return RedirectToAction("Index", "Home");
            }

            ViewBag.Error = string.Join(", ", result.Errors.Select(e => e.Description));
            return View();
        }
        // --- 3. Logout ---
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        // --- 4. Forgot Password ---
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(string phone)
        {
            if (string.IsNullOrEmpty(phone)) return View();

            var user = await _userManager.FindByNameAsync(phone);
            if (user == null)
            {
                // لا تكشف أن المستخدم غير موجود للأمان
                return View("ForgotPasswordConfirmation");
            }

            // هنا يجب إرسال رمز التحقق SMS أو رابط للإيميل
            // للتبسيط حالياً سنقوم بتوليد التوكن وتوجيهه (محاكاة)
            var code = await _userManager.GeneratePasswordResetTokenAsync(user);

            // في الواقع: SendSMS(user.PhoneNumber, code);
            // للمحاكاة: سنمرر الكود للصفحة التالية مباشرة
            return RedirectToAction("ResetPassword", new { code = code, phone = phone });
        }

        // --- 5. Reset Password ---
        [HttpGet]
        public IActionResult ResetPassword(string code = null, string phone = null)
        {
            if (code == null) return BadRequest("A code must be supplied for password reset.");
            var model = new ResetPasswordViewModel { Code = code, Phone = phone };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByNameAsync(model.Phone);
            if (user == null)
            {
                // لا تكشف أن المستخدم غير موجود
                return RedirectToAction("ResetPasswordConfirmation");
            }

            var result = await _userManager.ResetPasswordAsync(user, model.Code, model.Password);
            if (result.Succeeded)
            {
                return RedirectToAction("ResetPasswordConfirmation");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult ResetPasswordConfirmation()
        {
            return View();
        }

        [HttpGet]
        public IActionResult ForgotPasswordConfirmation()
        {
            return View();
        }
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        // --- Helpers ---
        private IActionResult RedirectToRoleDashboard()
        {
            if (User.IsInRole("Admin"))
                return RedirectToAction("Index", "Dashboard", new { area = "Admin" });

            if (User.IsInRole("Merchant"))
                return RedirectToAction("Index", "Dashboard", new { area = "Merchant" });

            return RedirectToAction("Index", "Home", new { area = "" });
        }
    }

    // View Models for Password Reset
    public class ResetPasswordViewModel
    {
        public string Phone { get; set; }
        public string Code { get; set; }

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.DataType(System.ComponentModel.DataAnnotations.DataType.Password)]
        public string Password { get; set; }

        [System.ComponentModel.DataAnnotations.DataType(System.ComponentModel.DataAnnotations.DataType.Password)]
        [System.ComponentModel.DataAnnotations.Compare("Password", ErrorMessage = "كلمة المرور غير متطابقة")]
        public string ConfirmPassword { get; set; }
    }

}