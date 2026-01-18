using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Diska.Models
{
    public class RequestMessage
    {
        public int Id { get; set; }

        public int DealRequestId { get; set; }
        [ForeignKey("DealRequestId")]
        public virtual DealRequest DealRequest { get; set; }

        public string SenderId { get; set; }
        [ForeignKey("SenderId")]
        public virtual ApplicationUser Sender { get; set; }

        [Required]
        public string Message { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public bool IsAdmin { get; set; } // لتحديد هل المرسل هو الإدارة/التاجر أم العميل
    }
}