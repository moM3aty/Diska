using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Diska.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Diska.Services;

namespace Diska.Controllers
{
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
            string userId = user?.Id;

            var requests = await _context.DealRequests
                .Include(r => r.Offers) // تضمين عدد العروض
                .Where(r => r.Status == "Approved" || r.Status == "Completed" || (userId != null && r.UserId == userId))
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            return View(requests);
        }

        // تفاصيل الطلب والعروض المقدمة عليه
        [Authorize]
        public async Task<IActionResult> Details(int id)
        {
            var request = await _context.DealRequests
                .Include(r => r.Offers)
                .ThenInclude(o => o.Merchant)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null) return NotFound();

            return View(request);
        }

        [HttpPost]
        [Authorize(Roles = "Merchant")]
        public async Task<IActionResult> SubmitOffer(int requestId, decimal price, string notes)
        {
            var user = await _userManager.GetUserAsync(User);
            var request = await _context.DealRequests.FindAsync(requestId);

            if (request == null || request.Status != "Approved") return NotFound();

            var offer = new MerchantOffer
            {
                DealRequestId = requestId,
                MerchantId = user.Id,
                OfferPrice = price,
                Notes = notes,
                CreatedAt = DateTime.Now
            };

            _context.MerchantOffers.Add(offer);
            await _context.SaveChangesAsync();

            // إشعار العميل
            await _notificationService.NotifyUserAsync(request.UserId, "عرض جديد", $"قام التاجر {user.ShopName} بتقديم عرض سعر على طلبك.", "Order", $"/Request/Details/{requestId}");

            TempData["Message"] = "تم تقديم العرض بنجاح!";
            return RedirectToAction("Details", new { id = requestId });
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> AcceptOffer(int offerId)
        {
            var offer = await _context.MerchantOffers.Include(o => o.DealRequest).FirstOrDefaultAsync(o => o.Id == offerId);
            var user = await _userManager.GetUserAsync(User);

            // التأكد أن المستخدم هو صاحب الطلب
            if (offer == null || offer.DealRequest.UserId != user.Id) return Forbid();

            // قبول العرض وتحديث حالة الطلب
            offer.IsAccepted = true;
            offer.DealRequest.Status = "Completed";

            await _context.SaveChangesAsync();

            // إشعار التاجر
            await _notificationService.NotifyUserAsync(offer.MerchantId, "تم قبول عرضك", $"وافق العميل على عرضك لطلب {offer.DealRequest.ProductName}. يرجى التواصل معه.", "Order", $"/Request/Details/{offer.DealRequestId}");

            TempData["Message"] = "تم قبول العرض! يرجى التواصل مع التاجر لإتمام الصفقة.";
            return RedirectToAction("Details", new { id = offer.DealRequestId });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DealRequest request)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                request.UserId = user.Id;
                request.RequestDate = DateTime.Now;
                request.Status = "Pending";
                _context.DealRequests.Add(request);
                await _context.SaveChangesAsync();
                TempData["Message"] = "تم إرسال طلبك بنجاح للمراجعة.";
                return RedirectToAction(nameof(Index));
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var request = await _context.DealRequests.FirstOrDefaultAsync(r => r.Id == id && r.UserId == user.Id);
            if (request != null) { _context.DealRequests.Remove(request); await _context.SaveChangesAsync(); }
            return RedirectToAction(nameof(Index));
        }
    }
}