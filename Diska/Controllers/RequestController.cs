using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Diska.Models;

namespace Diska.Controllers
{
    public class RequestController : Controller
    {
        private readonly ApplicationDbContext _context;

        public RequestController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            // جلب الطلبات الأحدث أولاً
            var requests = _context.DealRequests
                .Where(r => r.Status == "Pending" || r.Status == "Approved")
                .OrderByDescending(r => r.RequestDate)
                .ToList();

            return View(requests);
        }

        [HttpPost]
        public IActionResult Create(DealRequest request)
        {
            if (ModelState.IsValid)
            {
                request.RequestDate = DateTime.Now;
                request.Status = "Pending";
                _context.DealRequests.Add(request);
                _context.SaveChanges();
                return RedirectToAction(nameof(Index));
            }
            return RedirectToAction(nameof(Index));
        }
    }
}