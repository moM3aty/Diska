using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Diska.Models
{
    public class MerchantOffer
    {
        public int Id { get; set; }

        public int DealRequestId { get; set; }
        [ForeignKey("DealRequestId")]
        public virtual DealRequest DealRequest { get; set; }

        public string MerchantId { get; set; }
        [ForeignKey("MerchantId")]
        public virtual ApplicationUser Merchant { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal OfferPrice { get; set; } 

        public string Notes { get; set; }

        public bool IsAccepted { get; set; } = false; 

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}