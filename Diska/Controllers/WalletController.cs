using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Diska.Models;
using Microsoft.AspNetCore.Authorization;

namespace Diska.Controllers
{
    [Authorize]
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

            // جلب سجل المعاملات
            var transactions = await _context.WalletTransactions
                .Where(t => t.UserId == user.Id)
                .OrderByDescending(t => t.TransactionDate)
                .ToListAsync();

            ViewBag.Balance = user.WalletBalance;
            return View(transactions);
        }

        // دالة شحن رصيد (لأغراض العرض التجريبي فقط - Demo)
        // في الواقع يتم ربطها ببوابة دفع
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TopUp(decimal amount)
        {
            if (amount <= 0) return RedirectToAction(nameof(Index));

            var user = await _userManager.GetUserAsync(User);

            user.WalletBalance += amount;

            _context.WalletTransactions.Add(new WalletTransaction
            {
                UserId = user.Id,
                Amount = amount,
                Type = "Deposit",
                Description = "شحن رصيد (تجريبي)",
                TransactionDate = DateTime.Now
            });

            await _context.SaveChangesAsync();
            await _userManager.UpdateAsync(user);

            TempData["Success"] = $"تم شحن {amount} ج.م بنجاح!";
            return RedirectToAction(nameof(Index));
        }
    }
}