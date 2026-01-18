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

namespace Diska.Areas.Merchant.Controllers
{
    [Area("Merchant")]
    [Authorize(Roles = "Merchant")]
    public class RequestsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notificationService;

        public RequestsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, INotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _notificationService = notificationService;
        }

        // عرض الطلبات المتاحة (Marketplace)
        public async Task<IActionResult> Index()
        {
            // الطلبات المعتمدة من الإدارة فقط
            var requests = await _context.DealRequests
                .Where(r => r.Status == "Approved")
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            return View(requests);
        }

        // تفاصيل الطلب والمحادثة للتاجر
        public async Task<IActionResult> Details(int id)
        {
            var merchant = await _userManager.GetUserAsync(User);

            var request = await _context.DealRequests
                .Include(r => r.User) // بيانات العميل
                .Include(r => r.Offers)
                .Include(r => r.Messages).ThenInclude(m => m.Sender)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null) return NotFound();

            // التحقق هل قدم التاجر عرضاً؟
            var myOffer = request.Offers.FirstOrDefault(o => o.MerchantId == merchant.Id);
            ViewBag.MyOffer = myOffer;

            // إذا كان العرض مقبولاً، اعرض المحادثة
            if (myOffer != null && myOffer.IsAccepted)
            {
                request.Messages = request.Messages.OrderBy(m => m.CreatedAt).ToList();
            }
            else
            {
                // إخفاء الرسائل إذا لم يكن هو التاجر الفائز
                request.Messages = new System.Collections.Generic.List<RequestMessage>();
            }

            return View(request);
        }

        // تقديم عرض
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitOffer(int requestId, decimal price, string notes)
        {
            var merchant = await _userManager.GetUserAsync(User);
            var request = await _context.DealRequests.FindAsync(requestId);

            if (request == null || request.Status != "Approved") return NotFound();

            if (await _context.MerchantOffers.AnyAsync(o => o.DealRequestId == requestId && o.MerchantId == merchant.Id))
            {
                TempData["Error"] = "لقد قدمت عرضاً بالفعل.";
                return RedirectToAction(nameof(Details), new { id = requestId });
            }

            var offer = new MerchantOffer
            {
                DealRequestId = requestId,
                MerchantId = merchant.Id,
                OfferPrice = price,
                Notes = notes,
                CreatedAt = DateTime.Now,
                IsAccepted = false
            };

            _context.MerchantOffers.Add(offer);
            await _context.SaveChangesAsync();

            // إشعار العميل مع الرابط
            var link = Url.Action("Details", "Request", new { id = requestId });
            await _notificationService.NotifyUserAsync(request.UserId, "عرض سعر جديد", $"تلقيت عرضاً جديداً من {merchant.ShopName} بقيمة {price:N0} ج.م", "Request", link);

            TempData["Success"] = "تم تقديم العرض بنجاح.";
            return RedirectToAction(nameof(Details), new { id = requestId });
        }

        // إرسال رد من التاجر للعميل
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendReply(int requestId, string message)
        {
            var merchant = await _userManager.GetUserAsync(User);
            var request = await _context.DealRequests.FindAsync(requestId);

            if (request == null) return NotFound();
            if (string.IsNullOrWhiteSpace(message)) return RedirectToAction(nameof(Details), new { id = requestId });

            var msg = new RequestMessage
            {
                DealRequestId = requestId,
                SenderId = merchant.Id,
                Message = message,
                CreatedAt = DateTime.Now,
                IsAdmin = true // هنا IsAdmin = true تعني "الطرف الآخر" (التاجر) لتمييزه عن العميل في الواجهة
            };

            _context.RequestMessages.Add(msg);
            await _context.SaveChangesAsync();

            // إشعار العميل مع الرابط
            var link = Url.Action("Details", "Request", new { id = requestId });
            await _notificationService.NotifyUserAsync(request.UserId, "رسالة من التاجر", $"التاجر {merchant.ShopName} أرسل لك رسالة.", "Request", link);

            return RedirectToAction(nameof(Details), new { id = requestId });
        }
    }
}