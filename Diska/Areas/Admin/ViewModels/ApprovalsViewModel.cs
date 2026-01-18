using System.Collections.Generic;
using Diska.Models;

namespace Diska.Areas.Admin.ViewModels
{
    public class ApprovalsViewModel
    {
        // التجار الجدد بانتظار التوثيق
        public List<ApplicationUser> NewMerchants { get; set; } = new List<ApplicationUser>();

        // المنتجات الجديدة بانتظار الموافقة
        public List<Product> PendingProducts { get; set; } = new List<Product>();

        // طلبات أخرى (تعديل سعر، سحب رصيد، إلخ)
        public List<PendingMerchantAction> OtherActions { get; set; } = new List<PendingMerchantAction>();

        public int TotalCount => NewMerchants.Count + PendingProducts.Count + OtherActions.Count;
    }
}