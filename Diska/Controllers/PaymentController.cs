using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Diska.Services;
using Microsoft.EntityFrameworkCore;
using Diska.Models;

namespace Diska.Controllers
{
    // كنترولر للتعامل مع ردود بوابات الدفع
    public class PaymentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPaymentService _paymentService;

        public PaymentController(ApplicationDbContext context, IPaymentService paymentService)
        {
            _context = context;
            _paymentService = paymentService;
        }

        // صفحة التحقق بعد العودة من بوابة الدفع
        [HttpGet]
        public async Task<IActionResult> Callback(string transactionId, int orderId, bool success)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null) return NotFound();

            if (success)
            {
                // التحقق من المعاملة مع السيرفر
                bool verified = await _paymentService.VerifyPaymentAsync(transactionId);

                if (verified)
                {
                    order.Status = "Confirmed"; // أو "Paid"
                    // إضافة معاملة محفظة (اختياري للتوثيق)
                    _context.WalletTransactions.Add(new WalletTransaction
                    {
                        UserId = order.UserId,
                        Amount = order.TotalAmount,
                        Type = "Payment",
                        Description = $"دفع إلكتروني للطلب #{orderId} (Ref: {transactionId})",
                        TransactionDate = DateTime.Now
                    });

                    await _context.SaveChangesAsync();
                    return RedirectToAction("OrderSuccess", "Cart", new { id = orderId });
                }
            }

            return RedirectToAction("OrderFailed", "Cart", new { id = orderId });
        }

        // نقطة نهاية للـ Webhooks (تستخدمها شركات الدفع لإخطار السيرفر)
        [HttpPost]
        public async Task<IActionResult> Webhook([FromBody] object data)
        {
            // هنا يتم معالجة إشعارات الدفع الآلية
            // يتطلب Implementation حسب الشركة (Paymob/Stripe)
            await Task.CompletedTask;
            return Ok();
        }
    }
}