using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Diska.Models;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using System;
using Diska.Services; // ✅ استدعاء مجلد الخدمات
using Microsoft.AspNetCore.Http; // ✅ للتعامل مع الـ Session

namespace Diska.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ISmsService _smsService; // ✅ 1. تعريف خدمة الرسائل

        // ✅ 2. حقن الخدمة في المشيد (Constructor)
        public AccountController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager, ISmsService smsService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _smsService = smsService;
        }

        // =========================================================
        // 1. تسجيل الدخول
        // =========================================================
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

            var user = await _userManager.FindByNameAsync(phone) ?? _userManager.Users.FirstOrDefault(u => u.PhoneNumber == phone);

            if (user != null)
            {
                if (await _userManager.IsLockedOutAsync(user))
                {
                    ViewBag.Error = "هذا الحساب محظور مؤقتاً. يرجى التواصل مع الدعم.";
                    return View();
                }

                var result = await _signInManager.PasswordSignInAsync(user, password, rememberMe, lockoutOnFailure: true);

                if (result.Succeeded)
                {
                    if (await _userManager.IsInRoleAsync(user, "Merchant") && !user.IsVerifiedMerchant)
                    {
                        await _signInManager.SignOutAsync();
                        ViewBag.Error = "حساب التاجر الخاص بك قيد المراجعة ولم يتم تفعيله بعد.";
                        return View();
                    }

                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                        return Redirect(returnUrl);

                    return RedirectToRoleDashboard();
                }
            }

            ViewBag.Error = "بيانات الدخول غير صحيحة.";
            return View();
        }

        // =========================================================
        // 2. إنشاء حساب جديد
        // =========================================================
        [HttpGet]
        public IActionResult Signup() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Signup(string fullName, string shopName, string phone, string password, string type, string commercialReg, string taxCard)
        {
            phone = phone?.Trim();

            if (string.IsNullOrEmpty(phone))
            {
                ViewBag.Error = "رقم الهاتف مطلوب.";
                return View();
            }

            var existingUser = await _userManager.FindByNameAsync(phone);
            if (existingUser != null)
            {
                ViewBag.Error = "رقم الهاتف مسجل مسبقاً، حاول تسجيل الدخول.";
                return View();
            }

            string role = type == "Merchant" ? "Merchant" : "Customer";

            string finalShopName = role == "Merchant" ? shopName : "عميل";
            string finalCommReg = !string.IsNullOrEmpty(commercialReg) ? commercialReg : "000000";
            string finalTaxCard = !string.IsNullOrEmpty(taxCard) ? taxCard : "000000";

            var user = new ApplicationUser
            {
                UserName = phone,
                PhoneNumber = phone,
                FullName = fullName,
                ShopName = finalShopName,
                CommercialRegister = finalCommReg,
                TaxCard = finalTaxCard,
                IsVerifiedMerchant = false,
                Email = $"{phone}@diska.local",
                WalletBalance = 0,
                UserType = role,
                CreatedAt = DateTime.Now
            };

            var result = await _userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, role);

                if (role == "Merchant")
                {
                    TempData["Success"] = "تم تسجيل حساب التاجر بنجاح وهو قيد المراجعة.";
                    return RedirectToAction("Index", "Home");
                }

                await _signInManager.SignInAsync(user, isPersistent: true);
                return RedirectToAction("Index", "Home");
            }

            ViewBag.Error = string.Join(", ", result.Errors.Select(e => e.Description));
            return View();
        }

        // =========================================================
        // 3. API إرسال OTP عبر AJAX (للاستخدام في شاشات الموقع)
        // =========================================================
        [HttpPost]
        public async Task<IActionResult> SendOtpAjax(string phone)
        {
            if (string.IsNullOrEmpty(phone)) return Json(new { success = false, message = "رقم الهاتف مطلوب" });

            // توليد كود عشوائي من 6 أرقام
            Random rand = new Random();
            string otpCode = rand.Next(100000, 999999).ToString();

            // حفظ الكود في Session لمطابقته لاحقاً إذا أردت
            HttpContext.Session.SetString("VerifiedOTP", otpCode);

            // إرسال الـ SMS عبر خدمة WhySMS
            bool isSent = await _smsService.SendOtpAsync(phone, otpCode);

            if (isSent)
            {
                return Json(new { success = true, message = "تم إرسال رمز التحقق إلى هاتفك.", test_otp = otpCode }); // test_otp للحظات التطوير فقط
            }

            return Json(new { success = false, message = "حدث خطأ أثناء إرسال الرسالة من المزود." });
        }


        // =========================================================
        // 4. نسيت كلمة المرور (مع إرسال SMS حقيقي)
        // =========================================================
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

            // توليد التوكن الخاص بـ Identity
            var code = await _userManager.GeneratePasswordResetTokenAsync(user);

            // ✅ 5. إرسال الـ SMS الفعلي للعميل يحتوي على رابط استعادة كلمة المرور
            string resetLink = Url.Action("ResetPassword", "Account", new { code = code, phone = phone }, Request.Scheme);
            string smsMessage = $"ديسكا: لاستعادة كلمة المرور، اضغط على الرابط: {resetLink}";

            await _smsService.SendSmsAsync(phone, smsMessage);

            // توجيه المستخدم لصفحة تأكيد الإرسال
            return View("ForgotPasswordConfirmation");
        }

        // =========================================================
        // 5. استعادة كلمة المرور
        // =========================================================
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
        public IActionResult ResetPasswordConfirmation() => View();

        [HttpGet]
        public IActionResult ForgotPasswordConfirmation() => View();

        [HttpGet]
        public IActionResult AccessDenied() => View();

        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
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