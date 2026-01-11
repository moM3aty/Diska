using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Diska.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using System.Text.Json;
using Diska.Services;
using Microsoft.AspNetCore.Mvc.Rendering;

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
            return View(new Product { Status = "Draft", ProductionDate = DateTime.Now, ExpiryDate = DateTime.Now.AddMonths(6) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product model, IFormFile mainImage)
        {
            var user = await _userManager.GetUserAsync(User);

            ModelState.Remove("Merchant");
            ModelState.Remove("Category");
            ModelState.Remove("ImageUrl");

            if (ModelState.IsValid)
            {
                model.MerchantId = user.Id;
                model.Status = "Pending"; // يذهب للموافقة

                if (mainImage != null) model.ImageUrl = await SaveFile(mainImage);
                else model.ImageUrl = "images/default-product.png";

                _context.Products.Add(model);
                await _context.SaveChangesAsync();

                await _auditService.LogAsync(user.Id, "Create Product Request", "Product", model.Id.ToString(), $"طلب إضافة منتج: {model.Name}", HttpContext.Connection.RemoteIpAddress?.ToString());
                await _notificationService.NotifyAdminsAsync("طلب إضافة منتج", $"التاجر {user.ShopName} أضاف منتجاً جديداً.");

                TempData["Success"] = "تم إضافة المنتج وهو قيد المراجعة.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Categories = new SelectList(_context.Categories, "Id", "Name");
            return View(model);
        }

        // طلب تعديل سعر (Workflow)
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
                RequestDate = DateTime.Now
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