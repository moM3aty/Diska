using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Diska.Models
{
   

    // 2. نموذج تنبيهات المخزون
    public class RestockSubscription
    {
        public int Id { get; set; }

        public int ProductId { get; set; }
        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; }

        public string? UserId { get; set; }
        public string Email { get; set; } // للتواصل

        public DateTime RequestDate { get; set; } = DateTime.Now;
        public bool IsNotified { get; set; } = false;
    }
}