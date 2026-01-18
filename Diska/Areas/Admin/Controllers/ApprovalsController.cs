using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Diska.Models;
using Diska.Areas.Admin.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;

namespace Diska.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ApprovalsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ApprovalsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var viewModel = new ApprovalsViewModel();

            // 1. جلب التجار غير الموثقين
            var allMerchants = await _userManager.GetUsersInRoleAsync("Merchant");
            viewModel.NewMerchants = allMerchants.Where(u => !u.IsVerifiedMerchant).ToList();

            // 2. جلب المنتجات المعلقة
            viewModel.PendingProducts = await _context.Products
                .Include(p => p.Merchant)
                .Include(p => p.Category)
                .Where(p => p.Status == "Pending")
                .ToListAsync();

            // 3. جلب الطلبات الأخرى
            viewModel.OtherActions = await _context.PendingMerchantActions
                .Include(a => a.Merchant)
                .Where(a => a.Status == "Pending")
                .OrderByDescending(a => a.RequestDate)
                .ToListAsync();

            return View(viewModel);
        }

        // --- صفحة معاينة المنتج للموافقة (جديد) ---
        [HttpGet]
        public async Task<IActionResult> ProductDetails(int id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Merchant)
                .Include(p => p.ProductColors)
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null) return NotFound();

            return View(product);
        }

        // --- إجراءات التجار ---
        [HttpPost]
        public async Task<IActionResult> ApproveMerchant(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                user.IsVerifiedMerchant = true;
                await _userManager.UpdateAsync(user);
                TempData["Success"] = $"تم توثيق التاجر {user.ShopName} بنجاح.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> RejectMerchant(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                await _userManager.DeleteAsync(user);
                TempData["Success"] = "تم رفض طلب التاجر وحذف الحساب.";
            }
            return RedirectToAction(nameof(Index));
        }

        // --- إجراءات المنتجات ---
        [HttpPost]
        public async Task<IActionResult> ApproveProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                product.Status = "Active";
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم نشر المنتج بنجاح.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> RejectProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                product.Status = "Rejected";
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم رفض المنتج.";
            }
            return RedirectToAction(nameof(Index));
        }

        // --- إجراءات أخرى ---
        [HttpPost]
        public async Task<IActionResult> ApproveAction(int id)
        {
            var action = await _context.PendingMerchantActions.FindAsync(id);
            if (action == null) return NotFound();

            if (action.ActionType == "UpdateProductPrice")
            {
                var product = await _context.Products.FindAsync(int.Parse(action.EntityId));
                if (product != null)
                {
                    using (JsonDocument doc = JsonDocument.Parse(action.NewValueJson))
                    {
                        if (doc.RootElement.TryGetProperty("Price", out JsonElement priceElement))
                        {
                            product.Price = priceElement.GetDecimal();
                            _context.Products.Update(product);
                        }
                    }
                }
            }

            action.Status = "Approved";
            action.ProcessedDate = DateTime.Now;
            await _context.SaveChangesAsync();

            TempData["Success"] = "تمت الموافقة على الطلب.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> RejectAction(int id)
        {
            var action = await _context.PendingMerchantActions.FindAsync(id);
            if (action != null)
            {
                action.Status = "Rejected";
                action.ProcessedDate = DateTime.Now;
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم رفض الطلب.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}