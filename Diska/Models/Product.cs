using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Diska.Models
{
    public class Product
    {
        public int Id { get; set; }

        // --- 1. Basic Info ---
        [Display(Name = "الاسم (عربي)")]
        [Required(ErrorMessage = "اسم المنتج مطلوب")]
        public string Name { get; set; }

        [Display(Name = "الاسم (إنجليزي)")]
        [Required(ErrorMessage = "اسم المنتج مطلوب")]
        public string NameEn { get; set; }

        public string Description { get; set; }
        public string DescriptionEn { get; set; }

        public string Brand { get; set; } // الماركة

        // --- 2. Identification ---
        public string SKU { get; set; } // رمز المخزون الفريد
        public string Barcode { get; set; } // الباركود الدولي

        // --- 3. Pricing ---
        [Column(TypeName = "decimal(18,2)")]
        [Required]
        public decimal Price { get; set; } // سعر البيع

        [Column(TypeName = "decimal(18,2)")]
        public decimal? OldPrice { get; set; } // السعر قبل الخصم

        [Column(TypeName = "decimal(18,2)")]
        public decimal CostPrice { get; set; } // سعر التكلفة (للتقارير الداخلية فقط)

        // --- 4. Inventory ---
        public int StockQuantity { get; set; }
        public int LowStockThreshold { get; set; } = 5; // حد التنبيه للنواقص
        public int UnitsPerCarton { get; set; } = 1;

        // --- 5. Status ---
        public string Status { get; set; } = "Active"; // Draft, Active, Archived
        public bool IsActive => Status == "Active"; // خاصية مساعدة للكود القديم

        // --- 6. Media ---
        public string ImageUrl { get; set; }
        public virtual List<ProductImage> Images { get; set; } = new List<ProductImage>();
        public string Color { get; set; }
        public virtual List<ProductColor> ProductColors { get; set; } = new List<ProductColor>();

        // --- 7. Shipping ---
        public decimal Weight { get; set; } // الوزن بالكيلو

        // --- 8. SEO ---
        public string Slug { get; set; }
        public string MetaTitle { get; set; }
        public string MetaDescription { get; set; }

        // --- 9. Dates ---
        [DataType(DataType.Date)]
        public DateTime? ProductionDate { get; set; }
        [DataType(DataType.Date)]
        public DateTime? ExpiryDate { get; set; }

        // --- Relationships ---
        public string MerchantId { get; set; }
        [ForeignKey("MerchantId")]
        public virtual ApplicationUser Merchant { get; set; }

        public int CategoryId { get; set; }
        [ForeignKey("CategoryId")]
        public virtual Category Category { get; set; }

        public virtual List<PriceTier> PriceTiers { get; set; } = new List<PriceTier>();

        [NotMapped]
        public bool IsExpired => ExpiryDate.HasValue && ExpiryDate.Value < DateTime.Now;
    }
}