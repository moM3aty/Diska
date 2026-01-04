using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Diska.Models
{
    // نموذج الصفقات الجماعية (Deals)
    public class GroupDeal
    {
        public int Id { get; set; }

        public int ProductId { get; set; }
        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DealPrice { get; set; } // السعر المخفض

        public int TargetQuantity { get; set; } // الكمية المطلوبة لإتمام الصفقة
        public int ReservedQuantity { get; set; } // الكمية المحجوزة حالياً

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public bool IsActive => DateTime.Now >= StartDate && DateTime.Now <= EndDate && ReservedQuantity < TargetQuantity;
    }

    // نموذج طلبات الشراء الخاصة (Requests)
    public class DealRequest
    {
        public int Id { get; set; }
        public string UserId { get; set; } // صاحب الطلب

        [Required(ErrorMessage = "اسم المنتج مطلوب")]
        public string ProductName { get; set; }

        [Required(ErrorMessage = "الكمية المطلوبة مطلوبة")]
        public int TargetQuantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Required(ErrorMessage = "السعر المستهدف مطلوب")]
        public decimal DealPrice { get; set; }

        public string Location { get; set; }
        public string Status { get; set; } = "Pending"; // Pending, Approved, Completed, Cancelled
        public DateTime RequestDate { get; set; } = DateTime.Now;
    }


        public class UserNotification
        {
            public int Id { get; set; }

            [Required]
            public string UserId { get; set; }

            [Required]
            public string Title { get; set; }

            [Required]
            public string Message { get; set; }

            public string Type { get; set; } = "Info"; // Order, Deal, System, Alert

            public string Link { get; set; } // تم إضافة الرابط المفقود

            public bool IsRead { get; set; } = false;

            public DateTime CreatedAt { get; set; } = DateTime.Now;

            public string TimeAgo
            {
                get
                {
                    var span = DateTime.Now - CreatedAt;
                    if (span.TotalMinutes < 60) return $"منذ {(int)span.TotalMinutes} دقيقة";
                    if (span.TotalHours < 24) return $"منذ {(int)span.TotalHours} ساعة";
                    return CreatedAt.ToString("dd/MM/yyyy");
                }
            }
        }
    
}