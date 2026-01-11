using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Diska.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class AuditController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AuditController(ApplicationDbContext context)
        {
            _context = context;
        }

        // عرض سجلات النظام
        public async Task<IActionResult> Index(string userId, string actionType, DateTime? fromDate, DateTime? toDate)
        {
            var query = _context.AuditLogs.AsQueryable();

            if (!string.IsNullOrEmpty(userId))
            {
                query = query.Where(l => l.UserId == userId);
            }

            if (!string.IsNullOrEmpty(actionType) && actionType != "All")
            {
                query = query.Where(l => l.Action == actionType);
            }

            if (fromDate.HasValue)
            {
                query = query.Where(l => l.Timestamp >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(l => l.Timestamp <= toDate.Value);
            }

            // عرض أحدث 100 عملية فقط لتحسين الأداء
            var logs = await query.OrderByDescending(l => l.Timestamp).Take(100).ToListAsync();

            // تجهيز القوائم للفلتر
            // ملاحظة: في التطبيق الحقيقي يفضل استخدام Join مع جدول المستخدمين لعرض الأسماء
            ViewBag.ActionTypes = new SelectList(new[] { "Create", "Update", "Delete", "Login", "Approve", "Reject" });

            return View(logs);
        }
    }
}