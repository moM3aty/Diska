using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Diska.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting; // للإستضافة
using Microsoft.AspNetCore.Http; // للملفات
using System.IO; // للمسارات
using System;

namespace Diska.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class CategoriesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment; // إضافة بيئة الاستضافة

        public CategoriesController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // 1. Index (كما هو)
        public async Task<IActionResult> Index()
        {
            var categories = await _context.Categories
                .Include(c => c.Parent)
                .OrderBy(c => c.ParentId)
                .ThenBy(c => c.DisplayOrder)
                .ToListAsync();
            return View(categories);
        }

        // 2. Create (GET)
        [HttpGet]
        public IActionResult Create()
        {
            PrepareParentDropdown();
            return View();
        }

        // 3. Create (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Category model, IFormFile imageFile)
        {
            // إزالة التحقق من الحقول غير المطلوبة من المستخدم
            ModelState.Remove(nameof(model.Parent));
            ModelState.Remove(nameof(model.Children));
            ModelState.Remove(nameof(model.Products));
            ModelState.Remove("ImageUrl"); // سنتعامل معها يدوياً
            ModelState.Remove("MetaTitle");
            ModelState.Remove("Slug");
            ModelState.Remove("IconClass"); // لم نعد نحتاجها

            if (ModelState.IsValid)
            {
                // رفع الصورة
                if (imageFile != null)
                {
                    model.ImageUrl = await SaveImage(imageFile);
                }
                else
                {
                    model.ImageUrl = "images/default-category.png";
                }

                // تعيين أيقونة افتراضية لأن الحقل قد يكون مطلوباً في قاعدة البيانات
                model.IconClass = "fas fa-box";

                if (string.IsNullOrEmpty(model.Slug))
                    model.Slug = (model.NameEn ?? model.Name).ToLower().Replace(" ", "-");

                if (string.IsNullOrEmpty(model.MetaTitle)) model.MetaTitle = model.Name;
                if (string.IsNullOrEmpty(model.MetaDescription)) model.MetaDescription = model.Name;

                _context.Categories.Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم إضافة القسم بنجاح";
                return RedirectToAction(nameof(Index));
            }

            PrepareParentDropdown();
            return View(model);
        }

        // 4. Edit (GET)
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound();

            PrepareParentDropdown(category.ParentId, id);
            return View(category);
        }

        // 5. Edit (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Category model, IFormFile imageFile)
        {
            if (id != model.Id) return NotFound();

            ModelState.Remove(nameof(model.Parent));
            ModelState.Remove(nameof(model.Children));
            ModelState.Remove(nameof(model.Products));
            ModelState.Remove("ImageUrl");
            ModelState.Remove("MetaTitle");
            ModelState.Remove("IconClass");

            if (ModelState.IsValid)
            {
                try
                {
                    if (model.ParentId == model.Id)
                    {
                        ModelState.AddModelError("ParentId", "لا يمكن للقسم أن يكون أباً لنفسه");
                        PrepareParentDropdown(model.ParentId, id);
                        return View(model);
                    }

                    // الحفاظ على الصورة القديمة إذا لم يتم رفع جديدة
                    var existingImg = await _context.Categories.Where(c => c.Id == id).Select(c => c.ImageUrl).FirstOrDefaultAsync();

                    if (imageFile != null)
                    {
                        model.ImageUrl = await SaveImage(imageFile);
                    }
                    else
                    {
                        model.ImageUrl = existingImg;
                    }

                    // الحفاظ على الأيقونة القديمة أو تعيين افتراضي
                    if (string.IsNullOrEmpty(model.IconClass)) model.IconClass = "fas fa-box";

                    if (string.IsNullOrEmpty(model.Slug))
                        model.Slug = (model.NameEn ?? model.Name).ToLower().Replace(" ", "-");

                    if (string.IsNullOrEmpty(model.MetaTitle)) model.MetaTitle = model.Name;
                    if (string.IsNullOrEmpty(model.MetaDescription)) model.MetaDescription = model.Name;

                    _context.Update(model);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "تم تعديل القسم بنجاح";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Categories.Any(e => e.Id == id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }

            PrepareParentDropdown(model.ParentId, id);
            return View(model);
        }

        // 6. Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category != null)
            {
                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حذف القسم";
            }
            return RedirectToAction(nameof(Index));
        }

        // دالة مساعدة لحفظ الصور
        private async Task<string> SaveImage(IFormFile file)
        {
            string folder = "images/categories/";
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

        private void PrepareParentDropdown(int? selectedId = null, int? excludeId = null)
        {
            var query = _context.Categories.AsQueryable();

            if (excludeId.HasValue)
            {
                query = query.Where(c => c.Id != excludeId.Value);
            }

            var parents = query.OrderBy(c => c.Name).ToList();
            ViewBag.Parents = new SelectList(parents, "Id", "Name", selectedId);
        }
    }
}