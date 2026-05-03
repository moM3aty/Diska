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

            if (string.IsNullOrEmpty(phone) || string.IsNullOrEmpty(password))
            {
                TempData["Error"] = "من فضلك أدخل البيانات كاملة";
                return View();
            }

            var user = await _userManager.FindByNameAsync(phone) ?? _userManager.Users.FirstOrDefault(u => u.PhoneNumber == phone);

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
                    if (await _userManager.IsInRoleAsync(user, "Merchant") && !user.IsVerifiedMerchant)
                    {
                        await _signInManager.SignOutAsync();
                        TempData["Error"] = "حساب التاجر الخاص بك قيد المراجعة ولم يتم تفعيله بعد.";
                        return View();
                    }

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

            TempData["Error"] = string.Join(", ", result.Errors.Select(e => e.Description));
            return View();
        }

        // =========================================================
        // 3. API إرسال OTP عبر AJAX (لشاشة التسجيل)
        // =========================================================
        [HttpPost]
        public async Task<IActionResult> SendOtpAjax(string phone)
        {
            if (string.IsNullOrEmpty(phone)) return Json(new { success = false, message = "رقم الهاتف مطلوب" });

            string otpCode = new Random().Next(100000, 999999).ToString();
            HttpContext.Session.SetString("VerifiedOTP", otpCode);

            var smsResult = await _smsService.SendOtpAsync(phone, otpCode);

            if (smsResult.IsSuccess)
            {
                return Json(new { success = true, message = "تم إرسال رمز التحقق بنجاح!" });
            }

            return Json(new { success = false, message = "فشل الإرسال", provider_error = smsResult.Message });
        }

        // =========================================================
        // 4. نسيت كلمة المرور
        // =========================================================
        [HttpGet]
        public IActionResult ForgotPassword() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(string phone)
        {
            if (string.IsNullOrEmpty(phone)) return View();

            var user = await _userManager.FindByNameAsync(phone);
            if (user == null) return View("ForgotPasswordConfirmation");

            var code = await _userManager.GeneratePasswordResetTokenAsync(user);
            string resetLink = Url.Action("ResetPassword", "Account", new { code = code, phone = phone }, Request.Scheme);
            string smsMessage = $"ديسكا: لاستعادة كلمة المرور، اضغط على الرابط: {resetLink}";

            var smsResult = await _smsService.SendSmsAsync(phone, smsMessage);

            if (!smsResult.IsSuccess)
            {
                // هنا سيتم طباعة سبب الرفض من الشركة على الشاشة
                TempData["Error"] = $"تم الرفض من شركة الاتصالات: {smsResult.Message}";
                return View();
            }

            return View("ForgotPasswordConfirmation");
        }

        // =========================================================
        // 5. استعادة كلمة المرور
        // =========================================================
        [HttpGet]
        public IActionResult ResetPassword(string code = null, string phone = null)
        {
            if (code == null) return BadRequest("A code must be supplied for password reset.");
            return View(new ResetPasswordViewModel { Code = code, Phone = phone });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            var user = await _userManager.FindByNameAsync(model.Phone);
            if (user == null) return RedirectToAction("ResetPasswordConfirmation");

            var result = await _userManager.ResetPasswordAsync(user, model.Code, model.Password);
            if (result.Succeeded) return RedirectToAction("ResetPasswordConfirmation");

            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
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