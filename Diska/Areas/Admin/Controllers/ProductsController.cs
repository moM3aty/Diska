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
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ProductsController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        public async Task<IActionResult> Index()
        {
            var products = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Merchant)
                .OrderByDescending(p => p.Id)
                .ToListAsync();
            return View(products);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            ViewBag.Categories = new SelectList(_context.Categories, "Id", "Name", product.CategoryId);
            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Product model, IFormFile imageFile)
        {
            if (id != model.Id) return NotFound();

            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            product.Name = model.Name;
            product.NameEn = model.NameEn;
            product.Price = model.Price;
            product.OldPrice = model.OldPrice;
            product.StockQuantity = model.StockQuantity;
            product.UnitsPerCarton = model.UnitsPerCarton;
            product.Description = model.Description;
            product.CategoryId = model.CategoryId;
            product.IsActive = model.IsActive;

            if (imageFile != null)
            {
                string folder = "images/products/";
                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
                string serverPath = Path.Combine(_webHostEnvironment.WebRootPath, folder + fileName);
                using (var stream = new FileStream(serverPath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(stream);
                }
                product.ImageUrl = folder + fileName;
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}