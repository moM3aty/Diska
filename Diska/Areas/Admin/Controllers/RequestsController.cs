using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Microsoft.EntityFrameworkCore;
using Diska.Models;
using Microsoft.AspNetCore.Authorization;
using Diska.Services;

namespace Diska.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class RequestsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;

        public RequestsController(ApplicationDbContext context, INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        // 1. القائمة الرئيسية
        public async Task<IActionResult> Index(string status = "All", string search = "")
        {
            var query = _context.DealRequests.Include(r => r.User).AsQueryable();

            // Filter by Status
            if (status != "All")
            {
                query = query.Where(r => r.Status == status);
            }

            // Search
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(r => r.ProductName.Contains(search) || r.User.FullName.Contains(search));
            }

            var requests = await query.OrderByDescending(r => r.RequestDate).ToListAsync();

            ViewBag.CurrentStatus = status;
            return View(requests);
        }

        // 2. تفاصيل الطلب وإدارته
        public async Task<IActionResult> Details(int id)
        {
            var request = await _context.DealRequests
                .Include(r => r.User)
                .Include(r => r.Offers)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null) return NotFound();

            return View(request);
        }

        // 3. تحديث الحالة (Workflow)
        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int id, string status, string notes)
        {
            var request = await _context.DealRequests.FindAsync(id);
            if (request == null) return NotFound();

            string oldStatus = request.Status;
            request.Status = status;
            request.UpdatedAt = DateTime.Now;

            if (!string.IsNullOrEmpty(notes))
            {
                request.AdminNotes = notes;
            }

            await _context.SaveChangesAsync();

            // إشعارات بناءً على الحالة
            if (oldStatus != status)
            {
                string title = "تحديث طلبك";
                string msg = "";
                string type = "Info";

                switch (status)
                {
                    case "InReview":
                        msg = $"طلبك لمنتج '{request.ProductName}' قيد المراجعة الآن.";
                        break;
                    case "Approved":
                        title = "مبروك! تمت الموافقة";
                        msg = $"تمت الموافقة على طلبك '{request.ProductName}'. سيتمكن التجار من تقديم عروضهم الآن.";
                        type = "Success";
                        break;
                    case "Rejected":
                        title = "عذراً";
                        msg = $"تم رفض طلبك لمنتج '{request.ProductName}'. راجع الملاحظات.";
                        type = "Alert";
                        break;
                }

                if (!string.IsNullOrEmpty(msg))
                {
                    await _notificationService.NotifyUserAsync(request.UserId, title, msg, type, $"/Request/Details/{id}");
                }
            }

            TempData["Success"] = "تم تحديث حالة الطلب بنجاح.";
            return RedirectToAction(nameof(Details), new { id = id });
        }

        // 4. حفظ الملاحظات فقط
        [HttpPost]
        public async Task<IActionResult> SaveNotes(int id, string notes)
        {
            var request = await _context.DealRequests.FindAsync(id);
            if (request != null)
            {
                request.AdminNotes = notes;
                request.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حفظ الملاحظات.";
            }
            return RedirectToAction(nameof(Details), new { id = id });
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var request = await _context.DealRequests.FindAsync(id);
            if (request != null)
            {
                _context.DealRequests.Remove(request);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حذف الطلب.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}