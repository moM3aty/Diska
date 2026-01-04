using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Diska.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Diska.Controllers
{
    public class RequestController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public RequestController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // عرض طلبات الشراء (سوق الطلبات)
        public async Task<IActionResult> Index()
        {
            var requests = await _context.DealRequests
                .Where(r => r.Status == "Pending" || r.Status == "Approved")
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            return View(requests);
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
                request.Status = "Pending"; // يتطلب موافقة الإدارة

                _context.DealRequests.Add(request);
                await _context.SaveChangesAsync();

                TempData["Message"] = "تم إرسال طلبك بنجاح وسيتم مراجعته من قبل الإدارة.";
                return RedirectToAction(nameof(Index));
            }

            TempData["Error"] = "برجاء التأكد من إدخال جميع البيانات المطلوبة.";
            return RedirectToAction(nameof(Index));
        }
    }
}