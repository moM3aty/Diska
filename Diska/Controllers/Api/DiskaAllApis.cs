using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Diska.Models;
using Diska.Data;
using Diska.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;

namespace Diska.ApiControllers
{
    // =========================================================================
    // 1. AUTHENTICATION API
    // =========================================================================
    [Route("api/mobile/auth")] // 🚨 تعديل الرابط
    [ApiController]
    public class AuthApiController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public AuthApiController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] ApiLoginDto model)
        {
            if (string.IsNullOrEmpty(model.Phone) || string.IsNullOrEmpty(model.Password))
                return BadRequest(new { success = false, message = "بيانات غير مكتملة" });

            var user = await _userManager.FindByNameAsync(model.Phone) ?? _userManager.Users.FirstOrDefault(u => u.PhoneNumber == model.Phone);
            if (user == null) return Unauthorized(new { success = false, message = "المستخدم غير موجود" });

            if (await _userManager.IsLockedOutAsync(user))
                return StatusCode(403, new { success = false, message = "هذا الحساب محظور" });

            var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, true);
            if (result.Succeeded)
            {
                if (await _userManager.IsInRoleAsync(user, "Merchant") && !user.IsVerifiedMerchant)
                {
                    await _signInManager.SignOutAsync();
                    return StatusCode(403, new { success = false, message = "حساب التاجر قيد المراجعة" });
                }
                return Ok(new { success = true, data = new { userId = user.Id, name = user.FullName, role = user.UserType, balance = user.WalletBalance } });
            }
            return Unauthorized(new { success = false, message = "كلمة المرور غير صحيحة" });
        }

        [HttpPost("signup")]
        public async Task<IActionResult> Signup([FromBody] ApiSignupDto model)
        {
            if (string.IsNullOrEmpty(model.Phone)) return BadRequest(new { success = false, message = "رقم الهاتف مطلوب" });
            if (await _userManager.FindByNameAsync(model.Phone) != null) return Conflict(new { success = false, message = "رقم الهاتف مسجل مسبقاً" });

            string role = model.Type == "Merchant" ? "Merchant" : "Customer";
            var user = new ApplicationUser
            {
                UserName = model.Phone,
                PhoneNumber = model.Phone,
                FullName = model.FullName,
                ShopName = role == "Merchant" ? model.ShopName : "عميل",
                CommercialRegister = model.CommercialReg ?? "000000",
                TaxCard = model.TaxCard ?? "000000",
                IsVerifiedMerchant = false,
                Email = $"{model.Phone}@diska.local",
                UserType = role,
                CreatedAt = DateTime.Now
            };

            var result = await _userManager.CreateAsync(user, model.Password!);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, role);
                if (role == "Customer") await _signInManager.SignInAsync(user, true);
                return Ok(new { success = true, message = "تم إنشاء الحساب بنجاح" });
            }
            return BadRequest(new { success = false, errors = result.Errors.Select(e => e.Description) });
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout() { await _signInManager.SignOutAsync(); return Ok(); }
    }

    // =========================================================================
    // 2. PUBLIC API
    // =========================================================================
    [Route("api/mobile/public")] // 🚨 تعديل الرابط
    [ApiController]
    public class PublicApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        public PublicApiController(ApplicationDbContext context) => _context = context;

        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories()
        {
            try
            {
                var data = await _context.Categories
                    .Where(c => c.IsActive)
                    .Select(c => new { c.Id, c.Name, c.NameEn, c.ImageUrl })
                    .ToListAsync();

                return Ok(new { success = true, data = data });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, message = "قاعدة البيانات ضربت إيرور:", errorDetail = ex.Message });
            }
        }

        [HttpGet("products")]
        public async Task<IActionResult> GetProducts(string? query, int? categoryId)
        {
            var q = _context.Products.Where(p => p.Status == "Active" && p.StockQuantity > 0).AsQueryable();
            if (categoryId.HasValue) q = q.Where(p => p.CategoryId == categoryId);
            if (!string.IsNullOrEmpty(query)) q = q.Where(p => p.Name.Contains(query) || p.SKU.Contains(query));
            return Ok(await q.Select(p => new { p.Id, p.Name, p.Price, p.OldPrice, p.ImageUrl, p.StockQuantity }).Take(50).ToListAsync());
        }

        [HttpGet("deals")]
        public async Task<IActionResult> GetDeals() => Ok(await _context.GroupDeals.Where(d => d.IsActive && d.EndDate > DateTime.Now).Include(d => d.Product).Select(d => new { d.Id, d.Title, d.DiscountValue, Product = d.Product!.Name }).ToListAsync());

        [HttpGet("banners")]
        public async Task<IActionResult> GetBanners() => Ok(await _context.Banners.Where(b => b.IsActive && b.EndDate > DateTime.Now).Select(b => new { b.Id, b.Title, b.ImageMobile, b.LinkId, b.LinkType }).ToListAsync());
    }

    // =========================================================================
    // 3. CUSTOMER API
    // =========================================================================
    [Route("api/mobile/customer")] // 🚨 تعديل الرابط
    [ApiController]
    [Authorize]
    public class CustomerApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IShippingService _shippingService;

        public CustomerApiController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IShippingService shippingService)
        {
            _context = context; _userManager = userManager; _shippingService = shippingService;
        }

        private string UserId => _userManager.GetUserId(User) ?? string.Empty;

        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile() => Ok(await _context.Users.Where(u => u.Id == UserId).Select(u => new { u.FullName, u.PhoneNumber, u.WalletBalance }).FirstOrDefaultAsync());

        [HttpGet("addresses")]
        public async Task<IActionResult> GetAddresses() => Ok(await _context.UserAddresses.Where(a => a.UserId == UserId).ToListAsync());

        [HttpPost("addresses")]
        public async Task<IActionResult> AddAddress([FromBody] UserAddress addr) { addr.UserId = UserId; _context.UserAddresses.Add(addr); await _context.SaveChangesAsync(); return Ok(); }

        [HttpPost("wishlist/toggle")]
        public async Task<IActionResult> ToggleWishlist([FromBody] ApiIdDto dto)
        {
            var item = await _context.WishlistItems.FirstOrDefaultAsync(w => w.UserId == UserId && w.ProductId == dto.Id);
            if (item != null) _context.WishlistItems.Remove(item); else _context.WishlistItems.Add(new WishlistItem { UserId = UserId, ProductId = dto.Id });
            await _context.SaveChangesAsync(); return Ok(new { success = true, added = item == null });
        }

        [HttpGet("shipping-cost")]
        public IActionResult GetShipping(string gov, string city) => Ok(new { cost = _shippingService.CalculateCost(gov, city) });

        [HttpPost("checkout")]
        public async Task<IActionResult> Checkout([FromBody] ApiCheckoutDto dto)
        {
            if (dto.Items == null || !dto.Items.Any()) return BadRequest(new { success = false, message = "السلة فارغة" });

            decimal total = dto.ShippingCost;
            var order = new Order
            {
                UserId = UserId,
                CustomerName = dto.Name!,
                Phone = dto.Phone!,
                Governorate = dto.Governorate!,
                City = dto.City!,
                Address = dto.Address!,
                PaymentMethod = dto.PaymentMethod ?? "Cash",
                OrderDate = DateTime.Now,
                Status = "Pending",
                ShippingCost = dto.ShippingCost,
                OrderItems = new List<OrderItem>()
            };

            foreach (var item in dto.Items)
            {
                var p = await _context.Products.FindAsync(item.Id);
                if (p != null && p.StockQuantity >= item.Qty)
                {
                    p.StockQuantity -= item.Qty;
                    order.OrderItems.Add(new OrderItem { ProductId = p.Id, Quantity = item.Qty, UnitPrice = p.Price });
                    total += (p.Price * item.Qty);
                }
            }
            order.TotalAmount = total; _context.Orders.Add(order); await _context.SaveChangesAsync();
            return Ok(new { success = true, orderId = order.Id });
        }
    }

    // =========================================================================
    // 4. MERCHANT & ADMIN API (مختصرة لتجنب الإطالة في نفس الملف)
    // =========================================================================
    [Route("api/mobile/merchant")]
    [ApiController]
    [Authorize(Roles = "Merchant")]
    public class MerchantApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context; private readonly UserManager<ApplicationUser> _userManager;
        public MerchantApiController(ApplicationDbContext context, UserManager<ApplicationUser> userManager) { _context = context; _userManager = userManager; }
        private string UserId => _userManager.GetUserId(User) ?? string.Empty;

        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard() => Ok(new { Products = await _context.Products.CountAsync(p => p.MerchantId == UserId) });
    }

    [Route("api/mobile/admin")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context; private readonly UserManager<ApplicationUser> _userManager;
        public AdminApiController(ApplicationDbContext context, UserManager<ApplicationUser> userManager) { _context = context; _userManager = userManager; }

        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard() => Ok(new { TotalOrders = await _context.Orders.CountAsync() });
    }

    // =========================================================================
    // DTOs
    // =========================================================================
    public class ApiLoginDto { public string? Phone { get; set; } public string? Password { get; set; } public bool RememberMe { get; set; } }
    public class ApiSignupDto { public string? FullName { get; set; } public string? Phone { get; set; } public string? Password { get; set; } public string? Type { get; set; } public string? ShopName { get; set; } public string? CommercialReg { get; set; } public string? TaxCard { get; set; } }
    public class ApiIdDto { public int Id { get; set; } }
    public class ApiCheckoutDto { public string? Name { get; set; } public string? Phone { get; set; } public string? Governorate { get; set; } public string? City { get; set; } public string? Address { get; set; } public string? PaymentMethod { get; set; } public decimal ShippingCost { get; set; } public List<ApiCartItemDto>? Items { get; set; } }
    public class ApiCartItemDto { public int Id { get; set; } public int Qty { get; set; } }
}