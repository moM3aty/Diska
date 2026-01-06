using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace Diska.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class WalletController : Controller
    {
        private readonly ApplicationDbContext _context;

        public WalletController(ApplicationDbContext context)
        {
            _context = context;
        }

        // تقرير مالي شامل
        public async Task<IActionResult> Index()
        {
            var transactions = await _context.WalletTransactions
                .Include(t => t.User)
                .OrderByDescending(t => t.TransactionDate)
                .ToListAsync();

            // حساب الإجماليات
            ViewBag.TotalDeposits = transactions.Where(t => t.Type == "Deposit" || t.Type == "Refund").Sum(t => t.Amount);
            ViewBag.TotalSales = transactions.Where(t => t.Type == "Purchase").Sum(t => t.Amount);

            return View(transactions);
        }
    }
}