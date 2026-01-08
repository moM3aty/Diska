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
        private readonly Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> _userManager;

        public ProductsController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment, Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _userManager = userManager;
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
        public async Task<IActionResult> Create()
        {
            var merchants = await _userManager.GetUsersInRoleAsync("Merchant");
            ViewBag.Merchants = new SelectList(merchants, "Id", "ShopName");
            ViewBag.Categories = new SelectList(_context.Categories, "Id", "Name");
            return View(new Product { ProductionDate = DateTime.Today, ExpiryDate = DateTime.Today.AddMonths(6), UnitsPerCarton = 1 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product model, IFormFile mainImage, List<IFormFile> galleryImages, string[] productColorsHex, string[] productColorsName)
        {
            // تجاهل التحقق من الحقول التي لا تأتي من الفورم مباشرة
            ModelState.Remove("Merchant");
            ModelState.Remove("Category");
            ModelState.Remove("ImageUrl");

            if (!ModelState.IsValid)
            {
                var merchants = await _userManager.GetUsersInRoleAsync("Merchant");
                ViewBag.Merchants = new SelectList(merchants, "Id", "ShopName");
                ViewBag.Categories = new SelectList(_context.Categories, "Id", "Name");
                return View(model);
            }

            // 1. الصورة الرئيسية
            if (mainImage != null) model.ImageUrl = await SaveFile(mainImage);
            else model.ImageUrl = "images/default-product.png";

            model.IsActive = true;
            _context.Products.Add(model);
            await _context.SaveChangesAsync();

            // 2. صور المعرض
            if (galleryImages != null && galleryImages.Any())
            {
                foreach (var file in galleryImages)
                {
                    if (file.Length > 0)
                    {
                        string path = await SaveFile(file);
                        _context.ProductImages.Add(new ProductImage { ProductId = model.Id, ImageUrl = path });
                    }
                }
            }

            // 3. الألوان (تمت الإعادة)
            if (productColorsHex != null && productColorsHex.Length > 0)
            {
                for (int i = 0; i < productColorsHex.Length; i++)
                {
                    if (!string.IsNullOrEmpty(productColorsHex[i]))
                    {
                        _context.ProductColors.Add(new ProductColor
                        {
                            ProductId = model.Id,
                            ColorHex = productColorsHex[i],
                            ColorName = productColorsName?.ElementAtOrDefault(i) ?? "لون"
                        });
                    }
                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "تم إضافة المنتج بنجاح!";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var product = await _context.Products
                .Include(p => p.Images)
                .Include(p => p.ProductColors) // جلب الألوان
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null) return NotFound();

            var merchants = await _userManager.GetUsersInRoleAsync("Merchant");
            ViewBag.Merchants = new SelectList(merchants, "Id", "ShopName", product.MerchantId);
            ViewBag.Categories = new SelectList(_context.Categories, "Id", "Name", product.CategoryId);

            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Product model, IFormFile mainImage, List<IFormFile> galleryImages, string[] productColorsHex, string[] productColorsName)
        {
            if (id != model.Id) return NotFound();

            var productToUpdate = await _context.Products
                .Include(p => p.ProductColors) // تضمين الألوان للحذف
                .FirstOrDefaultAsync(p => p.Id == id);

            if (productToUpdate == null) return NotFound();

            // تحديث البيانات
            productToUpdate.Name = model.Name;
            productToUpdate.NameEn = model.NameEn;
            productToUpdate.Description = model.Description;
            productToUpdate.DescriptionEn = model.DescriptionEn;
            productToUpdate.Price = model.Price;
            productToUpdate.OldPrice = model.OldPrice;
            productToUpdate.StockQuantity = model.StockQuantity;
            productToUpdate.UnitsPerCarton = model.UnitsPerCarton;
            productToUpdate.CategoryId = model.CategoryId;
            productToUpdate.MerchantId = model.MerchantId;
            productToUpdate.IsActive = model.IsActive;
            productToUpdate.ProductionDate = model.ProductionDate;
            productToUpdate.ExpiryDate = model.ExpiryDate;

            // تحديث الصورة الرئيسية
            if (mainImage != null)
            {
                productToUpdate.ImageUrl = await SaveFile(mainImage);
            }

            // إضافة صور جديدة للمعرض
            if (galleryImages != null && galleryImages.Any())
            {
                foreach (var file in galleryImages)
                {
                    if (file.Length > 0)
                    {
                        string path = await SaveFile(file);
                        _context.ProductImages.Add(new ProductImage { ProductId = productToUpdate.Id, ImageUrl = path });
                    }
                }
            }

            // تحديث الألوان (حذف القديم وإضافة الجديد)
            _context.ProductColors.RemoveRange(productToUpdate.ProductColors);
            if (productColorsHex != null && productColorsHex.Length > 0)
            {
                for (int i = 0; i < productColorsHex.Length; i++)
                {
                    if (!string.IsNullOrEmpty(productColorsHex[i]))
                    {
                        _context.ProductColors.Add(new ProductColor
                        {
                            ProductId = productToUpdate.Id,
                            ColorHex = productColorsHex[i],
                            ColorName = productColorsName?.ElementAtOrDefault(i) ?? "لون"
                        });
                    }
                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "تم تعديل المنتج بنجاح!";
            return RedirectToAction(nameof(Index));
        }

        // حذف صورة
        [HttpPost]
        public async Task<IActionResult> DeleteImage(int id)
        {
            var img = await _context.ProductImages.FindAsync(id);
            if (img != null)
            {
                _context.ProductImages.Remove(img);
                await _context.SaveChangesAsync();
                return Ok();
            }
            return NotFound();
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حذف المنتج.";
            }
            return RedirectToAction(nameof(Index));
        }

        private async Task<string> SaveFile(IFormFile file)
        {
            string folder = "images/products/";
            string fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
            string serverPath = Path.Combine(_webHostEnvironment.WebRootPath, folder + fileName);

            string dirPath = Path.GetDirectoryName(serverPath);
            if (!Directory.Exists(dirPath)) Directory.CreateDirectory(dirPath);

            using (var stream = new FileStream(serverPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }
            return folder + fileName;
        }
    }
}