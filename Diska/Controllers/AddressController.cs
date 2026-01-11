using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Diska.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;
using System.Linq;

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

        // 1. عرض قائمة العناوين (Index)
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var addresses = await _context.UserAddresses
                .Where(a => a.UserId == user.Id)
                .OrderByDescending(a => a.IsDefault)
                .ToListAsync();
            return View(addresses);
        }

        // 2. إنشاء عنوان جديد (Create)
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

                // منطق العنوان الافتراضي
                if (model.IsDefault || !_context.UserAddresses.Any(a => a.UserId == user.Id))
                {
                    var others = await _context.UserAddresses.Where(a => a.UserId == user.Id).ToListAsync();
                    others.ForEach(a => a.IsDefault = false);
                    model.IsDefault = true;
                }

                _context.UserAddresses.Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حفظ العنوان بنجاح";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        // 3. تعديل العنوان (Edit)
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var address = await _context.UserAddresses
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == user.Id);

            if (address == null) return NotFound();

            return View(address);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, UserAddress model)
        {
            if (id != model.Id) return NotFound();

            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);

                // التأكد من الملكية
                var existingAddress = await _context.UserAddresses.AsNoTracking()
                    .FirstOrDefaultAsync(a => a.Id == id && a.UserId == user.Id);

                if (existingAddress == null) return NotFound();

                model.UserId = user.Id; // الحفاظ على الـ UserId

                // تحديث الافتراضي
                if (model.IsDefault)
                {
                    var others = await _context.UserAddresses
                        .Where(a => a.UserId == user.Id && a.Id != id)
                        .ToListAsync();
                    others.ForEach(a => a.IsDefault = false);
                }
                // منع إلغاء الافتراضي إذا كان هو الوحيد أو الافتراضي الحالي
                else if (existingAddress.IsDefault)
                {
                    model.IsDefault = true; // إجبار البقاء كافتراضي حتى يتم تعيين غيره
                }

                _context.Update(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم تحديث العنوان بنجاح";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        // 4. حذف العنوان (Delete)
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var address = await _context.UserAddresses.FirstOrDefaultAsync(a => a.Id == id && a.UserId == user.Id);

            if (address != null)
            {
                _context.UserAddresses.Remove(address);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حذف العنوان";
            }
            return RedirectToAction(nameof(Index));
        }

        // 5. تعيين كافتراضي (SetDefault)
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
            TempData["Success"] = "تم تغيير العنوان الافتراضي";
            return RedirectToAction(nameof(Index));
        }
    }
}