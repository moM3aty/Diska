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

        // 1. عرض قائمة طلبات العميل
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

        // 2. صفحة إنشاء طلب جديد (GET)
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        // 2. حفظ الطلب (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DealRequest request)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);

                request.UserId = user.Id;
                request.RequestDate = DateTime.Now;
                request.Status = "Pending"; // يذهب للمراجعة من الأدمن

                _context.DealRequests.Add(request);
                await _context.SaveChangesAsync();

                // إشعار للأدمن
                await _notificationService.NotifyAdminsAsync("طلب خاص جديد", $"العميل {user.FullName} أرسل طلب شراء خاص: {request.ProductName}");

                TempData["Success"] = "تم إرسال طلبك بنجاح، سيتم مراجعته قريباً.";
                return RedirectToAction(nameof(Index));
            }
            return View(request);
        }

        // 3. تفاصيل الطلب والعروض المقدمة عليه
        public async Task<IActionResult> Details(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var request = await _context.DealRequests
                .Include(r => r.Offers)
                .ThenInclude(o => o.Merchant)
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == user.Id);

            if (request == null) return NotFound();

            return View(request);
        }

        // قبول عرض من تاجر
        [HttpPost]
        public async Task<IActionResult> AcceptOffer(int offerId)
        {
            var offer = await _context.MerchantOffers
                .Include(o => o.DealRequest)
                .FirstOrDefaultAsync(o => o.Id == offerId);

            var user = await _userManager.GetUserAsync(User);

            // التأكد من الملكية والصلاحية
            if (offer == null || offer.DealRequest.UserId != user.Id) return Forbid();

            // قبول العرض وتحديث حالة الطلب
            offer.IsAccepted = true;
            offer.DealRequest.Status = "Completed"; // تم الاتفاق

            await _context.SaveChangesAsync();

            // إشعار التاجر
            await _notificationService.NotifyUserAsync(offer.MerchantId, "عرض مقبول", $"وافق العميل على عرضك لطلب {offer.DealRequest.ProductName}. يرجى البدء في التنفيذ.", "Order");

            TempData["Success"] = "تم قبول العرض! يرجى التواصل مع التاجر لإتمام الصفقة.";
            return RedirectToAction(nameof(Details), new { id = offer.DealRequestId });
        }

        // حذف الطلب (إذا كان مازال معلقاً أو مرفوضاً)
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