using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Diska.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Diska.Controllers
{
    [Authorize]
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


        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var products = _context.Products.Where(p => p.MerchantId == user.Id).OrderByDescending(p => p.Id).ToList();
            return View(products);
        }

 
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var product = await _context.Products
                .Include(p => p.PriceTiers) 
                .FirstOrDefaultAsync(p => p.Id == id && p.MerchantId == user.Id);

            if (product == null) return NotFound();

            ViewBag.Categories = _context.Categories.ToList();
            return View(product);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(Product model, IFormFile imageFile)
        {
            var product = await _context.Products
                .Include(p => p.PriceTiers)
                .FirstOrDefaultAsync(p => p.Id == model.Id);

            if (product == null) return NotFound();

            product.Name = model.Name;
            product.NameEn = model.NameEn;
            product.Price = model.Price;
            product.OldPrice = model.OldPrice;
            product.StockQuantity = model.StockQuantity;
            product.UnitsPerCarton = model.UnitsPerCarton;
            product.Description = model.Description;
            product.DescriptionEn = model.DescriptionEn;
            product.CategoryId = model.CategoryId;

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


            _context.PriceTiers.RemoveRange(product.PriceTiers);

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

            _context.Update(product);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> AddProduct(Product product, IFormFile imageFile)
        {
            var user = await _userManager.GetUserAsync(User);
            product.MerchantId = user.Id;

            if (imageFile != null)
            {
                string folder = "images/products/";
                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
                string serverPath = Path.Combine(_webHostEnvironment.WebRootPath, folder + fileName);

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

            _context.Products.Add(product);
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