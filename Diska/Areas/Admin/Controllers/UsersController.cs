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
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;

        public UsersController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ApplicationDbContext context, INotificationService notificationService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _notificationService = notificationService;
        }

        // عرض قائمة المستخدمين مع الفلترة
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
                        IsVerified = user.IsVerifiedMerchant,
                        Email = user.Email
                    });
                }
            }

            ViewBag.CurrentRole = role;
            return View(model);
        }

        // تفاصيل المستخدم الكاملة (بروفايل + طلبات + محفظة + صلاحيات)
        public async Task<IActionResult> Details(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            ViewBag.UserRole = roles.FirstOrDefault();

            // إحصائيات سريعة
            ViewBag.OrdersCount = await _context.Orders.CountAsync(o => o.UserId == id);
            ViewBag.TotalSpent = await _context.Orders.Where(o => o.UserId == id && o.Status != "Cancelled").SumAsync(o => (decimal?)o.TotalAmount) ?? 0;

            // جلب السجلات
            var recentOrders = await _context.Orders
                .Where(o => o.UserId == id)
                .OrderByDescending(o => o.OrderDate)
                .Take(10)
                .ToListAsync();

            var walletHistory = await _context.WalletTransactions
                .Where(t => t.UserId == id)
                .OrderByDescending(t => t.TransactionDate)
                .Take(10)
                .ToListAsync();

            var viewModel = new UserDetailsViewModel
            {
                User = user,
                Orders = recentOrders,
                Transactions = walletHistory
            };

            return View(viewModel);
        }

        // توثيق / إلغاء توثيق التاجر
        [HttpPost]
        public async Task<IActionResult> ToggleVerification(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                user.IsVerifiedMerchant = !user.IsVerifiedMerchant;
                await _userManager.UpdateAsync(user);

                string msg = user.IsVerifiedMerchant
                    ? "تم تفعيل حساب التاجر الخاص بك، يمكنك الآن إضافة منتجات."
                    : "تم إيقاف صلاحيات التاجر مؤقتاً، يرجى مراجعة الإدارة.";

                await _notificationService.NotifyUserAsync(user.Id, "تحديث حالة الحساب", msg, "System");
            }
            return RedirectToAction(nameof(Index)); // أو العودة للـ Details حسب المصدر
        }

        // إضافة / خصم رصيد
        [HttpPost]
        public async Task<IActionResult> ManageBalance(string id, decimal amount, string reason)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null && amount != 0)
            {
                user.WalletBalance += amount;

                _context.WalletTransactions.Add(new WalletTransaction
                {
                    UserId = user.Id,
                    Amount = amount, // يمكن أن يكون سالباً للخصم
                    Type = amount > 0 ? "Deposit" : "Deduction",
                    Description = reason ?? "تسوية إدارية",
                    TransactionDate = DateTime.Now
                });

                await _context.SaveChangesAsync();
                await _userManager.UpdateAsync(user);

                string typeMsg = amount > 0 ? "إضافة" : "خصم";
                await _notificationService.NotifyUserAsync(user.Id, "تحديث المحفظة", $"تم {typeMsg} مبلغ {Math.Abs(amount)} ج.م. السبب: {reason}", "Wallet");

                TempData["Success"] = "تم تحديث الرصيد بنجاح.";
            }
            return RedirectToAction(nameof(Details), new { id = id });
        }

        // تغيير الصلاحية (Role)
        [HttpPost]
        public async Task<IActionResult> ChangeRole(string id, string newRole)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                var currentRoles = await _userManager.GetRolesAsync(user);
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
                await _userManager.AddToRoleAsync(user, newRole);

                // إذا أصبح تاجراً، نجعله غير موثق مبدئياً
                if (newRole == "Merchant")
                {
                    user.IsVerifiedMerchant = false;
                    await _userManager.UpdateAsync(user);
                }

                TempData["Success"] = $"تم تغيير صلاحية المستخدم إلى {newRole}.";
            }
            return RedirectToAction(nameof(Details), new { id = id });
        }

        [HttpPost]
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                // لا يمكن حذف الأدمن الرئيسي
                if (user.UserName == "01000000000")
                {
                    TempData["Error"] = "لا يمكن حذف الأدمن الرئيسي.";
                    return RedirectToAction(nameof(Index));
                }

                await _userManager.DeleteAsync(user);
                TempData["Success"] = "تم حذف المستخدم نهائياً.";
            }
            return RedirectToAction(nameof(Index));
        }
    }

    public class UserViewModel
    {
        public string Id { get; set; }
        public string FullName { get; set; }
        public string PhoneNumber { get; set; }
        public string Email { get; set; }
        public string ShopName { get; set; }
        public string Role { get; set; }
        public decimal WalletBalance { get; set; }
        public bool IsVerified { get; set; }
    }

    public class UserDetailsViewModel
    {
        public ApplicationUser User { get; set; }
        public List<Order> Orders { get; set; }
        public List<WalletTransaction> Transactions { get; set; }
    }
}