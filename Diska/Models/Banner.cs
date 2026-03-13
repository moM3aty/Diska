using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Diska.Models
{
    public class Banner
    {
        public int Id { get; set; }
        
        public string Title { get; set; }
        public string? TitleEn { get; set; }
        
        public string? Subtitle { get; set; }
        public string? SubtitleEn { get; set; }

        public string? ImageDesktop { get; set; }
        public string? ImageMobile { get; set; }

        public string? ButtonText { get; set; }
        public string? ButtonTextEn { get; set; }

        // ✅ تم إضافة ? للسماح بالقيم الفارغة (NULL)
        public string? LinkType { get; set; } // e.g., "Product", "Category", "External"
        public string? LinkId { get; set; }   

        public int Priority { get; set; } = 0;
        public bool IsActive { get; set; } = true;
        
        public DateTime StartDate { get; set; } = DateTime.Now;
        public DateTime EndDate { get; set; } = DateTime.Now.AddMonths(1);

        public string? MerchantId { get; set; }
        
        // ✅ إضافة خاصية التنقل للتاجر لكي يعمل الـ Include بشكل صحيح
        [ForeignKey("MerchantId")]
        public virtual ApplicationUser? Merchant { get; set; }

        public string? ApprovalStatus { get; set; } // "Pending", "Approved", "Rejected"
        public string? AdminComment { get; set; }
        [NotMapped]
        public string Status
        {
            get
            {
                if (!IsActive) return "Inactive";
                if (DateTime.Now < StartDate) return "Scheduled";
                if (DateTime.Now > EndDate) return "Expired";
                return "Active";
            }
        }
    }
}