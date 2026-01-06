using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Diska.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace Diska.Controllers
{
    [Authorize]
    public class AddressController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AddressController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // عرض قائمة العناوين
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var addresses = await _context.UserAddresses
                .Where(a => a.UserId == user.Id)
                .OrderByDescending(a => a.IsDefault)
                .ToListAsync();
            return View(addresses);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UserAddress model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                model.UserId = user.Id;

                // إذا كان هذا أول عنوان أو تم تعيينه كافتراضي، قم بإلغاء الافتراضي من الباقي
                if (model.IsDefault || !_context.UserAddresses.Any(a => a.UserId == user.Id))
                {
                    var others = await _context.UserAddresses.Where(a => a.UserId == user.Id).ToListAsync();
                    others.ForEach(a => a.IsDefault = false);
                    model.IsDefault = true;
                }

                _context.UserAddresses.Add(model);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var address = await _context.UserAddresses.FirstOrDefaultAsync(a => a.Id == id && a.UserId == user.Id);

            if (address != null)
            {
                _context.UserAddresses.Remove(address);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> SetDefault(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var addresses = await _context.UserAddresses.Where(a => a.UserId == user.Id).ToListAsync();

            foreach (var addr in addresses)
            {
                addr.IsDefault = (addr.Id == id);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}