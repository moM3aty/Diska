using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Diska.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Diska.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Diska.Controllers
{
    [Authorize]
    public class RequestController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notificationService;

        public RequestController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, INotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _notificationService = notificationService;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var requests = await _context.DealRequests
                .Include(r => r.Offers)
                .Where(r => r.UserId == user.Id)
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            return View(requests);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DealRequest request)
        {
            ModelState.Remove("AdminNotes");
            ModelState.Remove("User");
            ModelState.Remove("UserId");
            ModelState.Remove("Messages");
            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                request.UserId = user.Id;
                request.RequestDate = DateTime.Now;
                request.Status = "Pending";
                request.AdminNotes = string.Empty;

                _context.DealRequests.Add(request);
                await _context.SaveChangesAsync();

                // إشعار للأدمن مع الرابط
                var link = Url.Action("Details", "DealRequest", new { area = "Admin", id = request.Id });
                await _notificationService.NotifyAdminsAsync("طلب خاص جديد", $"العميل {user.FullName} أرسل طلب شراء خاص: {request.ProductName}", "Request");

                TempData["Success"] = "تم إرسال طلبك بنجاح، سيتم مراجعته قريباً.";
                return RedirectToAction(nameof(Index));
            }
            return View(request);
        }

        public async Task<IActionResult> Details(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var request = await _context.DealRequests
                .Include(r => r.Offers).ThenInclude(o => o.Merchant)
                .Include(r => r.Messages).ThenInclude(m => m.Sender)
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == user.Id);

            if (request == null) return NotFound();

            request.Messages = request.Messages.OrderBy(m => m.CreatedAt).ToList();
            return View(request);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage(int requestId, string message)
        {
            var user = await _userManager.GetUserAsync(User);
            var request = await _context.DealRequests
                .Include(r => r.Offers)
                .FirstOrDefaultAsync(r => r.Id == requestId && r.UserId == user.Id);

            if (request == null) return NotFound();
            if (string.IsNullOrWhiteSpace(message)) return RedirectToAction(nameof(Details), new { id = requestId });

            var msg = new RequestMessage
            {
                DealRequestId = requestId,
                SenderId = user.Id,
                Message = message,
                CreatedAt = DateTime.Now,
                IsAdmin = false // رسالة من العميل
            };

            _context.RequestMessages.Add(msg);
            await _context.SaveChangesAsync();

            // التوجيه حسب الحالة
            if (request.Status == "Completed")
            {
                // إذا تم القبول، الرسالة تذهب للتاجر الفائز
                var acceptedOffer = request.Offers.FirstOrDefault(o => o.IsAccepted);
                if (acceptedOffer != null)
                {
                    var link = Url.Action("Details", "Requests", new { area = "Merchant", id = requestId });
                    await _notificationService.NotifyUserAsync(acceptedOffer.MerchantId, "رسالة من العميل", $"العميل أرسل رسالة بخصوص الطلب #{requestId}", "Request", link);
                }
            }
            else
            {
                // للأدمن (تصحيح الخطأ هنا أيضاً بإزالة المعامل الرابع)
                var link = Url.Action("Details", "DealRequest", new { area = "Admin", id = requestId });
                await _notificationService.NotifyAdminsAsync("رسالة جديدة من عميل", $"العميل {user.FullName} أرسل استفساراً بخصوص الطلب #{requestId}", "Request");
            }

            return RedirectToAction(nameof(Details), new { id = requestId });
        }

        [HttpPost]
        public async Task<IActionResult> AcceptOffer(int offerId)
        {
            var offer = await _context.MerchantOffers
                .Include(o => o.DealRequest)
                .FirstOrDefaultAsync(o => o.Id == offerId);

            var user = await _userManager.GetUserAsync(User);

            if (offer == null || offer.DealRequest.UserId != user.Id) return Forbid();

            offer.IsAccepted = true;
            offer.DealRequest.Status = "Completed";

            await _context.SaveChangesAsync();

            // إشعار التاجر
            await _notificationService.NotifyUserAsync(offer.MerchantId, "عرض مقبول", $"وافق العميل على عرضك لطلب {offer.DealRequest.ProductName}. يرجى البدء في التنفيذ.", "Order");

            TempData["Success"] = "تم قبول العرض! يرجى التواصل مع التاجر لإتمام الصفقة.";
            return RedirectToAction(nameof(Details), new { id = offer.DealRequestId });
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var request = await _context.DealRequests.FirstOrDefaultAsync(r => r.Id == id && r.UserId == user.Id);

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