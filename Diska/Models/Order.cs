using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Diska.Models
{
    public class Order
    {
        public int Id { get; set; }
        public DateTime OrderDate { get; set; } = DateTime.Now;

        public string UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; }

        public string CustomerName { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
        public string Governorate { get; set; }
        public string City { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ShippingCost { get; set; }

        public string Status { get; set; } = "Pending";
        public string PaymentMethod { get; set; }

        // --- إضافات جديدة من To-Do List ---
        public string DeliverySlot { get; set; } // صباحي / مسائي
        public string Notes { get; set; } // ملاحظات العميل

        public virtual ICollection<OrderItem> OrderItems { get; set; }
    }
}