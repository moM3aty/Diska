using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Diska.Models
{
    // نموذج سجل التدقيق لتتبع إجراءات الأدمن
    public class AuditLog
    {
        public int Id { get; set; }

        public string UserId { get; set; } // من قام بالفعل

        [Required]
        public string Action { get; set; } // Create, Update, Delete, Login, etc.

        [Required]
        public string EntityName { get; set; } // Product, Order, User...

        public string EntityId { get; set; } // ID العنصر المتأثر

        public string Details { get; set; } // وصف التفاصيل (مثلاً: تغيير السعر من 100 إلى 150)

        public string IpAddress { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}