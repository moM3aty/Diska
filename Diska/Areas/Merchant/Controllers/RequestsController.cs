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
                .Where(r => r.Status == "Approved") // فقط الطلبات المعتمدة
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            return View(requests);
        }

        // تفاصيل الطلب لتقديم عرض
        public async Task<IActionResult> Details(int id)
        {
            var request = await _context.DealRequests.FindAsync(id);
            if (request == null || request.Status != "Approved") return NotFound();
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
            await _notificationService.NotifyUserAsync(request.UserId, "عرض جديد", $"التاجر {user.ShopName} قدم عرضاً على طلبك.", "Offer", $"/Request/Details/{requestId}");

            TempData["Success"] = "تم تقديم عرضك بنجاح.";
            return RedirectToAction(nameof(Index));
        }
    }
}