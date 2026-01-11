using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Diska.Models
{
    public class GroupDeal
    {
        public int Id { get; set; }

        [Display(Name = "عنوان العرض")]
        [Required(ErrorMessage = "عنوان العرض مطلوب")]
        public string Title { get; set; } // اسم العرض (مثال: خصم الصيف)

        // الربط (إما منتج أو قسم)
        public int? ProductId { get; set; }
        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; }

        public int? CategoryId { get; set; } // جديد: دعم الأقسام
        [ForeignKey("CategoryId")]
        public virtual Category Category { get; set; }

        // نوع وقيمة الخصم
        public bool IsPercentage { get; set; } = false; // جديد: هل هو نسبة مئوية؟

        [Column(TypeName = "decimal(18,2)")]
        public decimal DealPrice { get; set; } // السعر النهائي (للمبلغ الثابت) أو قيمة الخصم

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountValue { get; set; } // جديد: قيمة الخصم (مثال: 10% أو 50 جنيه)

        // شروط العرض
        public int TargetQuantity { get; set; } // الكمية المستهدفة (للشراء الجماعي)
        public int ReservedQuantity { get; set; }
        public int? UsageLimit { get; set; } // جديد: حد أقصى للاستخدام

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public bool IsActive { get; set; } = true;

        [NotMapped]
        public string Status
        {
            get
            {
                if (!IsActive) return "Inactive";
                if (DateTime.Now < StartDate) return "Scheduled";
                if (DateTime.Now > EndDate) return "Expired";
                return "Active";
            }
        }
    }
}