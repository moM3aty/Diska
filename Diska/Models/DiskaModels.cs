using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace Diska.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string? FullName { get; set; }
        public string? ShopName { get; set; }
        public string? CommercialRegister { get; set; }
        public string? TaxCard { get; set; }
        public bool IsVerifiedMerchant { get; set; }
        public decimal WalletBalance { get; set; }
        public string? MerchantId { get; set; } 
        public string? UserType { get; set; } // "Merchant", "Customer", "Staff"
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsActive { get; set; } = true;

        public virtual ICollection<MerchantPermission>? Permissions { get; set; }
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
            [ForeignKey("OrderId")]
            public Order Order { get; set; }

            public int ProductId { get; set; }
            [ForeignKey("ProductId")]
            public Product Product { get; set; }

            public int Quantity { get; set; }

            [Column(TypeName = "decimal(18,2)")]
            public decimal UnitPrice { get; set; }

  
            public string SelectedColorName { get; set; }
            public string SelectedColorHex { get; set; }
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
