using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Microsoft.EntityFrameworkCore;
using Diska.Models;
using Microsoft.AspNetCore.Authorization;
using Diska.Services; // For notifications

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

        public async Task<IActionResult> Index()
        {
            var requests = await _context.DealRequests
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();
            return View(requests);
        }

        [HttpPost]
        public async Task<IActionResult> Approve(int id)
        {
            var request = await _context.DealRequests.FindAsync(id);
            if (request != null)
            {
                request.Status = "Approved";
                await _context.SaveChangesAsync();

                // إشعار صاحب الطلب
                await _notificationService.NotifyUserAsync(request.UserId, "تمت الموافقة", $"تمت الموافقة على طلبك '{request.ProductName}' وهو متاح الآن للتجار.", "System", "/Request/Index");

                // إشعار التجار (اختياري - يمكن أن يسبب ضغطاً إذا كان عدد التجار كبيراً)
                // await _notificationService.NotifyMerchantsAsync("طلب شراء جديد", $"عميل يطلب {request.ProductName}، هل يمكنك توفيره؟", "/Request/Index");
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Reject(int id)
        {
            var request = await _context.DealRequests.FindAsync(id);
            if (request != null)
            {
                request.Status = "Rejected";
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var request = await _context.DealRequests.FindAsync(id);
            if (request != null)
            {
                _context.DealRequests.Remove(request);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}