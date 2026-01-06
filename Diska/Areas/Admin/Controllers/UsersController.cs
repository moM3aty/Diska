using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Microsoft.AspNetCore.Identity;
using Diska.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Diska.Services;

namespace Diska.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class UsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;

        public UsersController(UserManager<ApplicationUser> userManager, ApplicationDbContext context, INotificationService notificationService)
        {
            _userManager = userManager;
            _context = context;
            _notificationService = notificationService;
        }

        public async Task<IActionResult> Index(string role = "All")
        {
            var users = await _userManager.Users.ToListAsync();
            var model = new List<UserViewModel>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var userRole = roles.FirstOrDefault() ?? "Customer";

                if (role == "All" || role == userRole)
                {
                    model.Add(new UserViewModel
                    {
                        Id = user.Id,
                        FullName = user.FullName,
                        PhoneNumber = user.PhoneNumber,
                        ShopName = user.ShopName,
                        Role = userRole,
                        WalletBalance = user.WalletBalance,
                        IsVerified = user.IsVerifiedMerchant
                    });
                }
            }

            return View(model);
        }

        public async Task<IActionResult> Details(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            ViewBag.Role = roles.FirstOrDefault();

            // جلب سجل الطلبات
            ViewBag.Orders = await _context.Orders
                .Where(o => o.UserId == id)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            // جلب سجل المحفظة
            ViewBag.Transactions = await _context.WalletTransactions
                .Where(t => t.UserId == id)
                .OrderByDescending(t => t.TransactionDate)
                .ToListAsync();

            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> ToggleVerification(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                user.IsVerifiedMerchant = !user.IsVerifiedMerchant;
                await _userManager.UpdateAsync(user);

                string msg = user.IsVerifiedMerchant ? "تم تفعيل حساب التاجر الخاص بك." : "تم إيقاف حساب التاجر مؤقتاً.";
                await _notificationService.NotifyUserAsync(user.Id, "تحديث الحساب", msg, "System");
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> AddBalance(string id, decimal amount, string reason)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null && amount != 0)
            {
                user.WalletBalance += amount;

                _context.WalletTransactions.Add(new WalletTransaction
                {
                    UserId = user.Id,
                    Amount = amount,
                    Type = amount > 0 ? "Deposit" : "Deduction",
                    Description = reason ?? "تعديل رصيد من الإدارة",
                    TransactionDate = DateTime.Now
                });

                await _context.SaveChangesAsync();
                await _userManager.UpdateAsync(user);

                await _notificationService.NotifyUserAsync(user.Id, "تحديث الرصيد", $"تم إضافة/خصم {amount} ج.م من محفظتك.", "Wallet");
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                await _userManager.DeleteAsync(user);
            }
            return RedirectToAction(nameof(Index));
        }
    }

    public class UserViewModel
    {
        public string Id { get; set; }
        public string FullName { get; set; }
        public string PhoneNumber { get; set; }
        public string ShopName { get; set; }
        public string Role { get; set; }
        public decimal WalletBalance { get; set; }
        public bool IsVerified { get; set; }
    }
}