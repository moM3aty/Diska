using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Diska.Models;

namespace Diska.Areas.Merchant.Controllers
{
    [Area("Merchant")]
    [Authorize(Roles = "Merchant")]
    public class RestockController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public RestockController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // عرض قائمة العملاء المنتظرين لمنتجات هذا التاجر
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            var requests = await _context.RestockSubscriptions
                .Include(r => r.Product)
                .Where(r => r.Product.MerchantId == user.Id) // فقط لمنتجاتي
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            return View(requests);
        }
    }
}