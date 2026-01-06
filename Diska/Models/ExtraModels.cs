using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Diska.Models
{
    // 1. نموذج التقييمات
    public class ProductReview
    {
        public int Id { get; set; }

        public int ProductId { get; set; }
        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; }

        public string UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; }

        [Range(1, 5)]
        public int Rating { get; set; }

        public string Comment { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    // 2. نموذج تنبيهات المخزون
    public class RestockSubscription
    {
        public int Id { get; set; }

        public int ProductId { get; set; }
        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; }

        // التعديل هنا: جعل الحقل Nullable
        public string? UserId { get; set; }

        public string Email { get; set; } // للتواصل

        public DateTime RequestDate { get; set; } = DateTime.Now;
        public bool IsNotified { get; set; } = false;
    }
}