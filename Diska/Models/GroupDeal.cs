using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Diska.Models
{
    public class GroupDeal
    {
        public int Id { get; set; }

        [Display(Name = "عنوان العرض (عربي)")]
        [Required(ErrorMessage = "العنوان بالعربية مطلوب")]
        public string Title { get; set; }

        [Display(Name = "عنوان العرض (إنجليزي)")]
        public string TitleEn { get; set; }

        public int? ProductId { get; set; }
        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; }

        public int? CategoryId { get; set; } 
        [ForeignKey("CategoryId")]
        public virtual Category Category { get; set; }

        public bool IsPercentage { get; set; } = false; 

        [Column(TypeName = "decimal(18,2)")]
        public decimal DealPrice { get; set; } 

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountValue { get; set; } 

        public int TargetQuantity { get; set; }
        public int ReservedQuantity { get; set; }
        public int? UsageLimit { get; set; } 

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
            set { }
        }
    }
}