using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Diska.Models;
using Diska.Services;

namespace Diska.Controllers
{
    [Authorize]
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notificationService;

        public OrderController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, INotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _notificationService = notificationService;
        }

        // 1. سجل الطلبات (Order History)
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var orders = await _context.Orders
                .Where(o => o.UserId == user.Id)
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return View(orders);
        }

        // 2. إنشاء طلب (توجيه للدفع أو معالجة مباشرة)
        [HttpGet]
        public IActionResult Create()
        {
            // عادة يتم إنشاء الطلب عبر سلة المشتريات (Cart/Checkout)
            // لذا سنقوم بالتوجيه لصفحة الدفع
            return RedirectToAction("Checkout", "Cart");
        }

        // 3. تفاصيل الطلب وتتبعه (Details & Tracking)
        public async Task<IActionResult> Details(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == user.Id);

            if (order == null) return NotFound();

            return View(order);
        }

        // 4. إلغاء الطلب (Cancel)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == user.Id);

            if (order == null) return NotFound();

            // السماح بالإلغاء فقط إذا كان الطلب "قيد الانتظار"
            if (order.Status == "Pending")
            {
                // إرجاع الكميات للمخزون
                foreach (var item in order.OrderItems)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product != null)
                    {
                        product.StockQuantity += item.Quantity;
                    }
                }

                // إذا كان الدفع بالمحفظة، يتم استرداد المبلغ
                if (order.PaymentMethod == "Wallet")
                {
                    user.WalletBalance += order.TotalAmount;

                    _context.WalletTransactions.Add(new WalletTransaction
                    {
                        UserId = user.Id,
                        Amount = order.TotalAmount,
                        Type = "Refund",
                        Description = $"استرداد قيمة الطلب الملغي #{id}",
                        TransactionDate = DateTime.Now
                    });

                    await _userManager.UpdateAsync(user);
                }

                order.Status = "Cancelled";
                await _context.SaveChangesAsync();

                // إشعار للإدارة (اختياري)
                // await _notificationService.NotifyAdminsAsync(...);

                TempData["Message"] = "تم إلغاء الطلب بنجاح وتم استرداد المبلغ (إن وجد).";
            }
            else
            {
                TempData["Error"] = "عفواً، لا يمكن إلغاء الطلب في هذه المرحلة (قد يكون تم تأكيده أو شحنه).";
            }

            return RedirectToAction(nameof(Index)); // العودة لسجل الطلبات
        }
    }
}