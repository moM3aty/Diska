using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Diska.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Diska.Controllers
{
    [Authorize] // يجب أن يكون مسجلاً للدخول
    public class MerchantController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly UserManager<IdentityUser> _userManager;

        public MerchantController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            // عرض منتجات التاجر الحالي فقط
            var products = _context.Products
                                   .Where(p => p.MerchantId == user.Id)
                                   .Include(p => p.Category)
                                   .OrderByDescending(p => p.Id)
                                   .ToList();

            // تمرير التصنيفات للعرض في الـ View (للقائمة المنسدلة في المودال)
            ViewBag.Categories = _context.Categories.ToList();
            return View(products);
        }

        [HttpPost]
        public async Task<IActionResult> AddProduct(Product product, IFormFile imageFile)
        {
            var user = await _userManager.GetUserAsync(User);
            product.MerchantId = user.Id; // ربط المنتج بالتاجر

            if (imageFile != null)
            {
                string folder = "images/products/";
                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
                string serverPath = Path.Combine(_webHostEnvironment.WebRootPath, folder + fileName);

                // التأكد من وجود المجلد
                string directory = Path.GetDirectoryName(serverPath);
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

                using (var stream = new FileStream(serverPath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(stream);
                }
                product.ImageUrl = folder + fileName;
            }
            else
            {
                product.ImageUrl = "images/default-product.png";
            }

            // تحقق بسيط لتفادي أخطاء الـ Model State بسبب الـ Relations
            if (product.CategoryId == 0) product.CategoryId = _context.Categories.FirstOrDefault()?.Id ?? 1;

            _context.Products.Add(product);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // --- CRUD: Edit (GET) ---
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id && p.MerchantId == user.Id);

            if (product == null) return NotFound();

            ViewBag.Categories = _context.Categories.ToList();
            return View(product);
        }

        // --- CRUD: Edit (POST) ---
        [HttpPost]
        public async Task<IActionResult> Edit(Product model, IFormFile imageFile)
        {
            var product = await _context.Products.FindAsync(model.Id);
            if (product == null) return NotFound();

            // تحديث البيانات
            product.Name = model.Name;
            product.Price = model.Price;
            product.OldPrice = model.OldPrice;
            product.StockQuantity = model.StockQuantity;
            product.UnitsPerCarton = model.UnitsPerCarton;
            product.Description = model.Description;
            product.CategoryId = model.CategoryId;
            product.ProductionDate = model.ProductionDate;
            product.ExpiryDate = model.ExpiryDate;

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

            _context.Update(product);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // --- CRUD: Delete ---
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