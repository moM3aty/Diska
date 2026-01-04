using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Diska.Models;
using System.Threading.Tasks;

namespace Diska.Controllers
{
    public class DemoController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;

        public DemoController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
        }

        // تسجيل دخول سريع كعميل (للتجربة)
        public async Task<IActionResult> LoginAsGuest()
        {
            // البحث عن حساب ديمو أو إنشاؤه
            var guestUser = await _userManager.FindByNameAsync("01099999999");
            if (guestUser == null)
            {
                guestUser = new ApplicationUser
                {
                    UserName = "01099999999",
                    PhoneNumber = "01099999999",
                    FullName = "زائر تجريبي",
                    Email = "guest@diska.local",
                    WalletBalance = 5000 // رصيد وهمي للتجربة
                };
                await _userManager.CreateAsync(guestUser, "Guest@123");
                await _userManager.AddToRoleAsync(guestUser, "Customer");
            }

            await _signInManager.SignInAsync(guestUser, isPersistent: false);
            return RedirectToAction("Index", "Home");
        }
    }
}