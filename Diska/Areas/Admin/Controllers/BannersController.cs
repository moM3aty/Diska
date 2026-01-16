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
            // إزالة التحقق من الصور لأننا سنتعامل معها يدوياً
            ModelState.Remove("ImageMobile");
            ModelState.Remove("ImageDesktop");

            if (ModelState.IsValid)
            {
                // رفع الصور
                if (desktopFile != null) banner.ImageDesktop = await SaveFile(desktopFile);
                else banner.ImageDesktop = "images/default-banner.png"; // صورة افتراضية في حال عدم الرفع

                if (mobileFile != null) banner.ImageMobile = await SaveFile(mobileFile);
                else banner.ImageMobile = banner.ImageDesktop; // استخدام صورة الديسكتوب للموبايل كبديل

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

            // نتجاهل التحقق من الصور لأنها قد تكون موجودة مسبقاً ولا نحتاج لرفعها مجدداً
            ModelState.Remove("ImageMobile");
            ModelState.Remove("ImageDesktop");
            ModelState.Remove("mobileFile");
            ModelState.Remove("desktopFile");

            if (ModelState.IsValid)
            {
                try
                {
                    // جلب البيانات القديمة (بدون تتبع لتجنب تعارض التحديث)
                    var existing = await _context.Banners.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id);

                    if (existing == null)
                    {
                        return NotFound();
                    }

                    // المنطق للحفاظ على الصور القديمة:
                    // إذا تم رفع ملف جديد (desktopFile != null)، نستخدمه.
                    // وإلا، نستخدم المسار القديم المحفوظ في قاعدة البيانات (existing.ImageDesktop).
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
                // خياري: حذف الصور الفعلية من السيرفر لتوفير المساحة
                // DeleteFile(banner.ImageDesktop);
                // DeleteFile(banner.ImageMobile);

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
            // لتعبئة القوائم المنسدلة عند الربط (للاقسام، المنتجات، الصفقات)
            ViewBag.Categories = new SelectList(_context.Categories, "Id", "Name");
            ViewBag.Products = new SelectList(_context.Products.Take(100), "Id", "Name");
            ViewBag.Deals = new SelectList(_context.GroupDeals.Where(d => d.IsActive), "Id", "Id");
        }
    }
}