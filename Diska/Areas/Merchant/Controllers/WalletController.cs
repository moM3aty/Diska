using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Diska.Models;
using Diska.Filters;

namespace Diska.Areas.Merchant.Controllers
{
    [Area("Merchant")]
    [Authorize(Roles = "Merchant")]
    // [CheckPermission("Wallet", "View")] // يمكن تفعيل هذا الفلتر إذا كنت تستخدم نظام الصلاحيات المتقدم للموظفين
    public class WalletController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public WalletController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            var transactions = await _context.WalletTransactions
                .Where(t => t.UserId == user.Id)
                .OrderByDescending(t => t.TransactionDate)
                .ToListAsync();

            ViewBag.Balance = user.WalletBalance;
            ViewBag.TotalEarnings = transactions.Where(t => t.Type == "Deposit" || t.Type == "Sale").Sum(t => t.Amount);
            ViewBag.TotalWithdrawals = transactions.Where(t => t.Type == "Withdraw" || t.Type == "Deduction").Sum(t => t.Amount);

            return View(transactions);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestWithdraw(decimal amount)
        {
            var user = await _userManager.GetUserAsync(User);

            if (amount > user.WalletBalance)
            {
                TempData["Error"] = "رصيدك الحالي لا يسمح بهذا المبلغ.";
                return RedirectToAction(nameof(Index));
            }

            var action = new PendingMerchantAction
            {
                MerchantId = user.Id,
                ActionType = "WithdrawRequest",
                EntityName = "Wallet",
                EntityId = user.Id,
                NewValueJson = System.Text.Json.JsonSerializer.Serialize(new { Amount = amount }),
                Status = "Pending",
                RequestDate = DateTime.Now
            };

            _context.PendingMerchantActions.Add(action);
            await _context.SaveChangesAsync();

            TempData["Success"] = "تم إرسال طلب سحب الرصيد للإدارة للمراجعة.";
            return RedirectToAction(nameof(Index));
        }
    }
}