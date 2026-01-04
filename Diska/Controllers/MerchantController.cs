using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Diska.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Diska.Controllers
{
    [Authorize(Roles = "Merchant")]
    public class MerchantController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly UserManager<ApplicationUser> _userManager;

        public MerchantController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _userManager = userManager;
        }

        // Dashboard: Stats & Product List
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var products = await _context.Products
                .Include(p => p.Category)
                .Where(p => p.MerchantId == user.Id)
                .OrderByDescending(p => p.Id)
                .ToListAsync();

            // Merchant Stats
            ViewBag.TotalProducts = products.Count;
            ViewBag.LowStock = products.Count(p => p.StockQuantity < 10);
            ViewBag.TotalValue = products.Sum(p => p.Price * p.StockQuantity);

            return View(products);
        }

        // GET: Add/Edit Product
        [HttpGet]
        public async Task<IActionResult> ProductForm(int? id)
        {
            var user = await _userManager.GetUserAsync(User);
            ViewBag.Categories = new SelectList(_context.Categories, "Id", "Name");

            if (id == null)
            {
                return View(new Product { ProductionDate = DateTime.Today, ExpiryDate = DateTime.Today.AddMonths(6) });
            }

            var product = await _context.Products
                .Include(p => p.PriceTiers)
                .FirstOrDefaultAsync(p => p.Id == id && p.MerchantId == user.Id);

            if (product == null) return NotFound();

            return View(product);
        }

        // POST: Save Product (Add or Edit)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveProduct(Product model, IFormFile imageFile)
        {
            var user = await _userManager.GetUserAsync(User);

            // Validation: Production < Expiry
            if (model.ExpiryDate <= model.ProductionDate)
            {
                ModelState.AddModelError("ExpiryDate", "تاريخ الانتهاء يجب أن يكون بعد تاريخ الإنتاج.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Categories = new SelectList(_context.Categories, "Id", "Name");
                return View("ProductForm", model);
            }

            Product product;

            if (model.Id == 0)
            {
                // New Product
                product = new Product { MerchantId = user.Id };
                _context.Products.Add(product);
            }
            else
            {
                // Edit Existing
                product = await _context.Products
                    .Include(p => p.PriceTiers)
                    .FirstOrDefaultAsync(p => p.Id == model.Id && p.MerchantId == user.Id);

                if (product == null) return NotFound();
            }

            // Update Fields
            product.Name = model.Name;
            product.NameEn = model.NameEn;
            product.Price = model.Price;
            product.OldPrice = model.OldPrice;
            product.StockQuantity = model.StockQuantity;
            product.UnitsPerCarton = model.UnitsPerCarton;
            product.Description = model.Description;
            product.DescriptionEn = model.DescriptionEn;
            product.CategoryId = model.CategoryId;
            product.ProductionDate = model.ProductionDate;
            product.ExpiryDate = model.ExpiryDate;

            // Handle Image
            if (imageFile != null)
            {
                string folder = "images/products/";
                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
                string serverPath = Path.Combine(_webHostEnvironment.WebRootPath, folder + fileName);

                // Ensure directory exists
                string dirPath = Path.GetDirectoryName(serverPath);
                if (!Directory.Exists(dirPath)) Directory.CreateDirectory(dirPath);

                using (var stream = new FileStream(serverPath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(stream);
                }
                product.ImageUrl = folder + fileName;
            }
            else if (model.Id == 0)
            {
                product.ImageUrl = "images/default-product.png";
            }

            // Handle Price Tiers (Clear old, Add new)
            if (product.PriceTiers != null) _context.PriceTiers.RemoveRange(product.PriceTiers);

            if (model.PriceTiers != null && model.PriceTiers.Any())
            {
                foreach (var tier in model.PriceTiers)
                {
                    if (tier.MinQuantity > 0 && tier.UnitPrice > 0)
                    {
                        tier.Id = 0; // Reset ID for new insert
                        tier.ProductId = product.Id;
                        _context.PriceTiers.Add(tier);
                    }
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id && p.MerchantId == user.Id);

            if (product != null)
            {
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}