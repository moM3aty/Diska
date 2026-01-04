using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace Diska.Models
{
    // 1. توسيع هوية المستخدم (للتاجر والعميل)
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; }
        public string ShopName { get; set; }
        public string CommercialRegister { get; set; } // رقم السجل التجاري
        public string TaxCard { get; set; } // رقم البطاقة الضريبية

        [Column(TypeName = "decimal(18,2)")]
        public decimal WalletBalance { get; set; } = 0; // رصيد المحفظة

        public bool IsVerifiedMerchant { get; set; } = false; // هل تم تفعيل حساب التاجر؟
    }

    // 2. التصنيفات
    public class Category
    {
        public int Id { get; set; }
        [Required(ErrorMessage = "اسم القسم مطلوب")]
        public string Name { get; set; }
        public string NameEn { get; set; }
        public string IconClass { get; set; }
        public virtual ICollection<Product> Products { get; set; }
    }

    // 3. المنتجات (مع دعم شرائح الأسعار)
    public class Product
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم المنتج مطلوب")]
        public string Name { get; set; }
        public string NameEn { get; set; }

        [Required(ErrorMessage = "السعر مطلوب")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; } // سعر القطاعي (الأساسي)

        [Column(TypeName = "decimal(18,2)")]
        public decimal? OldPrice { get; set; }

        [Required(ErrorMessage = "الكمية المتاحة مطلوبة")]
        public int StockQuantity { get; set; }
        public int UnitsPerCarton { get; set; }

        public string Description { get; set; }
        public string DescriptionEn { get; set; }

        public string ImageUrl { get; set; }
        public DateTime? ProductionDate { get; set; }
        public DateTime? ExpiryDate { get; set; }

        public string MerchantId { get; set; }

        public int CategoryId { get; set; }
        [ForeignKey("CategoryId")]
        public virtual Category Category { get; set; }

        // العلاقة مع شرائح الأسعار
        public virtual List<PriceTier> PriceTiers { get; set; } = new List<PriceTier>();
    }

    // 4. شرائح الأسعار (للجملة)
    public class PriceTier
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int MinQuantity { get; set; } // من 5
        public int MaxQuantity { get; set; } // إلى 10

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; } // السعر: 180ج

        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; }
    }

    // 5. باقي الموديلات (بدون تغييرات جوهرية)
    public class WishlistItem
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public int ProductId { get; set; }
        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; }
    }

    public class GroupDeal
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; }
        public decimal DealPrice { get; set; }
        public int TargetQuantity { get; set; }
        public int ReservedQuantity { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class DealRequest
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string ProductName { get; set; }
        public int TargetQuantity { get; set; }
        public decimal DealPrice { get; set; }
        public string Location { get; set; }
        public string Description { get; set; }
        public string Status { get; set; } = "Pending";
        public DateTime RequestDate { get; set; } = DateTime.Now;
    }

    public class Order
    {
        public int Id { get; set; }
        public DateTime OrderDate { get; set; } = DateTime.Now;
        public string UserId { get; set; }
        public string CustomerName { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
        public string Governorate { get; set; }
        public string City { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        public string Status { get; set; } = "Pending";
        public string PaymentMethod { get; set; }
        public virtual ICollection<OrderItem> OrderItems { get; set; }
    }

    public class OrderItem
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [ForeignKey("OrderId")]
        public virtual Order Order { get; set; }
        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; }
    }

    public class ContactMessage
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string Subject { get; set; }
        public string Message { get; set; }
        public DateTime DateSent { get; set; } = DateTime.Now;
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
        public string Type { get; set; } = "Info";
        public string Link { get; set; }
        public bool IsRead { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string TimeAgo
        {
            get
            {
                var span = DateTime.Now - CreatedAt;
                if (span.TotalMinutes < 60) return $"منذ {Math.Ceiling(span.TotalMinutes)} دقيقة";
                if (span.TotalHours < 24) return $"منذ {Math.Ceiling(span.TotalHours)} ساعة";
                return CreatedAt.ToString("dd/MM/yyyy");
            }
        }
    }
}