using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Diska.Models
{
    public class DealRequest
    {
        public int Id { get; set; }

        public string UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; } // إضافة العلاقة

        [Display(Name = "المنتج المطلوب")]
        [Required(ErrorMessage = "اسم المنتج مطلوب")]
        public string ProductName { get; set; }

        [Display(Name = "الكمية")]
        [Required(ErrorMessage = "الكمية المطلوبة")]
        public int TargetQuantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "السعر المستهدف")]
        [Required(ErrorMessage = "السعر المستهدف مطلوب")]
        public decimal DealPrice { get; set; }

        [Display(Name = "الموقع / المدينة")]
        public string Location { get; set; }

        // Workflow: Pending (New) -> InReview -> Approved -> Rejected
        public string Status { get; set; } = "Pending";

        // Admin Fields
        public string AdminNotes { get; set; } // ملاحظات الأدمن

        public DateTime RequestDate { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; } // تاريخ آخر تحديث

        public virtual ICollection<MerchantOffer> Offers { get; set; } = new List<MerchantOffer>();
    }
}