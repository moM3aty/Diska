using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Diska.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Diska.Services;

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

        // عرض الطلبات المتاحة للتقديم عليها (التي وافقت عليها الإدارة)
        public async Task<IActionResult> Index()
        {
            var requests = await _context.DealRequests
                .Where(r => r.Status == "Approved") // فقط الطلبات المعتمدة من الإدارة
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            return View(requests);
        }

        // تفاصيل الطلب لتقديم عرض
        public async Task<IActionResult> Details(int id)
        {
            var request = await _context.DealRequests.FindAsync(id);
            // يجب أن يكون الطلب معتمد
            if (request == null || request.Status != "Approved") return NotFound();

            // التحقق مما إذا كان التاجر قد قدم عرضاً مسبقاً
            var user = await _userManager.GetUserAsync(User);
            var existingOffer = await _context.MerchantOffers
                .FirstOrDefaultAsync(o => o.DealRequestId == id && o.MerchantId == user.Id);

            ViewBag.ExistingOffer = existingOffer;

            return View(request);
        }

        // تقديم عرض سعر
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitOffer(int requestId, decimal price, string notes)
        {
            var user = await _userManager.GetUserAsync(User);
            var request = await _context.DealRequests.FindAsync(requestId);

            if (request == null || request.Status != "Approved") return NotFound();

            // التحقق من عدم التكرار
            if (await _context.MerchantOffers.AnyAsync(o => o.DealRequestId == requestId && o.MerchantId == user.Id))
            {
                TempData["Error"] = "لقد قمت بتقديم عرض لهذا الطلب مسبقاً.";
                return RedirectToAction(nameof(Details), new { id = requestId });
            }

            var offer = new MerchantOffer
            {
                DealRequestId = requestId,
                MerchantId = user.Id,
                OfferPrice = price,
                Notes = notes,
                CreatedAt = DateTime.Now,
                IsAccepted = false
            };

            _context.MerchantOffers.Add(offer);
            await _context.SaveChangesAsync();

            // إشعار العميل
            await _notificationService.NotifyUserAsync(request.UserId, "عرض سعر جديد", $"تلقيت عرضاً جديداً من {user.ShopName} بقيمة {price} ج.م", "Offer", $"/Request/Details/{requestId}");

            TempData["Success"] = "تم تقديم عرضك بنجاح.";
            return RedirectToAction(nameof(Index));
        }
    }
}