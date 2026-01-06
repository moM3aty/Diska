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

        // --- المنتجات ---
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var products = await _context.Products
                .Include(p => p.Category)
                .Where(p => p.MerchantId == user.Id)
                .OrderByDescending(p => p.Id)
                .ToListAsync();

            // إحصائيات سريعة
            ViewBag.TotalProducts = products.Count;
            ViewBag.LowStock = products.Count(p => p.StockQuantity < 10);
            ViewBag.TotalValue = products.Sum(p => p.Price * p.StockQuantity);

            return View(products);
        }

        [HttpPost]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id && p.MerchantId == user.Id);

            if (product != null)
            {
                product.IsActive = !product.IsActive;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> ProductForm(int? id)
        {
            var user = await _userManager.GetUserAsync(User);
            ViewBag.Categories = new SelectList(_context.Categories, "Id", "Name");

            if (id == null) return View(new Product { ProductionDate = DateTime.Today, ExpiryDate = DateTime.Today.AddMonths(6) });

            var product = await _context.Products
                .Include(p => p.PriceTiers)
                .FirstOrDefaultAsync(p => p.Id == id && p.MerchantId == user.Id);

            if (product == null) return NotFound();
            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveProduct(Product model, IFormFile imageFile)
        {
            var user = await _userManager.GetUserAsync(User);

            if (model.ExpiryDate <= model.ProductionDate) ModelState.AddModelError("ExpiryDate", "تاريخ الانتهاء يجب أن يكون بعد تاريخ الإنتاج.");

            if (!ModelState.IsValid)
            {
                ViewBag.Categories = new SelectList(_context.Categories, "Id", "Name");
                return View("ProductForm", model);
            }

            Product product;
            if (model.Id == 0)
            {
                product = new Product { MerchantId = user.Id };
                _context.Products.Add(product);
            }
            else
            {
                product = await _context.Products.Include(p => p.PriceTiers).FirstOrDefaultAsync(p => p.Id == model.Id && p.MerchantId == user.Id);
                if (product == null) return NotFound();
            }

            // تحديث البيانات
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
            product.IsActive = model.IsActive;

            // الصورة
            if (imageFile != null)
            {
                string folder = "images/products/";
                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
                string serverPath = Path.Combine(_webHostEnvironment.WebRootPath, folder + fileName);

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

            // الشرائح
            if (product.PriceTiers != null) _context.PriceTiers.RemoveRange(product.PriceTiers);
            if (model.PriceTiers != null && model.PriceTiers.Any())
            {
                foreach (var tier in model.PriceTiers)
                {
                    if (tier.MinQuantity > 0 && tier.UnitPrice > 0)
                    {
                        tier.Id = 0;
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
            if (product != null) { _context.Products.Remove(product); await _context.SaveChangesAsync(); }
            return RedirectToAction(nameof(Index));
        }

        // --- عروض التاجر (My Offers) ---
        public async Task<IActionResult> MyOffers()
        {
            var user = await _userManager.GetUserAsync(User);
            var offers = await _context.MerchantOffers
                .Include(o => o.DealRequest)
                .Where(o => o.MerchantId == user.Id)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            return View(offers);
        }

        public async Task<IActionResult> RestockRequests()
        {
            var user = await _userManager.GetUserAsync(User);

            var requests = await _context.RestockSubscriptions
                .Include(r => r.Product)
                .Where(r => r.Product.MerchantId == user.Id && !r.IsNotified)
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            return View(requests);
        }

        [HttpPost]
        public async Task<IActionResult> MarkRestocked(int id)
        {
            var req = await _context.RestockSubscriptions.FindAsync(id);
            if (req != null)
            {
                req.IsNotified = true;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(RestockRequests));
        }
    }
}