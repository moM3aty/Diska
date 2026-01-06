using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Diska.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Diska.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class DealsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DealsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var deals = await _context.GroupDeals
                .Include(d => d.Product)
                .OrderByDescending(d => d.EndDate)
                .ToListAsync();
            return View(deals);
        }

        [HttpGet]
        public IActionResult Create()
        {
            ViewBag.Products = new SelectList(_context.Products, "Id", "Name");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(GroupDeal deal)
        {
            if (ModelState.IsValid)
            {
                deal.ReservedQuantity = 0;
                deal.IsActive = true;

                _context.GroupDeals.Add(deal);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Products = new SelectList(_context.Products, "Id", "Name");
            return View(deal);
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var deal = await _context.GroupDeals.FindAsync(id);
            if (deal != null)
            {
                _context.GroupDeals.Remove(deal);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}