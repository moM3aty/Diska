using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Diska.Models;

namespace Diska.Controllers
{
    [Authorize]
    public class InvoiceController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public InvoiceController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // 1. عرض قائمة الفواتير (الطلبات المكتملة أو المؤكدة فقط)
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            // نعتبر الطلب فاتورة بمجرد تأكيده أو شحنه
            var invoices = await _context.Orders
                .Where(o => o.UserId == user.Id && o.Status != "Pending" && o.Status != "Cancelled")
                .OrderByDescending(o => o.OrderDate)
                .Select(o => new InvoiceViewModel
                {
                    Id = o.Id,
                    Date = o.OrderDate,
                    TotalAmount = o.TotalAmount,
                    Status = o.Status,
                    PaymentMethod = o.PaymentMethod
                })
                .ToListAsync();

            return View(invoices);
        }

        // 2. تفاصيل الفاتورة (للعرض)
        public async Task<IActionResult> Details(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var order = await GetOrderForInvoice(id, user.Id);

            if (order == null) return NotFound();

            return View(order);
        }

        // 3. صفحة الطباعة / تحميل PDF
        public async Task<IActionResult> Print(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var order = await GetOrderForInvoice(id, user.Id);

            if (order == null) return NotFound();

            // نستخدم Layout خاص أو null لتكون الصفحة نظيفة للطباعة
            return View(order);
        }

        // Helper
        private async Task<Order> GetOrderForInvoice(int id, string userId)
        {
            return await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);
        }
    }

    // ViewModel بسيط للقائمة
    public class InvoiceViewModel
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; }
        public string PaymentMethod { get; set; }
    }
}