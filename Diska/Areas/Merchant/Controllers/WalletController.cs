using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Diska.Models;
using System.Text.Json;
using Diska.Services;

namespace Diska.Areas.Merchant.Controllers
{
    [Area("Merchant")]
    [Authorize(Roles = "Merchant")]
    public class WalletController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notificationService;

        public WalletController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, INotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _notificationService = notificationService;
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
            ViewBag.TotalWithdrawals = transactions.Where(t => t.Type == "Withdraw" || t.Type == "Deduction").Sum(t => Math.Abs(t.Amount));

            return View(transactions);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestWithdraw(decimal amount)
        {
            var user = await _userManager.GetUserAsync(User);

            if (amount <= 0)
            {
                TempData["Error"] = "يرجى إدخال مبلغ صحيح.";
                return RedirectToAction(nameof(Index));
            }

            if (amount > user.WalletBalance)
            {
                TempData["Error"] = "رصيدك الحالي لا يسمح بهذا المبلغ.";
                return RedirectToAction(nameof(Index));
            }

            // إنشاء طلب سحب للإدارة
            var action = new PendingMerchantAction
            {
                MerchantId = user.Id,
                ActionType = "WithdrawRequest",
                EntityName = "Wallet",
                EntityId = user.Id,
                NewValueJson = JsonSerializer.Serialize(new { Amount = amount }),
                OldValueJson = "{}",
                Status = "Pending",
                RequestDate = DateTime.Now,
                ActionByAdminId = string.Empty,
                AdminComment = string.Empty
            };

            _context.PendingMerchantActions.Add(action);
            await _context.SaveChangesAsync();

            await _notificationService.NotifyAdminsAsync("طلب سحب رصيد", $"التاجر {user.ShopName} يطلب سحب مبلغ {amount} ج.م");

            TempData["Success"] = "تم إرسال طلب سحب الرصيد للإدارة للمراجعة.";
            return RedirectToAction(nameof(Index));
        }
    }
}