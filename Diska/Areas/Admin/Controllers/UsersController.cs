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
                        Email = user.Email,
                        IsLocked = await _userManager.IsLockedOutAsync(user)
                    });
                }
            }

            ViewBag.CurrentRole = role;
            return View(model);
        }

        // عرض مصفوفة الصلاحيات (Permission Matrix)
        public IActionResult Permissions()
        {
            // هذه البيانات للعرض فقط لتوضيح الصلاحيات في النظام
            return View();
        }

        public async Task<IActionResult> Details(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            ViewBag.UserRole = roles.FirstOrDefault();

            ViewBag.OrdersCount = await _context.Orders.CountAsync(o => o.UserId == id);
            ViewBag.TotalSpent = await _context.Orders.Where(o => o.UserId == id && o.Status != "Cancelled").SumAsync(o => (decimal?)o.TotalAmount) ?? 0;

            // جلب سجلات الدخول (إذا لم تكن مضافة للـ DBContext بعد، سنرسل قائمة فارغة تجنباً للخطأ)
            var loginHistory = new List<UserLoginLog>();
            try
            {
                // loginHistory = await _context.UserLoginLogs.Where(l => l.UserId == id).OrderByDescending(l => l.LoginTime).Take(20).ToListAsync();
                // محاكاة بيانات للعرض
                loginHistory.Add(new UserLoginLog { LoginTime = DateTime.Now.AddHours(-1), IpAddress = "192.168.1.1", DeviceInfo = "Chrome / Windows", IsSuccess = true });
                loginHistory.Add(new UserLoginLog { LoginTime = DateTime.Now.AddDays(-1), IpAddress = "192.168.1.1", DeviceInfo = "Mobile / Android", IsSuccess = true });
                loginHistory.Add(new UserLoginLog { LoginTime = DateTime.Now.AddDays(-5), IpAddress = "10.0.0.5", DeviceInfo = "Chrome / Windows", IsSuccess = false, FailureReason = "Wrong Password" });
            }
            catch { }

            var viewModel = new UserDetailsViewModel
            {
                User = user,
                Orders = await _context.Orders.Where(o => o.UserId == id).OrderByDescending(o => o.OrderDate).Take(5).ToListAsync(),
                Transactions = await _context.WalletTransactions.Where(t => t.UserId == id).OrderByDescending(t => t.TransactionDate).Take(5).ToListAsync(),
                LoginLogs = loginHistory
            };

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> ToggleVerification(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                user.IsVerifiedMerchant = !user.IsVerifiedMerchant;
                await _userManager.UpdateAsync(user);

                string msg = user.IsVerifiedMerchant ? "تم تفعيل حساب التاجر الخاص بك." : "تم إيقاف صلاحيات التاجر مؤقتاً.";
                await _notificationService.NotifyUserAsync(user.Id, "تحديث حالة الحساب", msg, "System");
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> ManageBalance(string id, decimal amount, string type, string reason)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null && amount > 0)
            {
                decimal finalAmount = (type == "deduct") ? -amount : amount;
                user.WalletBalance += finalAmount;

                _context.WalletTransactions.Add(new WalletTransaction
                {
                    UserId = user.Id,
                    Amount = finalAmount,
                    Type = (type == "deduct") ? "Deduction" : "Deposit",
                    Description = reason ?? "تسوية إدارية",
                    TransactionDate = DateTime.Now
                });

                await _context.SaveChangesAsync();
                await _userManager.UpdateAsync(user);

                string typeMsg = (type == "deduct") ? "خصم" : "إضافة";
                await _notificationService.NotifyUserAsync(user.Id, "تحديث المحفظة", $"تم {typeMsg} مبلغ {amount} ج.م. السبب: {reason}", "Wallet");
                TempData["Success"] = $"تم {typeMsg} الرصيد بنجاح.";
            }
            return RedirectToAction(nameof(Details), new { id = id });
        }

        [HttpPost]
        public async Task<IActionResult> ChangeRole(string id, string newRole)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                var currentRoles = await _userManager.GetRolesAsync(user);
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
                await _userManager.AddToRoleAsync(user, newRole);

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
            if (user == null) return NotFound();
            if (await _userManager.IsInRoleAsync(user, "Admin")) return RedirectToAction(nameof(Index));

            try
            {
                // حذف البيانات الفرعية
                var notifs = _context.UserNotifications.Where(u => u.UserId == id);
                _context.UserNotifications.RemoveRange(notifs);
                await _context.SaveChangesAsync();

                var result = await _userManager.DeleteAsync(user);
                if (!result.Succeeded) throw new Exception();
                TempData["Success"] = "تم حذف المستخدم.";
            }
            catch
            {
                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
                TempData["Warning"] = "تم حظر الحساب بدلاً من حذفه لوجود بيانات مرتبطة.";
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
        public bool IsLocked { get; set; }
    }

    public class UserDetailsViewModel
    {
        public ApplicationUser User { get; set; }
        public List<Order> Orders { get; set; }
        public List<WalletTransaction> Transactions { get; set; }
        public List<UserLoginLog> LoginLogs { get; set; } // جديد
    }
}