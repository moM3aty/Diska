using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Diska.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Diska.Services;

namespace Diska.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class MerchantAccessController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditService _auditService;

        public MerchantAccessController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IAuditService auditService)
        {
            _context = context;
            _userManager = userManager;
            _auditService = auditService;
        }

        // عرض صفحة الصلاحيات لتاجر معين
        public async Task<IActionResult> Permissions(string merchantId)
        {
            var merchant = await _userManager.FindByIdAsync(merchantId);
            if (merchant == null) return NotFound();

            var permissions = await _context.MerchantPermissions
                .Where(p => p.MerchantId == merchantId)
                .ToListAsync();

            // تعريف الموديولات الأساسية للنظام
            var modules = new List <string> { "Products", "Deals", "Orders", "Wallet", "Reviews" }
            ;

            // التأكد من وجود سجل لكل موديول
            foreach (var mod in modules)
            {
                if (!permissions.Any(p => p.Module == mod))
                {
                    permissions.Add(new MerchantPermission
                    {
                        MerchantId = merchantId,
                        Module = mod,
                        CanView = true, // افتراضي
                        CanCreate = false,
                        CanEdit = false,
                        CanDelete = false
                    });
                }
            }

            ViewBag.MerchantName = merchant.ShopName ?? merchant.FullName;
            ViewBag.MerchantId = merchantId;

            return View(permissions);
        }

        // حفظ المصفوفة
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SavePermissions(string merchantId, List<MerchantPermission> model)
        {
            // حذف القديم
            var oldPerms = _context.MerchantPermissions.Where(p => p.MerchantId == merchantId);
            _context.MerchantPermissions.RemoveRange(oldPerms);

            // إضافة الجديد
            foreach (var item in model)
            {
                item.Id = 0; // Reset ID to insert new
                item.MerchantId = merchantId;
                _context.MerchantPermissions.Add(item);
            }

            await _context.SaveChangesAsync();

            // تسجيل التدقيق
            var adminId = _userManager.GetUserId(User);
            await _auditService.LogAsync(adminId, "Update Permissions", "User", merchantId, "تحديث مصفوفة الصلاحيات للتاجر", HttpContext.Connection.RemoteIpAddress?.ToString());

            TempData["Success"] = "تم تحديث الصلاحيات بنجاح.";
            return RedirectToAction("Index", "Dashboard", new { area = "Admin" }); // أو العودة لقائمة التجار
        }
    }
}