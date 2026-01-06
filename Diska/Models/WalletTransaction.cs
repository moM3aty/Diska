using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Diska.Models
{
    // نموذج حركة المحفظة (سجل العمليات)
    public class WalletTransaction
    {
        public int Id { get; set; }

        public string UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; } // المبلغ (+ للإيداع/الاسترداد، - للشراء)

        public string Description { get; set; } // وصف العملية (مثلاً: شراء طلب رقم #10)

        public string Type { get; set; } // "Deposit" (إيداع), "Purchase" (شراء), "Refund" (استرداد)

        public DateTime TransactionDate { get; set; } = DateTime.Now;
    }
}