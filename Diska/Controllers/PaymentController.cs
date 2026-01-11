using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Diska.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Diska.Services;
using System.Threading.Tasks;

namespace Diska.Controllers
{
    [Authorize]
    public class PaymentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IPaymentService _paymentService;

        public PaymentController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IPaymentService paymentService)
        {
            _context = context;
            _userManager = userManager;
            _paymentService = paymentService;
        }

        // صفحة الدفع لطلب محدد (مثلاً عند اختيار الدفع أونلاين أو إعادة المحاولة)
        [HttpGet]
        public async Task<IActionResult> Checkout(int orderId)
        {
            var user = await _userManager.GetUserAsync(User);
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == user.Id);

            if (order == null) return NotFound();

            // إذا كان الطلب مدفوعاً بالفعل
            if (order.Status == "Confirmed" || order.Status == "Shipped" || order.Status == "Delivered")
            {
                return RedirectToAction("Success");
            }

            return View(order);
        }

        // بدء عملية الدفع (يتم استدعاؤها من زر "ادفع الآن")
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InitiatePayment(int orderId)
        {
            var user = await _userManager.GetUserAsync(User);
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == user.Id);

            if (order == null) return NotFound();

            try
            {
                // استدعاء خدمة الدفع للحصول على رابط البوابة (Paymob/Stripe/etc)
                // نمرر الـ Callback URL للعودة بعد الدفع
                var callbackUrl = Url.Action("Callback", "Payment", new { orderId = order.Id }, Request.Scheme);

                string paymentUrl = await _paymentService.InitiatePaymentAsync(order.TotalAmount, "EGP", new
                {
                    OrderId = order.Id,
                    CustomerName = user.FullName,
                    Phone = user.PhoneNumber,
                    CallbackUrl = callbackUrl
                });

                return Redirect(paymentUrl);
            }
            catch
            {
                return RedirectToAction("Failed", new { orderId = order.Id });
            }
        }

        // الرد من بوابة الدفع (Callback)
        [HttpGet]
        public async Task<IActionResult> Callback(int orderId, string transactionId, bool success, string message)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null) return NotFound();

            if (success)
            {
                // التحقق من صحة المعاملة من السيرفر (Server-to-Server Check)
                bool isVerified = await _paymentService.VerifyPaymentAsync(transactionId);

                if (isVerified)
                {
                    order.Status = "Confirmed";
                    order.PaymentMethod = "Credit Card (Paid)";

                    // تسجيل المعاملة
                    _context.WalletTransactions.Add(new WalletTransaction
                    {
                        UserId = order.UserId,
                        Amount = order.TotalAmount,
                        Type = "Payment",
                        Description = $"دفع إلكتروني للطلب #{orderId} (Ref: {transactionId})",
                        TransactionDate = DateTime.Now
                    });

                    await _context.SaveChangesAsync();
                    return RedirectToAction("Success", new { orderId = order.Id });
                }
            }

            return RedirectToAction("Failed", new { orderId = order.Id });
        }

        public IActionResult Success(int orderId)
        {
            ViewBag.OrderId = orderId;
            return View();
        }

        public IActionResult Failed(int orderId)
        {
            ViewBag.OrderId = orderId;
            return View();
        }
    }
}