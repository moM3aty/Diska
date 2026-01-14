using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Diska.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Linq;
using System.Threading.Tasks;

namespace Diska.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class CategoriesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CategoriesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. Index
        public async Task<IActionResult> Index()
        {
            var categories = await _context.Categories
                .Include(c => c.Parent) // Corrected from ParentCategory
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
        public async Task<IActionResult> Create(Category model)
        {
            // Remove validation for navigation properties
            ModelState.Remove(nameof(model.Parent));
            ModelState.Remove(nameof(model.Children));
            ModelState.Remove(nameof(model.Products));

            if (ModelState.IsValid)
            {
                if (string.IsNullOrEmpty(model.IconClass)) model.IconClass = "fas fa-folder";

                // Fix: Set default ImageUrl to satisfy DB NOT NULL constraint
                // Since images are removed from UI, we store a placeholder path
                model.ImageUrl = "images/default-category.png";

                if (string.IsNullOrEmpty(model.Slug))
                    model.Slug = (model.NameEn ?? model.Name).ToLower().Replace(" ", "-");

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

            // Pass ID to exclude it from parent list (category cannot be its own parent)
            PrepareParentDropdown(category.ParentId, id);
            return View(category);
        }

        // 5. Edit (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Category model)
        {
            if (id != model.Id) return NotFound();

            // Remove validation for navigation properties
            ModelState.Remove(nameof(model.Parent));
            ModelState.Remove(nameof(model.Children));
            ModelState.Remove(nameof(model.Products));

            if (ModelState.IsValid)
            {
                try
                {
                    // Check that parent is not the category itself
                    if (model.ParentId == model.Id)
                    {
                        ModelState.AddModelError("ParentId", "لا يمكن للقسم أن يكون أباً لنفسه");
                        PrepareParentDropdown(model.ParentId, id);
                        return View(model);
                    }

                    // Fix: Ensure ImageUrl is not null during update
                    // We set it to placeholder if it comes null from the form
                    if (string.IsNullOrEmpty(model.ImageUrl))
                    {
                        model.ImageUrl = "images/default-category.png";
                    }

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
                // Note: Database cascade delete or SetNull behavior applies here for subcategories/products
                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حذف القسم";
            }
            return RedirectToAction(nameof(Index));
        }

        // Helper: Prepare Parent Category Dropdown
        private void PrepareParentDropdown(int? selectedId = null, int? excludeId = null)
        {
            var query = _context.Categories.AsQueryable();

            // Exclude current category when editing to prevent circular reference
            if (excludeId.HasValue)
            {
                query = query.Where(c => c.Id != excludeId.Value);
            }

            var parents = query.OrderBy(c => c.Name).ToList();

            // Add "Main Category" option (no parent)
            ViewBag.Parents = new SelectList(parents, "Id", "Name", selectedId);
        }
    }
}