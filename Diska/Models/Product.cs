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

        [Required(ErrorMessage = "اسم المنتج (إنجليزي) مطلوب")]
        [Display(Name = "اسم المنتج (إنجليزي)")]
        public string NameEn { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Required(ErrorMessage = "السعر مطلوب")]
        public decimal Price { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? OldPrice { get; set; }

        [Required(ErrorMessage = "الكمية المتاحة مطلوبة")]
        public int StockQuantity { get; set; }

        [Required(ErrorMessage = "وحدات الكرتونة مطلوبة")]
        public int UnitsPerCarton { get; set; }

        public string Description { get; set; }

        [Required(ErrorMessage = "الوصف (إنجليزي) مطلوب")]
        public string DescriptionEn { get; set; }

        public string ImageUrl { get; set; }

        public virtual List<ProductImage> Images { get; set; } = new List<ProductImage>();


        public virtual List<ProductColor> ProductColors { get; set; } = new List<ProductColor>();

        public string Color { get; set; }

        [DataType(DataType.Date)]
        public DateTime? ProductionDate { get; set; }
        [DataType(DataType.Date)]
        public DateTime? ExpiryDate { get; set; }

        public string MerchantId { get; set; }
        [ForeignKey("MerchantId")]
        public virtual ApplicationUser Merchant { get; set; }

        [Required(ErrorMessage = "القسم مطلوب")]
        public int CategoryId { get; set; }
        [ForeignKey("CategoryId")]
        public virtual Category Category { get; set; }

        public bool IsActive { get; set; } = true;
        public virtual List<PriceTier> PriceTiers { get; set; } = new List<PriceTier>();

        [NotMapped]
        public bool IsExpired => ExpiryDate.HasValue && ExpiryDate.Value < DateTime.Now;
    }
}