using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Diska.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Diska.Services; // إضافة الـ Namespace للخدمات
using Microsoft.AspNetCore.Identity;
using System.Text; // هام للمستخدم

namespace Diska.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditService _auditService; // حقن خدمة التدقيق

        public ProductsController(
            ApplicationDbContext context,
            IWebHostEnvironment webHostEnvironment,
            UserManager<ApplicationUser> userManager,
            IAuditService auditService) // الحقن هنا
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _userManager = userManager;
            _auditService = auditService;
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
            await PrepareDropdowns();
            return View(new Product
            {
                Status = "Draft",
                ProductionDate = DateTime.Today,
                ExpiryDate = DateTime.Today.AddMonths(6),
                UnitsPerCarton = 1
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product model, IFormFile mainImage, List<IFormFile> galleryImages, string[] productColorsHex, string[] productColorsName)
        {
            // تنظيف التحقق للحقول الاختيارية أو التي تدار يدوياً
            ModelState.Remove("Merchant");
            ModelState.Remove("Category");
            ModelState.Remove("ImageUrl");
            foreach (var key in ModelState.Keys.Where(k => k.StartsWith("PriceTiers") || k.StartsWith("ProductColors"))) ModelState.Remove(key);

            if (!ModelState.IsValid)
            {
                await PrepareDropdowns();
                return View(model);
            }

            // معالجة الـ Slug و SEO
            if (string.IsNullOrEmpty(model.Slug)) model.Slug = model.NameEn.ToLower().Replace(" ", "-");
            if (string.IsNullOrEmpty(model.MetaTitle)) model.MetaTitle = model.Name;

            // حفظ الصورة
            if (mainImage != null) model.ImageUrl = await SaveFile(mainImage);
            else model.ImageUrl = "images/default-product.png";

            _context.Products.Add(model);
            await _context.SaveChangesAsync();

            // حفظ المعرض والألوان وشرائح الأسعار
            await ProcessSubItems(model, galleryImages, productColorsHex, productColorsName);

            var userId = _userManager.GetUserId(User);
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            await _auditService.LogAsync(userId, "Create", "Product", model.Id.ToString(), $"تم إضافة منتج جديد: {model.Name}", ip);

            TempData["Success"] = "تم إضافة المنتج بنجاح";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var product = await _context.Products
                .Include(p => p.Images)
                .Include(p => p.PriceTiers)
                .Include(p => p.ProductColors)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null) return NotFound();

            await PrepareDropdowns(product.MerchantId, product.CategoryId);
            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Product model, IFormFile mainImage, List<IFormFile> galleryImages, string[] productColorsHex, string[] productColorsName)
        {

            if (id != model.Id) return NotFound();

            var productToUpdate = await _context.Products.FirstOrDefaultAsync(p => p.Id == id);
           

            if (productToUpdate == null) return NotFound();

            // تحديث البيانات
            productToUpdate.Name = model.Name;
            productToUpdate.NameEn = model.NameEn;
            productToUpdate.Brand = model.Brand;
            productToUpdate.SKU = model.SKU;
            productToUpdate.Barcode = model.Barcode;
            productToUpdate.Price = model.Price;
            productToUpdate.OldPrice = model.OldPrice;
            productToUpdate.CostPrice = model.CostPrice;
            productToUpdate.StockQuantity = model.StockQuantity;
            productToUpdate.LowStockThreshold = model.LowStockThreshold;
            productToUpdate.UnitsPerCarton = model.UnitsPerCarton;
            productToUpdate.Weight = model.Weight;
            productToUpdate.Status = model.Status;
            productToUpdate.Description = model.Description;
            productToUpdate.DescriptionEn = model.DescriptionEn;
            productToUpdate.Slug = model.Slug;
            productToUpdate.MetaTitle = model.MetaTitle;
            productToUpdate.MetaDescription = model.MetaDescription;
            productToUpdate.CategoryId = model.CategoryId;
            productToUpdate.MerchantId = model.MerchantId;
            productToUpdate.ProductionDate = model.ProductionDate;
            productToUpdate.ExpiryDate = model.ExpiryDate;

            if (mainImage != null)
            {
                productToUpdate.ImageUrl = await SaveFile(mainImage);
            }

            // تحديث العناصر الفرعية
            await ProcessSubItems(productToUpdate, galleryImages, productColorsHex, productColorsName, model.PriceTiers, true);

            await _context.SaveChangesAsync();

            var userId = _userManager.GetUserId(User);
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            await _auditService.LogAsync(userId, "Update", "Product", id.ToString(), $"تم تعديل بيانات المنتج: {model.Name}. السعر الجديد: {model.Price}", ip);


            TempData["Success"] = "تم تحديث المنتج بنجاح";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                string prodName = product.Name;

                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
                var userId = _userManager.GetUserId(User);
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                await _auditService.LogAsync(userId, "Delete", "Product", id.ToString(), $"تم حذف المنتج: {prodName}", ip);
                TempData["Success"] = "تم حذف المنتج.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> ExportToCsv()
        {
            var products = await _context.Products.Include(p => p.Category).Include(p => p.Merchant).ToListAsync();
            var builder = new StringBuilder();
            builder.AppendLine("Id,Name,SKU,Price,Stock,Category,Merchant,Status");

            foreach (var p in products)
            {
                builder.AppendLine($"{p.Id},{p.Name},{p.SKU},{p.Price},{p.StockQuantity},{p.Category?.Name},{p.Merchant?.ShopName},{p.Status}");
            }

            return File(Encoding.UTF8.GetBytes(builder.ToString()), "text/csv", $"products_{DateTime.Now:yyyyMMdd}.csv");
        }

        // Helpers
        private async Task PrepareDropdowns(string selectedMerchant = null, int? selectedCategory = null)
        {
            var merchants = await _userManager.GetUsersInRoleAsync("Merchant");
            ViewBag.Merchants = new SelectList(merchants, "Id", "ShopName", selectedMerchant);
            ViewBag.Categories = new SelectList(_context.Categories, "Id", "Name", selectedCategory);
        }

        private async Task ProcessSubItems(Product product, List<IFormFile> gallery, string[] colorsHex, string[] colorsName, List<PriceTier> priceTiers = null, bool isEdit = false)
        {
            // Gallery
            if (gallery != null)
            {
                foreach (var file in gallery)
                {
                    if (file.Length > 0)
                    {
                        string path = await SaveFile(file);
                        _context.ProductImages.Add(new ProductImage { ProductId = product.Id, ImageUrl = path });
                    }
                }
            }

            // Colors
            if (isEdit)
            {
                var oldColors = await _context.ProductColors.Where(c => c.ProductId == product.Id).ToListAsync();
                _context.ProductColors.RemoveRange(oldColors);

                var oldTiers = await _context.PriceTiers.Where(t => t.ProductId == product.Id).ToListAsync();
                _context.PriceTiers.RemoveRange(oldTiers);
            }

            if (colorsHex != null)
            {
                for (int i = 0; i < colorsHex.Length; i++)
                {
                    if (!string.IsNullOrEmpty(colorsHex[i]))
                    {
                        _context.ProductColors.Add(new ProductColor
                        {
                            ProductId = product.Id,
                            ColorHex = colorsHex[i],
                            ColorName = (colorsName != null && colorsName.Length > i) ? colorsName[i] : "Color"
                        });
                    }
                }
            }

            // Price Tiers (B2B)
            if (priceTiers != null)
            {
                foreach (var tier in priceTiers)
                {
                    if (tier.MinQuantity > 0 && tier.UnitPrice > 0)
                    {
                        tier.ProductId = product.Id;
                        _context.PriceTiers.Add(tier);
                    }
                }
            }
        }

        private async Task<string> SaveFile(IFormFile file)
        {
            string folder = "images/products/";
            string fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
            string serverPath = Path.Combine(_webHostEnvironment.WebRootPath, folder + fileName);
            string dir = Path.GetDirectoryName(serverPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            using (var stream = new FileStream(serverPath, FileMode.Create)) await file.CopyToAsync(stream);
            return folder + fileName;
        }
    }
}