using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace Diska.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class RestockController : Controller
    {
        private readonly ApplicationDbContext _context;

        public RestockController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var requests = await _context.RestockSubscriptions
                .Include(r => r.Product)
                .Where(r => !r.IsNotified)
                .ToListAsync();

            return View(requests);
        }

        [HttpPost]
        public async Task<IActionResult> MarkAsNotified(int id)
        {
            var req = await _context.RestockSubscriptions.FindAsync(id);
            if (req != null)
            {
                req.IsNotified = true;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}