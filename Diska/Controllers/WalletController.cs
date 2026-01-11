using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Diska.Models;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;
using System.Linq;

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

        // 1. الواجهة الرئيسية للمحفظة (الرصيد + ملخص)
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            // جلب آخر 5 معاملات فقط للعرض في الصفحة الرئيسية
            var recentTransactions = await _context.WalletTransactions
                .Where(t => t.UserId == user.Id)
                .OrderByDescending(t => t.TransactionDate)
                .Take(5)
                .ToListAsync();

            ViewBag.Balance = user.WalletBalance;
            return View(recentTransactions);
        }

        // 2. سجل المعاملات الكامل
        public async Task<IActionResult> Transactions()
        {
            var user = await _userManager.GetUserAsync(User);

            var transactions = await _context.WalletTransactions
                .Where(t => t.UserId == user.Id)
                .OrderByDescending(t => t.TransactionDate)
                .ToListAsync();

            return View(transactions);
        }

        // 3. شحن الرصيد (GET)
        [HttpGet]
        public IActionResult TopUp()
        {
            return View();
        }

        // 3. شحن الرصيد (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TopUp(decimal amount)
        {
            if (amount <= 0)
            {
                TempData["Error"] = "يجب إدخال مبلغ صحيح أكبر من صفر.";
                return View();
            }

            var user = await _userManager.GetUserAsync(User);

            // إضافة الرصيد
            user.WalletBalance += amount;

            // تسجيل المعاملة
            var transaction = new WalletTransaction
            {
                UserId = user.Id,
                Amount = amount,
                Type = "Deposit", // إيداع
                Description = "شحن رصيد (عملية تجريبية)",
                TransactionDate = DateTime.Now
            };

            _context.WalletTransactions.Add(transaction);
            await _userManager.UpdateAsync(user);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"تم شحن {amount.ToString("N0")} ج.م إلى محفظتك بنجاح!";
            return RedirectToAction(nameof(Index));
        }
    }
}