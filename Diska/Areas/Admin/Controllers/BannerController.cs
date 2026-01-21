using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Diska.Services;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.IO;
using System;
using Diska.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Diska.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class BannerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public BannerController(ApplicationDbContext context, INotificationService notificationService, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _notificationService = notificationService;
            _webHostEnvironment = webHostEnvironment;
        }

        public async Task<IActionResult> Index(string status = "All")
        {
            var banners = _context.Banners
                .Include(b => b.Merchant)
                .AsQueryable();

            if (status != "All")
                banners = banners.Where(b => b.Status == status);

            ViewBag.CurrentStatus = status;
            return View(await banners.OrderByDescending(b => b.Priority).ThenByDescending(b => b.Id).ToListAsync());
        }

        [HttpGet]
        public IActionResult Create()
        {
            PrepareViewBags();
            return View(new Banner { StartDate = DateTime.Now, EndDate = DateTime.Now.AddDays(7), IsActive = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Banner banner, IFormFile desktopFile, IFormFile mobileFile)
        {
            ModelState.Remove("ImageMobile");
            ModelState.Remove("ImageDesktop");
            ModelState.Remove("Merchant");
            ModelState.Remove("MerchantId");
            ModelState.Remove("AdminComment");

            if (ModelState.IsValid)
            {
                banner.ApprovalStatus = "Approved";

                if (desktopFile != null) banner.ImageDesktop = await SaveFile(desktopFile);
                else banner.ImageDesktop = "images/default-banner.png";

                if (mobileFile != null) banner.ImageMobile = await SaveFile(mobileFile);
                else banner.ImageMobile = banner.ImageDesktop; 

                _context.Banners.Add(banner);
                await _context.SaveChangesAsync();

                TempData["Success"] = "تم إضافة البنر بنجاح";
                return RedirectToAction(nameof(Index));
            }

            PrepareViewBags();
            return View(banner);
        }

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
            ModelState.Remove("Merchant");
            ModelState.Remove("MerchantId");
            ModelState.Remove("AdminComment");
            ModelState.Remove("mobileFile");
            ModelState.Remove("desktopFile");

            if (ModelState.IsValid)
            {
                try
                {
                    var existing = await _context.Banners.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id);
                    if (existing == null) return NotFound();

                    banner.ImageDesktop = desktopFile != null ? await SaveFile(desktopFile) : existing.ImageDesktop;
                    banner.ImageMobile = mobileFile != null ? await SaveFile(mobileFile) : existing.ImageMobile;

                    banner.MerchantId = existing.MerchantId;
                    if (string.IsNullOrEmpty(banner.Status)) banner.ApprovalStatus = existing.Status;

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

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var banner = await _context.Banners.FindAsync(id);
            if (banner != null)
            {
                _context.Banners.Remove(banner);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حذف البنر";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Approve(int id)
        {
            var banner = await _context.Banners.FindAsync(id);
            if (banner == null) return NotFound();

            banner.ApprovalStatus = "Approved";
            banner.IsActive = true;
            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(banner.MerchantId))
            {
                await _notificationService.NotifyUserAsync(banner.MerchantId, "تمت الموافقة ✅", $"تمت الموافقة على إعلانك '{banner.Title}' ونشره.", "Banner");
            }

            TempData["Success"] = "تمت الموافقة على الإعلان.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Reject(int id, string reason)
        {
            var banner = await _context.Banners.FindAsync(id);
            if (banner == null) return NotFound();

            banner.ApprovalStatus = "Rejected";
            banner.IsActive = false;
            banner.AdminComment = reason;
            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(banner.MerchantId))
            {
                await _notificationService.NotifyUserAsync(banner.MerchantId, "تم الرفض ❌", $"تم رفض إعلانك '{banner.Title}'. السبب: {reason}", "Banner");
            }

            TempData["Success"] = "تم رفض الإعلان.";
            return RedirectToAction(nameof(Index));
        }

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
            ViewBag.Categories = new SelectList(_context.Categories, "Id", "Name");
            ViewBag.Products = new SelectList(_context.Products.Take(100), "Id", "Name");
        }
    }
}