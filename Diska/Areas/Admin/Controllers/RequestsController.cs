using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Diska.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Diska.Services;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace Diska.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class DealRequestController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notificationService;

        public DealRequestController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, INotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _notificationService = notificationService;
        }

        // عرض قائمة الطلبات
        public async Task<IActionResult> Index()
        {
            var requests = await _context.DealRequests
                .Include(r => r.User)
                .Include(r => r.Offers)
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            return View(requests);
        }

        // عرض التفاصيل والمحادثة للأدمن
        public async Task<IActionResult> Details(int id)
        {
            var request = await _context.DealRequests
                .Include(r => r.User)
                .Include(r => r.Offers).ThenInclude(o => o.Merchant)
                .Include(r => r.Messages).ThenInclude(m => m.Sender)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null) return NotFound();

            request.Messages = request.Messages.OrderBy(m => m.CreatedAt).ToList();

            return View(request);
        }

        // إرسال رد من الأدمن
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendReply(int requestId, string message)
        {
            var user = await _userManager.GetUserAsync(User);
            var request = await _context.DealRequests.FindAsync(requestId);

            if (request == null) return NotFound();
            if (string.IsNullOrWhiteSpace(message)) return RedirectToAction(nameof(Details), new { id = requestId });

            var msg = new RequestMessage
            {
                DealRequestId = requestId,
                SenderId = user.Id,
                Message = message,
                CreatedAt = DateTime.Now,
                IsAdmin = true // هذه الرسالة من الإدارة
            };

            _context.RequestMessages.Add(msg);
            await _context.SaveChangesAsync();

            // إشعار العميل
            await _notificationService.NotifyUserAsync(request.UserId, "رد جديد من الإدارة", $"قامت الإدارة بالرد على طلبك #{requestId}.", "Request");

            return RedirectToAction(nameof(Details), new { id = requestId });
        }

        // تغيير حالة الطلب (موافقة/رفض)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeStatus(int id, string status)
        {
            var request = await _context.DealRequests.FindAsync(id);
            if (request == null) return NotFound();

            request.Status = status;
            await _context.SaveChangesAsync();

            // إشعار العميل بتحديث الحالة
            string msg = status == "Approved" ? "تمت الموافقة على طلبك وهو الآن متاح للتجار." : "عفواً، تم رفض طلبك.";
            await _notificationService.NotifyUserAsync(request.UserId, "تحديث حالة الطلب", msg, "Request");

            TempData["Success"] = "تم تحديث حالة الطلب بنجاح.";
            return RedirectToAction(nameof(Details), new { id = id });
        }
    }
}