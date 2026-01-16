using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Diska.Models;
using System.Text;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Identity;
using Diska.Services;

namespace Diska.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
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

        // عرض سجل المعاملات المالي
        public async Task<IActionResult> Index(string userId, string type, DateTime? fromDate, DateTime? toDate)
        {
            var query = _context.WalletTransactions
                .Include(t => t.User)
                .AsQueryable();

            // الفلترة
            if (!string.IsNullOrEmpty(userId))
            {
                query = query.Where(t => t.UserId == userId);
            }
            if (!string.IsNullOrEmpty(type) && type != "All")
            {
                query = query.Where(t => t.Type == type);
            }
            if (fromDate.HasValue)
            {
                query = query.Where(t => t.TransactionDate >= fromDate.Value);
            }
            if (toDate.HasValue)
            {
                query = query.Where(t => t.TransactionDate <= toDate.Value);
            }

            // الإجماليات (للإحصائيات السريعة في الصفحة)
            // ملاحظة: الحسابات هنا تتم على النتائج المفلترة
            var transactions = await query.OrderByDescending(t => t.TransactionDate).ToListAsync();

            ViewBag.TotalDeposits = transactions.Where(t => t.Type == "Deposit" || t.Type == "Refund").Sum(t => t.Amount);
            ViewBag.TotalDeductions = transactions.Where(t => t.Type == "Purchase" || t.Type == "Deduction").Sum(t => t.Amount);
            ViewBag.NetFlow = ViewBag.TotalDeposits - ViewBag.TotalDeductions;

            // ملء القوائم للفلتر
            ViewBag.Users = new SelectList(await _userManager.Users.OrderBy(u => u.FullName).ToListAsync(), "Id", "FullName", userId);
            ViewBag.CurrentType = type;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

            return View(transactions);
        }

        // تنفيذ تسوية يدوية (Manual Adjustment)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdjustBalance(string userId, decimal amount, string type, string description)
        {
            if (amount <= 0)
            {
                TempData["Error"] = "يجب أن يكون المبلغ أكبر من صفر.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["Error"] = "المستخدم غير موجود.";
                return RedirectToAction(nameof(Index));
            }

            // تحديد العملية
            // Deposit: زيادة الرصيد
            // Deduction: خصم الرصيد
            decimal finalAmount = (type == "Deduction") ? -amount : amount;

            // التحقق من الرصيد في حالة الخصم
            if (type == "Deduction" && user.WalletBalance < amount)
            {
                // تحذير ولكن سنسمح بالعملية (يمكن أن يصبح الرصيد بالسالب في حالات التسوية)
                // أو يمكن منعها:
                // TempData["Error"] = "رصيد المستخدم لا يكفي للخصم.";
                // return RedirectToAction(nameof(Index));
            }

            user.WalletBalance += finalAmount;

            var transaction = new WalletTransaction
            {
                UserId = userId,
                Amount = finalAmount, // يخزن بالسالب للخصم، وبالموجب للإيداع
                Type = type, // Deposit, Deduction
                Description = description ?? "تسوية إدارية يدوية",
                TransactionDate = DateTime.Now
            };

            _context.WalletTransactions.Add(transaction);
            await _context.SaveChangesAsync();
            await _userManager.UpdateAsync(user);

            // إشعار المستخدم
            string actionText = type == "Deduction" ? "خصم" : "إيداع";
            await _notificationService.NotifyUserAsync(userId, "تحديث المحفظة", $"تم {actionText} مبلغ {amount} ج.م من قبل الإدارة. البيان: {description}", "Wallet");

            TempData["Success"] = $"تمت عملية {actionText} بنجاح للمستخدم {user.FullName}.";
            return RedirectToAction(nameof(Index));
        }

        // تصدير البيانات المالية
        [HttpPost]
        public async Task<IActionResult> Export(string userId, string type, DateTime? fromDate, DateTime? toDate)
        {
            var query = _context.WalletTransactions.Include(t => t.User).AsQueryable();

            if (!string.IsNullOrEmpty(userId)) query = query.Where(t => t.UserId == userId);
            if (!string.IsNullOrEmpty(type) && type != "All") query = query.Where(t => t.Type == type);
            if (fromDate.HasValue) query = query.Where(t => t.TransactionDate >= fromDate.Value);
            if (toDate.HasValue) query = query.Where(t => t.TransactionDate <= toDate.Value);

            var data = await query.OrderByDescending(t => t.TransactionDate).ToListAsync();

            var builder = new StringBuilder();

            // إضافة عناوين الأعمدة
            builder.AppendLine("رقم العملية,المستخدم,نوع العملية,المبلغ,البيان,التاريخ");

            foreach (var item in data)
            {
                string operationName = item.Type switch
                {
                    "Deposit" => "إيداع",
                    "Refund" => "استرداد",
                    "Purchase" => "شراء",
                    "Deduction" => "خصم إداري",
                    _ => item.Type
                };

                // تنظيف البيانات من الفواصل لتجنب تكسير ملف الـ CSV
                string cleanDesc = item.Description?.Replace(",", " ") ?? "";
                string cleanUser = item.User?.FullName?.Replace(",", " ") ?? "";

                builder.AppendLine($"{item.Id},{cleanUser},{operationName},{item.Amount},{cleanDesc},{item.TransactionDate}");
            }

            // استخدام UTF8Encoding(true) لإضافة BOM (Byte Order Mark)
            // هذا يجعل Excel يتعرف على الملف كـ UTF-8 ويظهر الحروف العربية بشكل صحيح
            var encoding = new UTF8Encoding(true);
            var preamble = encoding.GetPreamble();
            var content = encoding.GetBytes(builder.ToString());
            var result = preamble.Concat(content).ToArray();

            return File(result, "text/csv", $"Financial_Report_{DateTime.Now:yyyyMMdd}.csv");
        }
    }
}