using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Diska.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Diska.Services;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Diska.Areas.Merchant.Controllers
{
    [Area("Merchant")]
    [Authorize(Roles = "Merchant")]
    public class BannerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notificationService;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public BannerController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, INotificationService notificationService, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _userManager = userManager;
            _notificationService = notificationService;
            _webHostEnvironment = webHostEnvironment;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var banners = await _context.Banners
                .Where(b => b.MerchantId == user.Id)
                .OrderByDescending(b => b.Id)
                .ToListAsync();

            return View(banners);
        }

        // --- إضافة إعلان ---
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await PrepareViewBags(); // تجهيز القوائم
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Banner model, IFormFile imageFile)
        {
            var user = await _userManager.GetUserAsync(User);

            if (imageFile == null || imageFile.Length == 0)
                ModelState.AddModelError("ImageDesktop", "يرجى رفع صورة للإعلان");

            ModelState.Remove("Merchant");
            ModelState.Remove("MerchantId");
            ModelState.Remove("ImageDesktop");
            ModelState.Remove("ImageMobile");
            // LinkId يتم تعبئته من القوائم المنسدلة عبر الجافاسكريبت، لذا لا نزيله

            if (ModelState.IsValid)
            {
                string folder = "images/banners/";
                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
                string path = Path.Combine(_webHostEnvironment.WebRootPath, folder + fileName);

                string dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                using (var stream = new FileStream(path, FileMode.Create)) await imageFile.CopyToAsync(stream);

                model.ImageDesktop = folder + fileName;
                model.ImageMobile = folder + fileName;
                model.MerchantId = user.Id;
                model.ApprovalStatus = "Pending";
                model.IsActive = false;

                // قيمة افتراضية إذا لم يتم اختيار رابط
                if (string.IsNullOrEmpty(model.LinkId)) model.LinkId = "#";

                _context.Banners.Add(model);
                await _context.SaveChangesAsync();

                await _notificationService.NotifyAdminsAsync("طلب إعلان جديد", $"التاجر {user.ShopName} طلب إضافة إعلان: {model.Title}", "Banner");

                TempData["Success"] = "تم إرسال الإعلان للمراجعة بنجاح.";
                return RedirectToAction(nameof(Index));
            }

            await PrepareViewBags(); // إعادة تعبئة القوائم عند الخطأ
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var banner = await _context.Banners.FirstOrDefaultAsync(b => b.Id == id && b.MerchantId == user.Id);

            if (banner != null)
            {
                _context.Banners.Remove(banner);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حذف الإعلان.";
            }
            return RedirectToAction(nameof(Index));
        }

        // --- دالة تجهيز القوائم ---
        private async Task PrepareViewBags()
        {
            var user = await _userManager.GetUserAsync(User);

            // جلب منتجات التاجر الحالي فقط
            var products = _context.Products
                .Where(p => p.MerchantId == user.Id && p.Status == "Active")
                .Select(p => new { p.Id, Name = p.Name });

            ViewBag.Products = new SelectList(await products.ToListAsync(), "Id", "Name");

            // جلب الأقسام الرئيسية
            var categories = _context.Categories
                .Select(c => new { c.Id, c.Name });

            ViewBag.Categories = new SelectList(await categories.ToListAsync(), "Id", "Name");
        }
    }
}