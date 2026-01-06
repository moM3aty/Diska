using System;
using System.Collections.Generic;
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
        public decimal DealPrice { get; set; }

        public int TargetQuantity { get; set; }
        public int ReservedQuantity { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        // تم التصحيح: خاصية قابلة للكتابة لحل خطأ (Property cannot be assigned to -- it is read only)
        public bool IsActive { get; set; } = true;
    }

    // نموذج طلبات الشراء الخاصة (Requests)
    public class DealRequest
    {
        public int Id { get; set; }
        public string UserId { get; set; }

        [Required(ErrorMessage = "اسم المنتج مطلوب")]
        public string ProductName { get; set; }

        [Required(ErrorMessage = "الكمية المطلوبة مطلوبة")]
        public int TargetQuantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Required(ErrorMessage = "السعر المستهدف مطلوب")]
        public decimal DealPrice { get; set; }

        public string Location { get; set; }
        public string Status { get; set; } = "Pending";
        public DateTime RequestDate { get; set; } = DateTime.Now;

        // تم التصحيح: إضافة القائمة المفقودة لحل خطأ (does not contain a definition for 'Offers')
        public virtual ICollection<MerchantOffer> Offers { get; set; } = new List<MerchantOffer>();
    }

    // نموذج الإشعارات (Notifications)
    public class UserNotification
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public string Type { get; set; } = "Info";
        public string Link { get; set; }
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