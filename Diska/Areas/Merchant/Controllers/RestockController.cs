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
            if (user == null) return RedirectToAction("Login", "Account", new { area = "" });

            var requests = await _context.Products
                .Include(p => p.Merchant)
                .Include(p => p.Category)
                .Where(p => p.MerchantId == user.Id)
                .Where(p => p.StockQuantity <= p.LowStockThreshold)
                .OrderBy(p => p.StockQuantity)
                .ToListAsync();

            return View(requests);
        }
    }
}