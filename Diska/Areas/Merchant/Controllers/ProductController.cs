using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Diska.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using System.Text.Json;
using Diska.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Text.RegularExpressions;

namespace Diska.Areas.Merchant.Controllers
{
    [Area("Merchant")]
    [Authorize(Roles = "Merchant")]
    public class ProductController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditService _auditService;
        private readonly INotificationService _notificationService;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ProductController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IAuditService auditService, INotificationService notificationService, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _userManager = userManager;
            _auditService = auditService;
            _notificationService = notificationService;
            _webHostEnvironment = webHostEnvironment;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var products = await _context.Products
                .Where(p => p.MerchantId == user.Id)
                .Include(p => p.Category)
                .OrderByDescending(p => p.Id)
                .ToListAsync();
            return View(products);
        }

        [HttpGet]
        public IActionResult Create()
        {
            ViewBag.Categories = new SelectList(_context.Categories, "Id", "Name");
            return View(new Product { Status = "Active", ProductionDate = DateTime.Now, ExpiryDate = DateTime.Now.AddMonths(6) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product model, IFormFile mainImage, IEnumerable<IFormFile> galleryImages, string[] productColorsHex, string[] productColorsName)
        {
            var user = await _userManager.GetUserAsync(User);

            ModelState.Remove("Merchant");
            ModelState.Remove("MerchantId");
            ModelState.Remove("Category");
            ModelState.Remove("ImageUrl");
            ModelState.Remove("Slug");
            ModelState.Remove("Color");

            if (ModelState.IsValid)
            {
                model.MerchantId = user.Id;
                model.Status = "Active"; // تفعيل مباشر

                if (string.IsNullOrWhiteSpace(model.Slug))
                {
                    string slugSource = !string.IsNullOrWhiteSpace(model.NameEn) ? model.NameEn : model.Name;
                    model.Slug = Regex.Replace(slugSource.Trim(), @"[^a-zA-Z0-9\u0600-\u06FF\s-]", "").Replace(" ", "-").ToLower();
                    if (string.IsNullOrWhiteSpace(model.Slug)) model.Slug = $"product-{Guid.NewGuid().ToString().Substring(0, 8)}";
                }

                if (productColorsName != null && productColorsName.Length > 0 && !string.IsNullOrWhiteSpace(productColorsName[0]))
                    model.Color = productColorsName[0];
                else
                    model.Color = "Standard";

                if (mainImage != null) model.ImageUrl = await SaveFile(mainImage);
                else model.ImageUrl = "images/default-product.png";

                _context.Products.Add(model);
                await _context.SaveChangesAsync();

                // حفظ الصور والألوان... (كما في الكود السابق)
                if (galleryImages != null && galleryImages.Any())
                {
                    foreach (var file in galleryImages)
                    {
                        if (file.Length > 0)
                        {
                            var imgPath = await SaveFile(file);
                            _context.ProductImages.Add(new ProductImage { ProductId = model.Id, ImageUrl = imgPath });
                        }
                    }
                }

                if (productColorsName != null && productColorsName.Length > 0)
                {
                    for (int i = 0; i < productColorsName.Length; i++)
                    {
                        if (!string.IsNullOrWhiteSpace(productColorsName[i]))
                        {
                            _context.ProductColors.Add(new ProductColor
                            {
                                ProductId = model.Id,
                                ColorName = productColorsName[i],
                                ColorHex = (productColorsHex != null && productColorsHex.Length > i) ? productColorsHex[i] : "#000000"
                            });
                        }
                    }
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = "تم إضافة المنتج بنجاح.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Categories = new SelectList(_context.Categories, "Id", "Name");
            return View(model);
        }

        // --- تحديث المخزون مباشرة (بدون موافقة) ---
        [HttpPost]
        public async Task<IActionResult> UpdateStockDirectly(int id, int newQuantity)
        {
            var user = await _userManager.GetUserAsync(User);
            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id && p.MerchantId == user.Id);

            if (product == null) return Json(new { success = false, message = "المنتج غير موجود" });

            if (newQuantity < 0) return Json(new { success = false, message = "الكمية لا يمكن أن تكون بالسالب" });

            // تحديث مباشر في الجدول
            product.StockQuantity = newQuantity;

            // (اختياري) إذا كانت الكمية 0 نغير الحالة، أو نتركها Active
            if (newQuantity == 0) { /* product.Status = "OutOfStock"; */ }

            _context.Products.Update(product);
            await _context.SaveChangesAsync();

            // إشعار للأدمن للعلم فقط (اختياري)
            // await _auditService.LogAsync(...);

            return Json(new { success = true, message = "تم تحديث المخزون بنجاح" });
        }

        // طلب تعديل السعر (ما زال يحتاج موافقة - يمكن تغييره ليصبح مباشراً بالمثل)
        [HttpPost]
        public async Task<IActionResult> RequestUpdate(int id, decimal newPrice)
        {
            var user = await _userManager.GetUserAsync(User);
            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id && p.MerchantId == user.Id);

            if (product == null) return NotFound();

            var action = new PendingMerchantAction
            {
                MerchantId = user.Id,
                ActionType = "UpdateProductPrice",
                EntityName = "Product",
                EntityId = id.ToString(),
                OldValueJson = JsonSerializer.Serialize(new { Price = product.Price }),
                NewValueJson = JsonSerializer.Serialize(new { Price = newPrice }),
                Status = "Pending",
                RequestDate = DateTime.Now,
                ActionByAdminId = string.Empty,
                AdminComment = string.Empty
            };

            _context.PendingMerchantActions.Add(action);
            await _context.SaveChangesAsync();
            await _notificationService.NotifyAdminsAsync("طلب تعديل سعر", $"التاجر {user.ShopName} يطلب تعديل سعر منتج.");

            TempData["Success"] = "تم إرسال طلب تعديل السعر للإدارة.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<string> SaveFile(IFormFile file)
        {
            string folder = "images/products/";
            string fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
            string path = Path.Combine(_webHostEnvironment.WebRootPath, folder + fileName);
            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            using (var stream = new FileStream(path, FileMode.Create)) await file.CopyToAsync(stream);
            return folder + fileName;
        }
    }
}