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

  
}
