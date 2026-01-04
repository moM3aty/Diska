using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Diska.Models
{


    public class Category
    {
        public int Id { get; set; }
        [Required(ErrorMessage = "اسم القسم مطلوب")]
        public string Name { get; set; }
        public string IconClass { get; set; }
        public virtual ICollection<Product> Products { get; set; }
    }

    public class Product
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم المنتج مطلوب")]
        public string Name { get; set; }

        [Required(ErrorMessage = "السعر مطلوب")]
        [Range(0.1, 100000, ErrorMessage = "سعر غير صحيح")]
        public decimal Price { get; set; }
        public decimal? OldPrice { get; set; }

        [Required(ErrorMessage = "الكمية المتاحة مطلوبة")]
        public int StockQuantity { get; set; }
        public int UnitsPerCarton { get; set; }
        public string Description { get; set; }
        public string ImageUrl { get; set; }
        public DateTime? ProductionDate { get; set; }
        public DateTime? ExpiryDate { get; set; }

        public string MerchantId { get; set; }

        public int CategoryId { get; set; }
        [ForeignKey("CategoryId")]
        public virtual Category Category { get; set; }
    }

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
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; }
        public string ShopName { get; set; }
        public string CommercialRegister { get; set; } // السجل التجاري
        public string TaxCard { get; set; } // البطاقة الضريبية

        [Column(TypeName = "decimal(18,2)")]
        public decimal WalletBalance { get; set; } = 0;
        public bool IsVerifiedMerchant { get; set; } = false;
    }

    // شرائح الأسعار (جوهر تجارة الجملة)
    public class PriceTier
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int MinQuantity { get; set; } // من كمية
        public int MaxQuantity { get; set; } // إلى كمية

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; } // السعر للقطعة في هذه الشريحة

        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; }
    }

    // سجل حركات المحفظة
    public class WalletTransaction
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public decimal Amount { get; set; }
        public string Type { get; set; } // Deposit, Withdrawal, Refund, Purchase
        public string Description { get; set; }
        public DateTime Date { get; set; } = DateTime.Now;
    }
}