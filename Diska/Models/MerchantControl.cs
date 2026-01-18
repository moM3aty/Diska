using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Diska.Models
{
    // 1. مصفوفة الصلاحيات لكل تاجر
    public class MerchantPermission
    {
        public int Id { get; set; }

        public string MerchantId { get; set; }
        [ForeignKey("MerchantId")]
        public virtual ApplicationUser Merchant { get; set; }

        public string Module { get; set; } // Products, Deals, Orders, Wallet

        // الصلاحيات التفصيلية
        public bool CanView { get; set; } = true;
        public bool CanCreate { get; set; } = false;
        public bool CanEdit { get; set; } = false;
        public bool CanDelete { get; set; } = false;
        public bool CanApprove { get; set; } = false; // غالباً false للتاجر
    }

    public class PendingMerchantAction
    {
        public int Id { get; set; }

        public string MerchantId { get; set; }
        [ForeignKey("MerchantId")]
        public virtual ApplicationUser Merchant { get; set; }

        public string ActionType { get; set; }
        public string EntityName { get; set; }
        public string EntityId { get; set; }

        public string OldValueJson { get; set; }
        public string NewValueJson { get; set; }

        public string Status { get; set; } = "Pending"; 

        public DateTime RequestDate { get; set; } = DateTime.Now;

        public DateTime? ProcessedDate { get; set; }

        public string ActionByAdminId { get; set; }
        public string AdminComment { get; set; }
    }
}