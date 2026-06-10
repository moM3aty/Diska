using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Diska.Models;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using System;
using Diska.Services;
using Microsoft.AspNetCore.Http;

namespace Diska.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ISmsService _smsService;

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
            if (User.Identity.IsAuthenticated) return RedirectToRoleDashboard();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string phone, string password, bool rememberMe, string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            // 🚨 المتغير قادم باسم phone من الـ View، ولكنه يحمل الإيميل أو رقم الهاتف
            string identifier = phone?.Trim() ?? "";

            if (string.IsNullOrEmpty(identifier) || string.IsNullOrEmpty(password))
            {
                TempData["Error"] = "من فضلك أدخل البيانات كاملة";
                return View();
            }

            // 🚨 فحص قاعدة البيانات: نبحث بالبريد أولاً، وإذا لم نجد نبحث برقم الهاتف أو الـ UserName
            var user = await _userManager.FindByEmailAsync(identifier)
                    ?? _userManager.Users.FirstOrDefault(u => u.PhoneNumber == identifier || u.UserName == identifier);

            if (user != null)
            {
                if (await _userManager.IsLockedOutAsync(user))
                {
                    TempData["Error"] = "هذا الحساب محظور مؤقتاً. يرجى التواصل مع الدعم.";
                    return View();
                }

                var result = await _signInManager.PasswordSignInAsync(user, password, rememberMe, lockoutOnFailure: true);

                if (result.Succeeded)
                {
                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
                    return RedirectToRoleDashboard();
                }
            }

            TempData["Error"] = "بيانات الدخول غير صحيحة.";
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
                TempData["Error"] = "رقم الهاتف مطلوب.";
                return View();
            }

            var existingUser = await _userManager.FindByNameAsync(phone);
            if (existingUser != null)
            {
                TempData["Error"] = "رقم الهاتف مسجل مسبقاً، حاول تسجيل الدخول.";
                return View();
            }

            string role = type == "Merchant" ? "Merchant" : "Customer";
            var user = new ApplicationUser
            {
                UserName = phone,
                PhoneNumber = phone,
                FullName = fullName,
                ShopName = role == "Merchant" ? shopName : "عميل",
                CommercialRegister = !string.IsNullOrEmpty(commercialReg) ? commercialReg : "000000",
                TaxCard = !string.IsNullOrEmpty(taxCard) ? taxCard : "000000",
                IsVerifiedMerchant = role == "Merchant" ? true : false,
                Email = $"{phone}@diska.local",
                WalletBalance = 0,
                UserType = role,
                CreatedAt = DateTime.Now
            };

            var result = await _userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, role);
                await _signInManager.SignInAsync(user, isPersistent: true);

                if (role == "Merchant")
                {
                    TempData["Success"] = "تم إنشاء حساب التاجر وتفعيله بنجاح.";
                }

                return RedirectToAction("Index", "Home");
            }

            TempData["Error"] = string.Join(", ", result.Errors.Select(e => e.Description));
            return View();
        }

        // =========================================================
        // 3. API إرسال OTP عبر AJAX (لشاشة التسجيل) - الوضع الحي (LIVE)
        // =========================================================
        [HttpPost]
        public async Task<IActionResult> SendOtpAjax(string phone)
        {
            if (string.IsNullOrEmpty(phone)) return Json(new { success = false, message = "رقم الهاتف مطلوب" });

            string otpCode = new Random().Next(100000, 999999).ToString();
            HttpContext.Session.SetString("VerifiedOTP", otpCode);

            // الإرسال الفعلي لشركة الـ SMS
            var smsResult = await _smsService.SendOtpAsync(phone, otpCode);

            if (smsResult.IsSuccess)
            {
                return Json(new { success = true, message = "تم إرسال رمز التحقق بنجاح!" });
            }

            // في حال فشل الإرسال (رصيد غير كافٍ أو خطأ في المزود)
            return Json(new { success = false, message = "فشل إرسال رمز التحقق، يرجى المحاولة لاحقاً", provider_error = "فشل إرسال رمز التحقق، يرجى المحاولة لاحقاً" });
            }

        // =========================================================
        // 4. نسيت كلمة المرور - الوضع الحي (LIVE)
        // =========================================================
        [HttpGet]
        public IActionResult ForgotPassword() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(string phone)
        {
            if (string.IsNullOrEmpty(phone)) return View();

            var user = await _userManager.FindByNameAsync(phone);
            // للحماية: إذا كان غير مسجل نوجهه وكأن العملية نجحت لمنع تخمين الأرقام
            if (user == null) return RedirectToAction("ResetPassword", new { phone = phone });

            // 🚨 التعديل: إنشاء كود OTP من 6 أرقام بدلاً من الرابط الطويل
            string otpCode = new Random().Next(100000, 999999).ToString();
            HttpContext.Session.SetString($"ResetOTP_{phone}", otpCode);

            string smsMessage = $"ديسكا: كود استعادة كلمة المرور الخاص بك هو: {otpCode}";

            // الإرسال الفعلي لشركة الـ SMS
            var smsResult = await _smsService.SendSmsAsync(phone, smsMessage);

            if (!smsResult.IsSuccess)
            {
                TempData["Error"] = $"تعذر إرسال رسالة الاستعادة: {smsResult.Message}";
                return View();
            }

            // 🚨 التوجيه لصفحة إدخال الكود وكلمة المرور الجديدة
            return RedirectToAction("ResetPassword", new { phone = phone });
        }

        // =========================================================
        // 5. استعادة كلمة المرور
        // =========================================================
        [HttpGet]
        public IActionResult ResetPassword(string phone = null)
        {
            if (string.IsNullOrEmpty(phone)) return RedirectToAction("ForgotPassword");
            return View(new ResetPasswordViewModel { Phone = phone });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            var user = await _userManager.FindByNameAsync(model.Phone);
            if (user == null) return RedirectToAction("ResetPasswordConfirmation");

            // 🚨 التعديل: التحقق من كود الـ OTP المدخل من المستخدم
            var savedOtp = HttpContext.Session.GetString($"ResetOTP_{model.Phone}");
            if (string.IsNullOrEmpty(savedOtp) || savedOtp != model.Code)
            {
                ModelState.AddModelError(string.Empty, "كود التحقق غير صحيح أو منتهي الصلاحية.");
                return View(model);
            }

            // 🚨 إنشاء توكن الاستعادة الحقيقي الخاص بـ Identity وتغيير كلمة المرور فوراً
            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, resetToken, model.Password);

            if (result.Succeeded)
            {
                // مسح الكود من الجلسة بعد النجاح لحماية الحساب
                HttpContext.Session.Remove($"ResetOTP_{model.Phone}");
                return RedirectToAction("ResetPasswordConfirmation");
            }

            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, "فشل إرسال رمز التحقق، يرجى المحاولة لاحقاً");
            return View(model);
        }

        [HttpGet] public IActionResult ResetPasswordConfirmation() => View();
        [HttpGet] public IActionResult ForgotPasswordConfirmation() => View();
        [HttpGet] public IActionResult AccessDenied() => View();

        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        private IActionResult RedirectToRoleDashboard()
        {
            if (User.IsInRole("Admin")) return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
            if (User.IsInRole("Merchant")) return RedirectToAction("Index", "Dashboard", new { area = "Merchant" });
            return RedirectToAction("Index", "Home", new { area = "" });
        }
    }

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