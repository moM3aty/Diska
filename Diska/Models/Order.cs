using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Diska.Models
{
    public class Order
    {
        public int Id { get; set; }
        public DateTime OrderDate { get; set; } = DateTime.Now;

        // العلاقة مع المستخدم
        public string UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; } // تم إضافة هذا التعريف لحل الخطأ

        public string CustomerName { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
        public string Governorate { get; set; }
        public string City { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ShippingCost { get; set; } // تم إضافة مصاريف الشحن

        public string Status { get; set; } = "Pending";
        public string PaymentMethod { get; set; }

        public virtual ICollection<OrderItem> OrderItems { get; set; }
    }
}