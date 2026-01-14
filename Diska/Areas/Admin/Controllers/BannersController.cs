using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Diska.Data;
using Diska.Models;
using Microsoft.AspNetCore.Authorization;

namespace Diska.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class BannersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public BannersController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // --- عرض البنرات ---
        public async Task<IActionResult> Index()
        {
            var banners = await _context.Banners
                .OrderByDescending(b => b.Priority)
                .ThenByDescending(b => b.Id)
                .ToListAsync();
            return View(banners);
        }

        // --- إنشاء بنر جديد ---
        [HttpGet]
        public IActionResult Create()
        {
            PrepareViewBags();
            return View(new Banner { StartDate = DateTime.Now, EndDate = DateTime.Now.AddDays(7) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Banner banner, IFormFile desktopFile, IFormFile mobileFile)
        {
            ModelState.Remove("ImageMobile");
            ModelState.Remove("ImageDesktop");
            if (ModelState.IsValid)
            {
                // رفع الصور
                if (desktopFile != null) banner.ImageDesktop = await SaveFile(desktopFile);
                if (mobileFile != null) banner.ImageMobile = await SaveFile(mobileFile);
                else banner.ImageMobile = banner.ImageDesktop; // Fallback

                _context.Banners.Add(banner);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم إضافة البنر بنجاح";
                return RedirectToAction(nameof(Index));
            }

            PrepareViewBags();
            return View(banner);
        }

        // --- تعديل البنر ---
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var banner = await _context.Banners.FindAsync(id);
            if (banner == null) return NotFound();

            PrepareViewBags();
            return View(banner);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Banner banner, IFormFile desktopFile, IFormFile mobileFile)
        {
            if (id != banner.Id) return NotFound();
            ModelState.Remove("ImageMobile");
            ModelState.Remove("ImageDesktop");
            if (ModelState.IsValid)
            {
                try
                {
                    var existing = await _context.Banners.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id);

                    // الاحتفاظ بالصور القديمة إذا لم يتم رفع جديد
                    banner.ImageDesktop = desktopFile != null ? await SaveFile(desktopFile) : existing.ImageDesktop;
                    banner.ImageMobile = mobileFile != null ? await SaveFile(mobileFile) : existing.ImageMobile;

                    _context.Update(banner);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "تم تحديث البنر بنجاح";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Banners.Any(e => e.Id == id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            PrepareViewBags();
            return View(banner);
        }

        // --- حذف البنر ---
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var banner = await _context.Banners.FindAsync(id);
            if (banner != null)
            {
                // يمكن هنا إضافة كود لحذف الصور من السيرفر أيضاً
                _context.Banners.Remove(banner);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حذف البنر";
            }
            return RedirectToAction(nameof(Index));
        }

        // --- دوال مساعدة ---
        private async Task<string> SaveFile(IFormFile file)
        {
            string folder = "images/banners/";
            string fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
            string serverPath = Path.Combine(_webHostEnvironment.WebRootPath, folder + fileName);

            string dir = Path.GetDirectoryName(serverPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            using (var stream = new FileStream(serverPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }
            return folder + fileName;
        }

        private void PrepareViewBags()
        {
            // لتعبئة القوائم المنسدلة عند الربط
            ViewBag.Categories = new SelectList(_context.Categories, "Id", "Name");
            ViewBag.Products = new SelectList(_context.Products.Take(100), "Id", "Name"); // Limit for perf
            ViewBag.Deals = new SelectList(_context.GroupDeals.Where(d => d.IsActive), "Id", "Id"); // Or add a Title to Deal model
        }
    }
}