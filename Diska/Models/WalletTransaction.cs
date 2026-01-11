using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Diska.Models
{
    public class WalletTransaction
    {
        public int Id { get; set; }

        public string UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; } 

        public string Description { get; set; } 

        public string Type { get; set; } 

        public DateTime TransactionDate { get; set; } = DateTime.Now;
    }
}