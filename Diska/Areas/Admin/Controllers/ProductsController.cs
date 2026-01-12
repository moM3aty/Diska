using Diska.Data;
using Diska.Models;
using Diska.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diska.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditService _auditService;

        public ProductsController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment, UserManager<ApplicationUser> userManager, IAuditService auditService)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _userManager = userManager;
            _auditService = auditService;
        }

        // 1. Index
        public async Task<IActionResult> Index(string search, int? categoryId, string merchantId, string status, int page = 1)
        {
            int pageSize = 10;
            var query = _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.Merchant)
                .AsQueryable();

            // الفلاتر
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(p => p.Name.Contains(search) || p.NameEn.Contains(search) || p.SKU.Contains(search));
            }
            if (categoryId.HasValue)
            {
                query = query.Where(p => p.CategoryId == categoryId);
            }
            if (!string.IsNullOrEmpty(merchantId))
            {
                query = query.Where(p => p.MerchantId == merchantId);
            }
            if (!string.IsNullOrEmpty(status))
            {
                if (status == "LowStock") query = query.Where(p => p.StockQuantity <= p.LowStockThreshold);
                else if (status == "OutOfStock") query = query.Where(p => p.StockQuantity == 0);
                else query = query.Where(p => p.Status == status);
            }

            // الترتيب والتقسيم
            int totalItems = await query.CountAsync();
            var products = await query
                .OrderByDescending(p => p.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // تمرير البيانات
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            ViewBag.TotalItems = totalItems;

            // الاحتفاظ بحالة الفلاتر
            ViewBag.Search = search;
            ViewBag.FilterCategoryId = categoryId;
            ViewBag.FilterMerchantId = merchantId;
            ViewBag.FilterStatus = status;

            // تعبئة القوائم
            await PrepareDropdownsAsync(categoryId, merchantId);

            return View(products);
        }
        // 2. Create (GET)
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await PrepareDropdownsAsync();
            return View(new Product { Color = "#000000", Status = "Active", StockQuantity = 1, UnitsPerCarton = 1 });
        }

        // 3. Create (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product model, IFormFile mainImage, List<IFormFile> galleryImages, string[] productColorsHex, string[] productColorsName, string[] productColorsQty)
        {
            ModelState.Remove(nameof(model.Merchant));
            ModelState.Remove(nameof(model.Category));
            ModelState.Remove(nameof(model.ImageUrl));
            ModelState.Remove("Color");
            foreach (var key in ModelState.Keys.Where(k => k.Contains("PriceTiers") || k.Contains("ProductColors") || k.Contains("Images")))
                ModelState.Remove(key);

            if (!ModelState.IsValid)
            {
                await PrepareDropdownsAsync();
                return View(model);
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (string.IsNullOrEmpty(model.Slug))
                    model.Slug = (model.NameEn ?? model.Name).ToLower().Replace(" ", "-") + "-" + Guid.NewGuid().ToString().Substring(0, 4);

                if (string.IsNullOrEmpty(model.MetaTitle)) model.MetaTitle = model.Name;
                if (string.IsNullOrEmpty(model.Color)) model.Color = "#000000";
                if (string.IsNullOrEmpty(model.Barcode)) model.Barcode = "GEN-" + DateTime.Now.Ticks.ToString().Substring(8);

                model.ImageUrl = mainImage != null ? await SaveFile(mainImage) : "images/default-product.png";

                _context.Products.Add(model);
                await _context.SaveChangesAsync();

                await ProcessSubItems(model.Id, galleryImages, productColorsHex, productColorsName, productColorsQty);
                await _context.SaveChangesAsync();

                var userId = _userManager.GetUserId(User);
                await _auditService.LogAsync(userId, "Create", "Product", model.Id.ToString(), $"إضافة منتج: {model.Name}", HttpContext.Connection.RemoteIpAddress?.ToString());

                await transaction.CommitAsync();

                TempData["Success"] = "تم إضافة المنتج بنجاح";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                if (model.ImageUrl != null && !model.ImageUrl.Contains("default")) DeleteFile(model.ImageUrl);
                ModelState.AddModelError("", "حدث خطأ: " + ex.Message);
                await PrepareDropdownsAsync();
                return View(model);
            }
        }

        // 4. Edit (GET)
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var product = await _context.Products
                .Include(p => p.Images)
                .Include(p => p.ProductColors)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null) return NotFound();

            await PrepareDropdownsAsync(product.CategoryId, product.MerchantId);
            return View(product);
        }

        // 5. Edit (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Product model, IFormFile mainImage, List<IFormFile> galleryImages, string[] productColorsHex, string[] productColorsName, string[] productColorsQty, int[] existingImages)
        {
            if (id != model.Id) return NotFound();

            ModelState.Remove(nameof(model.Merchant));
            ModelState.Remove(nameof(model.Category));
            ModelState.Remove(nameof(model.ImageUrl));
            ModelState.Remove("mainImage");
            ModelState.Remove("Color");

            foreach (var key in ModelState.Keys.Where(k => k.Contains("PriceTiers") || k.Contains("ProductColors") || k.Contains("Images")))
                ModelState.Remove(key);

            if (!ModelState.IsValid)
            {
                await PrepareDropdownsAsync(model.CategoryId, model.MerchantId);
                return View(model);
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var existingProduct = await _context.Products
                    .Include(p => p.Images)
                    .Include(p => p.ProductColors)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (existingProduct == null) return NotFound();

                // تحديث الحقول
                existingProduct.Name = model.Name;
                existingProduct.NameEn = model.NameEn;
                existingProduct.Description = model.Description;
                existingProduct.DescriptionEn = model.DescriptionEn;
                existingProduct.Price = model.Price;
                existingProduct.OldPrice = model.OldPrice;
                existingProduct.CostPrice = model.CostPrice;
                existingProduct.StockQuantity = model.StockQuantity;
                existingProduct.CategoryId = model.CategoryId;
                existingProduct.MerchantId = model.MerchantId;
                existingProduct.Status = model.Status;
                existingProduct.SKU = model.SKU;
                existingProduct.Barcode = model.Barcode ?? existingProduct.Barcode;
                existingProduct.Color = model.Color ?? "#000000";
                existingProduct.Weight = model.Weight;
                existingProduct.Slug = model.Slug;
                existingProduct.MetaTitle = model.MetaTitle;
                existingProduct.MetaDescription = model.MetaDescription;
                existingProduct.UnitsPerCarton = model.UnitsPerCarton;
                existingProduct.LowStockThreshold = model.LowStockThreshold;

                // تحديث الماركة (Brand) إذا لم تكن موجودة
                existingProduct.Brand = model.Brand;

                if (mainImage != null)
                {
                    DeleteFile(existingProduct.ImageUrl);
                    existingProduct.ImageUrl = await SaveFile(mainImage);
                }

                // إدارة الصور
                if (existingImages != null)
                {
                    var imagesToDelete = existingProduct.Images.Where(img => !existingImages.Contains(img.Id)).ToList();
                    foreach (var img in imagesToDelete)
                    {
                        DeleteFile(img.ImageUrl);
                        _context.ProductImages.Remove(img);
                    }
                }

                // إدارة الألوان
                _context.ProductColors.RemoveRange(existingProduct.ProductColors);

                await ProcessSubItems(existingProduct.Id, galleryImages, productColorsHex, productColorsName, productColorsQty);

                _context.Update(existingProduct);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] = "تم تعديل المنتج بنجاح";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError("", "خطأ: " + ex.Message);
                await PrepareDropdownsAsync(model.CategoryId, model.MerchantId);
                return View(model);
            }
        }

        // 6. Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _context.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == id);
            if (product != null)
            {
                DeleteFile(product.ImageUrl);
                foreach (var img in product.Images) DeleteFile(img.ImageUrl);
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حذف المنتج";
            }
            return RedirectToAction(nameof(Index));
        }

        // --- Helpers ---
        private async Task ProcessSubItems(int productId, List<IFormFile> galleryImages, string[] colorsHex, string[] colorsName, string[] colorsQty)
        {
            if (galleryImages != null)
            {
                foreach (var file in galleryImages)
                {
                    if (file.Length > 0)
                    {
                        var path = await SaveFile(file);
                        _context.ProductImages.Add(new ProductImage { ProductId = productId, ImageUrl = path });
                    }
                }
            }

            if (colorsHex != null && colorsName != null)
            {
                for (int i = 0; i < colorsHex.Length; i++)
                {
                    if (!string.IsNullOrEmpty(colorsHex[i]))
                    {
                        int qty = 0;
                        if (colorsQty != null && i < colorsQty.Length) int.TryParse(colorsQty[i], out qty);

                        _context.ProductColors.Add(new ProductColor
                        {
                            ProductId = productId,
                            ColorHex = colorsHex[i],
                            ColorName = (i < colorsName.Length && !string.IsNullOrEmpty(colorsName[i])) ? colorsName[i] : "لون",
                            // Quantity = qty // تأكد من إضافة هذا الحقل للموديل لاحقاً إذا أردت تفعيله
                        });
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

        private void DeleteFile(string path)
        {
            if (string.IsNullOrEmpty(path) || path.Contains("default")) return;
            try
            {
                string fullPath = Path.Combine(_webHostEnvironment.WebRootPath, path);
                if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
            }
            catch { }
        }
        [HttpPost]
        public async Task<IActionResult> ExportToExcel()
        {
            var products = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Merchant)
                .ToListAsync();

            var builder = new StringBuilder();
            builder.AppendLine("Id,Name,Price,Stock,Category,Merchant,Status,SKU");

            foreach (var p in products)
            {
                builder.AppendLine($"{p.Id},{EscapeCsv(p.Name)},{p.Price},{p.StockQuantity},{EscapeCsv(p.Category?.Name)},{EscapeCsv(p.Merchant?.ShopName)},{p.Status},{p.SKU}");
            }

            return File(Encoding.UTF8.GetBytes(builder.ToString()), "text/csv", "products_export.csv");
        }

        [HttpPost]
        public async Task<IActionResult> ImportFromExcel(IFormFile excelFile)
        {
            if (excelFile == null || excelFile.Length == 0)
            {
                TempData["Error"] = "يرجى اختيار ملف صحيح.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                using (var reader = new StreamReader(excelFile.OpenReadStream()))
                {
                    // Skip header
                    await reader.ReadLineAsync();

                    while (!reader.EndOfStream)
                    {
                        var line = await reader.ReadLineAsync();
                        var values = line.Split(',');

                        if (values.Length >= 4) // الحد الأدنى من الأعمدة
                        {
                            var product = new Product
                            {
                                Name = values[1],
                                Price = decimal.Parse(values[2]),
                                StockQuantity = int.Parse(values[3]),
                                Status = "Draft", // افتراضي
                                Slug = Guid.NewGuid().ToString(),
                                ImageUrl = "images/default-product.png"
                                // يمكن توسيع المنطق لربط الأقسام والتجار
                            };
                            _context.Products.Add(product);
                        }
                    }
                }
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم استيراد المنتجات بنجاح.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "حدث خطأ أثناء الاستيراد: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        private string EscapeCsv(string field)
        {
            if (string.IsNullOrEmpty(field)) return "";
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        private async Task PrepareDropdownsAsync(int? selectedCategory = null, string selectedMerchant = null)
        {
            var categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();
            ViewBag.CategoryId = new SelectList(categories, "Id", "Name", selectedCategory);

            var merchants = await _userManager.GetUsersInRoleAsync("Merchant");
            var merchantList = merchants.Select(u => new { Id = u.Id, ShopName = u.ShopName ?? u.UserName }).ToList();
            ViewBag.MerchantId = new SelectList(merchantList, "Id", "ShopName", selectedMerchant);
        }
    }
}