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
using System.Text.RegularExpressions;
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
                await RestoreModelData(model, productColorsHex, productColorsName, productColorsQty);
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

            builder.Append('\uFEFF');

            builder.AppendLine("Id,Name,NameEn,Description,DescriptionEn,Price,OldPrice,CostPrice,StockQuantity,LowStockThreshold,UnitsPerCarton,SKU,Barcode,Color,Brand,Weight,Slug,MetaTitle,MetaDescription,Status,Category,Merchant");

            foreach (var p in products)
            {
                var line = string.Join(",",
                    p.Id,
                    EscapeCsv(p.Name),
                    EscapeCsv(p.NameEn),
                    EscapeCsv(p.Description), 
                    EscapeCsv(p.DescriptionEn),
                    p.Price,
                    p.OldPrice,
                    p.CostPrice,
                    p.StockQuantity,
                    p.LowStockThreshold,
                    p.UnitsPerCarton,
                    EscapeCsv(p.SKU),
                    EscapeCsv(p.Barcode),
                    EscapeCsv(p.Color),
                    EscapeCsv(p.Brand),
                    p.Weight,
                    EscapeCsv(p.Slug),
                    EscapeCsv(p.MetaTitle),
                    EscapeCsv(p.MetaDescription),
                    p.Status,
                    EscapeCsv(p.Category?.Name),
                    EscapeCsv(p.Merchant?.ShopName)
                );
                builder.AppendLine(line);
            }

            return File(Encoding.UTF8.GetBytes(builder.ToString()), "text/csv", $"products_export_{DateTime.Now:yyyyMMdd}.csv");
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
                    await reader.ReadLineAsync();

                    while (!reader.EndOfStream)
                    {
                        var line = await reader.ReadLineAsync();

                        var values = Regex.Split(line, ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");

                        for (int i = 0; i < values.Length; i++) values[i] = values[i].Trim('"');

                        if (values.Length >= 2)
                        {
                            var product = new Product();

                          
                            product.Name = GetValue(values, 1);
                            product.NameEn = GetValue(values, 2);
                            product.Description = GetValue(values, 3);
                            product.DescriptionEn = GetValue(values, 4);
                            product.Price = ParseDecimal(GetValue(values, 5));
                            product.OldPrice = ParseDecimal(GetValue(values, 6));
                            product.CostPrice = ParseDecimal(GetValue(values, 7));
                            product.StockQuantity = ParseInt(GetValue(values, 8));
                            product.LowStockThreshold = ParseInt(GetValue(values, 9));
                            product.UnitsPerCarton = ParseInt(GetValue(values, 10));
                            product.SKU = GetValue(values, 11);
                            product.Barcode = GetValue(values, 12);
                            product.Color = GetValue(values, 13);
                            product.Brand = GetValue(values, 14);
                            product.Weight = ParseDecimal(GetValue(values, 15));
                            product.Slug = GetValue(values, 16);
                            product.MetaTitle = GetValue(values, 17);
                            product.MetaDescription = GetValue(values, 18);
                            product.Status = GetValue(values, 19) ?? "Draft";

                            if (string.IsNullOrEmpty(product.Color)) product.Color = "#000000";
                            if (string.IsNullOrEmpty(product.ImageUrl)) product.ImageUrl = "images/default-product.png";
                            if (string.IsNullOrEmpty(product.Slug)) product.Slug = Guid.NewGuid().ToString();

                            string catName = GetValue(values, 20);
                            if (!string.IsNullOrEmpty(catName))
                            {
                                var cat = await _context.Categories.FirstOrDefaultAsync(c => c.Name == catName || c.NameEn == catName);
                                if (cat != null) product.CategoryId = cat.Id;
                            }

                            string merchName = GetValue(values, 21);
                            if (!string.IsNullOrEmpty(merchName))
                            {
                                var merch = await _context.Users.FirstOrDefaultAsync(u => u.ShopName == merchName);
                                if (merch != null) product.MerchantId = merch.Id;
                            }
                            if (product.MerchantId == null) product.MerchantId = _userManager.GetUserId(User);

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
            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n"))
            {
                return $"\"{field.Replace("\"", "\"\"")}\"";
            }
            return field;
        }

        private string GetValue(string[] values, int index) => index < values.Length ? values[index] : null;
        private decimal ParseDecimal(string val) => decimal.TryParse(val, out decimal res) ? res : 0;
        private int ParseInt(string val) => int.TryParse(val, out int res) ? res : 0;
        private double ParseDouble(string val) => double.TryParse(val, out double res) ? res : 0;
        private async Task PrepareDropdownsAsync(int? selectedCategory = null, string selectedMerchant = null)
        {
            var categories = await _context.Categories
                .Select(c => new { c.Id, c.Name })
                .OrderBy(c => c.Name)
                .ToListAsync();

            ViewBag.Categories = new SelectList(categories, "Id", "Name", selectedCategory);

            
            var merchants = await _context.Users
                .Where(u => u.ShopName != null)
                .Select(u => new { u.Id, u.ShopName })
                .OrderBy(u => u.ShopName)
                .ToListAsync();

            ViewBag.Merchants = new SelectList(merchants, "Id", "ShopName", selectedMerchant);
        }
        private async Task RestoreModelData(Product model, string[] hex, string[] names, string[] qtys)
        {
            var dbItem = await _context.Products.Include(p => p.Images).AsNoTracking().FirstOrDefaultAsync(p => p.Id == model.Id);
            if (dbItem != null)
            {
                model.ImageUrl = dbItem.ImageUrl;
                model.Images = dbItem.Images;
            }

            model.ProductColors = new List<ProductColor>();
            if (hex != null)
            {
                for (int i = 0; i < hex.Length; i++)
                {
                    model.ProductColors.Add(new ProductColor
                    {
                        ColorHex = hex[i],
                        ColorName = (names != null && i < names.Length) ? names[i] : "",
                    });
                }
            }
        }
    }
}