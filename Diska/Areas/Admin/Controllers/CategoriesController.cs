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
    public class CategoriesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public CategoriesController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        public async Task<IActionResult> Index()
        {
            var categories = await _context.Categories
                .Include(c => c.Parent)
                .Include(c => c.Products)
                .OrderBy(c => c.ParentId) // Group roots then children
                .ThenBy(c => c.DisplayOrder)
                .ToListAsync();
            return View(categories);
        }

        [HttpGet]
        public IActionResult Create()
        {
            ViewBag.Parents = new SelectList(_context.Categories.Where(c => c.ParentId == null), "Id", "Name");
            return View(new Category { IsActive = true, DisplayOrder = 0 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Category category, IFormFile imageFile)
        {
            if (ModelState.IsValid)
            {
                // 1. Handle Image
                if (imageFile != null)
                {
                    category.ImageUrl = await SaveFile(imageFile);
                }

                // 2. Handle SEO Slug (Auto-generate if empty)
                if (string.IsNullOrEmpty(category.Slug))
                {
                    category.Slug = category.NameEn.ToLower().Replace(" ", "-");
                }

                _context.Categories.Add(category);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم إضافة القسم بنجاح";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Parents = new SelectList(_context.Categories.Where(c => c.ParentId == null), "Id", "Name", category.ParentId);
            return View(category);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound();

            // Exclude self from parents list to avoid circular reference
            ViewBag.Parents = new SelectList(_context.Categories.Where(c => c.ParentId == null && c.Id != id), "Id", "Name", category.ParentId);
            return View(category);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Category category, IFormFile imageFile)
        {
            if (id != category.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var existing = await _context.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);

                    // Keep old image if no new one uploaded
                    if (imageFile != null) category.ImageUrl = await SaveFile(imageFile);
                    else category.ImageUrl = existing.ImageUrl;

                    // Keep Slug logic
                    if (string.IsNullOrEmpty(category.Slug)) category.Slug = category.NameEn.ToLower().Replace(" ", "-");

                    _context.Update(category);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "تم تحديث القسم بنجاح";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Categories.Any(e => e.Id == id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Parents = new SelectList(_context.Categories.Where(c => c.ParentId == null && c.Id != id), "Id", "Name", category.ParentId);
            return View(category);
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var category = await _context.Categories.Include(c => c.Children).FirstOrDefaultAsync(c => c.Id == id);

            if (category != null)
            {
                // Prevent delete if has children
                if (category.Children != null && category.Children.Any())
                {
                    TempData["Error"] = "لا يمكن حذف قسم رئيسي يحتوي على أقسام فرعية.";
                    return RedirectToAction(nameof(Index));
                }

                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حذف القسم";
            }
            return RedirectToAction(nameof(Index));
        }

        private async Task<string> SaveFile(IFormFile file)
        {
            string folder = "images/categories/";
            string fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
            string serverPath = Path.Combine(_webHostEnvironment.WebRootPath, folder + fileName);
            string dir = Path.GetDirectoryName(serverPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            using (var stream = new FileStream(serverPath, FileMode.Create)) await file.CopyToAsync(stream);
            return folder + fileName;
        }
    }
}