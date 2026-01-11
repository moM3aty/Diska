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

    // 2. جدول الطلبات المعلقة (Approval Workflow)
    // أي تعديل حساس (سعر، مخزون ضخم) يتم تخزينه هنا أولاً
    public class PendingMerchantAction
    {
        public int Id { get; set; }

        public string MerchantId { get; set; }
        [ForeignKey("MerchantId")]
        public virtual ApplicationUser Merchant { get; set; }

        public string ActionType { get; set; } // CreateProduct, UpdatePrice, RestockRequest
        public string EntityName { get; set; } // Product, Deal
        public string EntityId { get; set; }   // ID للعنصر المتأثر (إن وجد)

        public string OldValueJson { get; set; } // البيانات القديمة (للمقارنة)
        public string NewValueJson { get; set; } // البيانات الجديدة المقترحة

        public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected
        public string AdminComment { get; set; } // سبب الرفض أو ملاحظات

        public DateTime RequestDate { get; set; } = DateTime.Now;
        public DateTime? ActionDate { get; set; }

        public string ActionByAdminId { get; set; } // الأدمن الذي اتخذ القرار
    }
}