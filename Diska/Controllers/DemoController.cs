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

        public async Task<IActionResult> LoginAsGuest()
        {
            var guestUser = await _userManager.FindByNameAsync("01099999999");

            if (guestUser == null)
            {
                guestUser = new ApplicationUser
                {
                    UserName = "01099999999",
                    PhoneNumber = "01099999999",
                    FullName = "زائر تجريبي",
                    Email = "guest@diska.local",
                    ShopName = "متجر الزوار",
                    WalletBalance = 5000,
                    IsVerifiedMerchant = false,
                    CommercialRegister = "00000",
                    TaxCard = "00000"
                };

                var result = await _userManager.CreateAsync(guestUser, "Guest@123");
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(guestUser, "Customer");
                }
            }

            await _signInManager.SignInAsync(guestUser, isPersistent: false);

            return View("Welcome");
        }
    }
}