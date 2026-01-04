using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Diska.Models
{
    public class Product
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم المنتج مطلوب")]
        [Display(Name = "اسم المنتج (عربي)")]
        public string Name { get; set; }

        [Display(Name = "اسم المنتج (إنجليزي)")]
        public string NameEn { get; set; }

        [Required(ErrorMessage = "السعر الأساسي مطلوب")]
        [Column(TypeName = "decimal(18,2)")]
        [Range(0.01, double.MaxValue, ErrorMessage = "السعر يجب أن يكون أكبر من 0")]
        public decimal Price { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "السعر قبل الخصم")]
        public decimal? OldPrice { get; set; }

        [Required(ErrorMessage = "الكمية المتاحة مطلوبة")]
        [Range(0, int.MaxValue, ErrorMessage = "الكمية لا يمكن أن تكون سالبة")]
        public int StockQuantity { get; set; }

        [Required(ErrorMessage = "عدد العبوات داخل الكرتونة مطلوب")]
        [Range(1, int.MaxValue, ErrorMessage = "يجب أن يكون 1 على الأقل")]
        public int UnitsPerCarton { get; set; }

        [DataType(DataType.MultilineText)]
        public string Description { get; set; }
        public string DescriptionEn { get; set; }

        public string ImageUrl { get; set; }

        [DataType(DataType.Date)]
        [Required(ErrorMessage = "تاريخ الإنتاج مطلوب")]
        public DateTime? ProductionDate { get; set; }

        [DataType(DataType.Date)]
        [Required(ErrorMessage = "تاريخ الانتهاء مطلوب")]
        public DateTime? ExpiryDate { get; set; }

        // Foreign Keys
        public string MerchantId { get; set; }
        [ForeignKey("MerchantId")]
        public virtual ApplicationUser Merchant { get; set; }

        [Required(ErrorMessage = "اختيار القسم مطلوب")]
        public int CategoryId { get; set; }
        [ForeignKey("CategoryId")]
        public virtual Category Category { get; set; }

        // B2B Logic: Wholesale Price Tiers
        public virtual List<PriceTier> PriceTiers { get; set; } = new List<PriceTier>();

        [NotMapped]
        public bool IsExpired => ExpiryDate.HasValue && ExpiryDate.Value < DateTime.Now;

        [NotMapped]
        public bool IsOutOfStock => StockQuantity <= 0;
    }
}