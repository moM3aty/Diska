using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Diska.Models;
using Microsoft.EntityFrameworkCore;
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

        public async Task<IActionResult> Index()
        {
            return View(await _context.Banners.OrderBy(b => b.DisplayOrder).ToListAsync());
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Banner banner, IFormFile imageFile)
        {
            if (imageFile != null)
            {
                string folder = "images/banners/";
                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
                string serverPath = Path.Combine(_webHostEnvironment.WebRootPath, folder + fileName);

                string dirPath = Path.GetDirectoryName(serverPath);
                if (!Directory.Exists(dirPath)) Directory.CreateDirectory(dirPath);

                using (var stream = new FileStream(serverPath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(stream);
                }
                banner.ImageUrl = folder + fileName;
            }
            else
            {
                banner.ImageUrl = "images/banner-img.png";
            }

            _context.Banners.Add(banner);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var banner = await _context.Banners.FindAsync(id);
            if (banner == null) return NotFound();
            return View(banner);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Banner banner, IFormFile imageFile)
        {
            if (id != banner.Id) return NotFound();

            var existingBanner = await _context.Banners.FindAsync(id);
            if (existingBanner == null) return NotFound();

            if (imageFile != null)
            {
                string folder = "images/banners/";
                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
                string serverPath = Path.Combine(_webHostEnvironment.WebRootPath, folder + fileName);

                using (var stream = new FileStream(serverPath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(stream);
                }
                existingBanner.ImageUrl = folder + fileName;
            }

            existingBanner.Title = banner.Title;
            existingBanner.Subtitle = banner.Subtitle;
            existingBanner.ButtonText = banner.ButtonText;
            existingBanner.LinkUrl = banner.LinkUrl;
            existingBanner.DisplayOrder = banner.DisplayOrder;
            existingBanner.IsActive = banner.IsActive;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var banner = await _context.Banners.FindAsync(id);
            if (banner != null)
            {
                _context.Banners.Remove(banner);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}