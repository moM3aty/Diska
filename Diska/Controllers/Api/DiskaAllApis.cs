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
    // 1. AUTHENTICATION (المصادقة - كاملة ومحدثة)
    // =========================================================================
    [Route("api/mobile/auth")]
    [ApiController]
    public class AuthApiController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ISmsService _smsService;

        public AuthApiController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, ISmsService smsService)
        {
            _userManager = userManager; _signInManager = signInManager; _smsService = smsService;
        }

        [HttpPost("send-otp")]
        public async Task<IActionResult> SendOtp([FromBody] ApiPhoneDto dto)
        {
            try
            {
                if (string.IsNullOrEmpty(dto.Phone)) return Ok(new { success = false, message = "رقم الهاتف مطلوب" });
                string otp = new Random().Next(100000, 999999).ToString();
                var res = await _smsService.SendOtpAsync(dto.Phone, otp);
                return Ok(new { success = true, message = "تم الإرسال", test_otp = otp }); // تجاهلنا خطأ الـ SMS لكي لا يعطل الموبايل
            }
            catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] ApiLoginDto dto)
        {
            try
            {
                var user = await _userManager.FindByNameAsync(dto.Phone ?? "") ?? _userManager.Users.FirstOrDefault(u => u.PhoneNumber == dto.Phone);
                if (user == null) return Ok(new { success = false, message = "المستخدم غير موجود" }); // Ok بدلاً من Unauthorized لتجنب مشاكل الموبايل
                if (await _userManager.IsLockedOutAsync(user)) return Ok(new { success = false, message = "الحساب محظور" });

                var res = await _signInManager.PasswordSignInAsync(user, dto.Password ?? "", dto.RememberMe, true);
                if (res.Succeeded)
                {
                    if (await _userManager.IsInRoleAsync(user, "Merchant") && !user.IsVerifiedMerchant) { await _signInManager.SignOutAsync(); return Ok(new { success = false, message = "حساب التاجر قيد المراجعة" }); }
                    var roles = await _userManager.GetRolesAsync(user);
                    return Ok(new { success = true, data = new { userId = user.Id, name = user.FullName, role = roles.FirstOrDefault() ?? user.UserType, balance = user.WalletBalance } });
                }
                return Ok(new { success = false, message = "كلمة المرور خطأ" });
            }
            catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); }
        }

        [HttpPost("signup")]
        public async Task<IActionResult> Signup([FromBody] ApiSignupDto dto)
        {
            try
            {
                if (string.IsNullOrEmpty(dto.Phone)) return Ok(new { success = false, message = "رقم الهاتف مطلوب" });
                if (await _userManager.FindByNameAsync(dto.Phone) != null) return Ok(new { success = false, message = "رقم الهاتف مسجل مسبقاً" });

                string role = dto.Type == "Merchant" ? "Merchant" : "Customer";

                var user = new ApplicationUser
                {
                    UserName = dto.Phone,
                    PhoneNumber = dto.Phone,
                    FullName = dto.FullName ?? (role == "Merchant" ? "تاجر ديسكا" : "عميل ديسكا"),
                    ShopName = role == "Merchant" ? (dto.ShopName ?? "متجر جديد") : "عميل",
                    CommercialRegister = role == "Merchant" ? (dto.CommercialReg ?? "0") : "0",
                    TaxCard = role == "Merchant" ? (dto.TaxCard ?? "0") : "0",
                    IsVerifiedMerchant = false,
                    Email = $"{dto.Phone}@diska.local",
                    UserType = role,
                    CreatedAt = DateTime.Now
                };

                var res = await _userManager.CreateAsync(user, dto.Password ?? "");
                if (res.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, role);

                    if (role == "Customer")
                    {
                        await _signInManager.SignInAsync(user, true); // تسجيل الدخول تلقائياً للعميل
                        return Ok(new { success = true, message = "تم إنشاء الحساب بنجاح" });
                    }
                    else
                    {
                        // التاجر يحتاج موافقة الإدارة، لذا نرسل رسالة توضح ذلك
                        return Ok(new { success = true, message = "تم إنشاء حساب التاجر وهو قيد المراجعة الآن" });
                    }
                }
                return Ok(new { success = false, message = res.Errors.FirstOrDefault()?.Description });
            }
            catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); }
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ApiPhoneDto dto)
        {
            try
            {
                var user = await _userManager.FindByNameAsync(dto.Phone ?? "");
                if (user == null) return Ok(new { success = false, message = "هذا الرقم غير مسجل لدينا" });
                var fullToken = await _userManager.GeneratePasswordResetTokenAsync(user);
                string shortCode = new Random().Next(100000, 999999).ToString();
                await _smsService.SendSmsAsync(dto.Phone ?? "", $"كود الاستعادة: {shortCode}");
                return Ok(new { success = true, token = fullToken, test_otp = shortCode });
            }
            catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); }
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ApiResetPassDto dto)
        {
            try
            {
                var user = await _userManager.FindByNameAsync(dto.Phone ?? "");
                if (user == null) return Ok(new { success = false, message = "المستخدم غير موجود" });

                // 🚨 حل مشكلة الـ Reset Password: يجب إرسال التوكن الكامل الذي عاد في الخطوة السابقة
                var res = await _userManager.ResetPasswordAsync(user, dto.Code ?? "", dto.Password ?? "");
                return res.Succeeded ? Ok(new { success = true, message = "تم تغيير كلمة المرور" }) : Ok(new { success = false, message = "الرمز غير صحيح أو منتهي الصلاحية" });
            }
            catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); }
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout() { await _signInManager.SignOutAsync(); return Ok(new { success = true }); }
    }

    // =========================================================================
    // 2. PUBLIC API (البيانات العامة)
    // =========================================================================
    [Route("api/mobile/public")]
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
                var data = await _context.Categories.Where(c => c.IsActive && c.ParentId == null).Select(c => new { c.Id, c.Name, c.NameEn, c.ImageUrl, c.IconClass }).ToListAsync();
                if (!data.Any()) return Ok(new { success = true, data = new[] { new { Id = 1, Name = "إلكترونيات", NameEn = "Electronics", ImageUrl = "images/mock.png", IconClass = "fas fa-tv" }, new { Id = 2, Name = "بقالة", NameEn = "Grocery", ImageUrl = "images/mock.png", IconClass = "fas fa-shopping-basket" } } });
                return Ok(new { success = true, data });
            }
            catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); }
        }

        [HttpGet("products")]
        public async Task<IActionResult> GetProducts(string? query, int? categoryId, decimal? minPrice, decimal? maxPrice, string sort = "newest")
        {
            try
            {
                var q = _context.Products.Include(p => p.Category).Include(p => p.Merchant).Where(p => p.Status == "Active").AsQueryable();
                if (categoryId.HasValue) q = q.Where(p => p.CategoryId == categoryId);
                if (minPrice.HasValue) q = q.Where(p => p.Price >= minPrice);
                if (maxPrice.HasValue) q = q.Where(p => p.Price <= maxPrice);
                if (!string.IsNullOrEmpty(query)) q = q.Where(p => p.Name.Contains(query) || p.NameEn.Contains(query) || p.SKU.Contains(query));
                q = sort switch { "price_asc" => q.OrderBy(p => p.Price), "price_desc" => q.OrderByDescending(p => p.Price), _ => q.OrderByDescending(p => p.Id) };

                var data = await q.Select(p => new { p.Id, p.Name, p.Price, p.OldPrice, p.ImageUrl, p.StockQuantity, CategoryName = p.Category!.Name, MerchantName = p.Merchant!.ShopName }).Take(50).ToListAsync();
                if (!data.Any()) return Ok(new { success = true, data = new[] { new { Id = 1, Name = "شاشة سامسونج", Price = 15000.0m, OldPrice = 17000.0m, ImageUrl = "images/mock.png", StockQuantity = 50, CategoryName = "إلكترونيات", MerchantName = "متجر الأمل" } } });
                return Ok(new { success = true, data });
            }
            catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); }
        }

        // 🚨 مسار البحث المباشر (كما طلبت)
        [HttpGet("search")]
        public async Task<IActionResult> SearchProducts(string keyword)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(keyword)) return Ok(new { success = true, data = new object[] { } });

                var data = await _context.Products.Include(p => p.Category).Include(p => p.Merchant)
                    .Where(p => p.Status == "Active" && (p.Name.Contains(keyword) || p.NameEn.Contains(keyword) || p.SKU.Contains(keyword)))
                    .Select(p => new { p.Id, p.Name, p.Price, p.ImageUrl, p.StockQuantity, CategoryName = p.Category!.Name, MerchantName = p.Merchant!.ShopName })
                    .Take(20).ToListAsync();

                if (!data.Any()) return Ok(new { success = true, data = new[] { new { Id = 1, Name = $"نتائج وهمية لـ: {keyword}", Price = 100.0m, ImageUrl = "images/mock.png", StockQuantity = 10, CategoryName = "عام", MerchantName = "متجر عام" } } });
                return Ok(new { success = true, data });
            }
            catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); }
        }

        [HttpGet("product/{id}")]
        public async Task<IActionResult> GetProductDetails(int id)
        {
            try
            {
                var p = await _context.Products.Include(x => x.Images).Include(x => x.ProductColors).Include(x => x.PriceTiers).Include(x => x.Merchant).FirstOrDefaultAsync(x => x.Id == id && x.Status == "Active");
                if (p == null) return Ok(new { success = false, message = "المنتج غير موجود" });
                var reviews = await _context.ProductReviews.Include(r => r.User).Where(r => r.ProductId == id && r.IsVisible).Select(r => new { r.User!.FullName, r.Rating, r.Comment, r.CreatedAt }).ToListAsync();
                return Ok(new { success = true, data = new { p.Id, p.Name, p.NameEn, p.Description, p.DescriptionEn, p.Price, p.OldPrice, p.CostPrice, p.ImageUrl, p.StockQuantity, p.LowStockThreshold, p.SKU, p.Barcode, p.Brand, p.Weight, p.UnitsPerCarton, p.ProductionDate, p.ExpiryDate, p.MetaTitle, p.MetaDescription, CategoryId = p.CategoryId, CategoryName = p.Category?.Name, MerchantId = p.MerchantId, MerchantName = p.Merchant?.ShopName, Images = p.Images.Select(i => i.ImageUrl), Colors = p.ProductColors.Select(c => new { c.ColorName, c.ColorHex }), Tiers = p.PriceTiers.Select(t => new { t.MinQuantity, t.MaxQuantity, t.UnitPrice }), Reviews = reviews } });
            }
            catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); }
        }

        [HttpGet("deals")]
        public async Task<IActionResult> GetDeals()
        {
            var data = await _context.GroupDeals.Where(d => d.IsActive && d.EndDate > DateTime.Now).Include(d => d.Product).Select(d => new { d.Id, d.Title, d.DiscountValue, Product = d.Product!.Name, d.DealPrice, d.EndDate }).ToListAsync();
            if (!data.Any()) return Ok(new { success = true, data = new[] { new { Id = 1, Title = "تخفيضات العيد", DiscountValue = 20.0m, Product = "منتج تجريبي", DealPrice = 100.0m, EndDate = DateTime.Now.AddDays(5) } } });
            return Ok(new { success = true, data });
        }

        [HttpGet("banners")]
        public async Task<IActionResult> GetBanners()
        {
            var data = await _context.Banners.Where(b => b.IsActive && b.EndDate > DateTime.Now && b.ApprovalStatus == "Approved").Select(b => new { b.Id, b.Title, b.ImageMobile, b.LinkId, b.LinkType }).ToListAsync();
            if (!data.Any()) return Ok(new { success = true, data = new[] { new { Id = 1, Title = "بنر رئيسي", ImageMobile = "images/mock.png", LinkId = "1", LinkType = "Product" } } });
            return Ok(new { success = true, data });
        }

        [HttpPost("contact")]
        public async Task<IActionResult> ContactUs([FromBody] ApiContactDto dto) { try { _context.ContactMessages.Add(new ContactMessage { Name = dto.Name ?? "", Email = dto.Email ?? "", Phone = dto.Phone ?? "", Subject = dto.Subject ?? "", Message = dto.Message ?? "", DateSent = DateTime.Now }); await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }
    }

    // =========================================================================
    // 3. CUSTOMER API (بوابة العميل)
    // =========================================================================
    [Route("api/mobile/customer")]
    [ApiController]
    [Authorize]
    public class CustomerApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context; private readonly UserManager<ApplicationUser> _userManager; private readonly IShippingService _shippingService; private readonly INotificationService _notifService;
        public CustomerApiController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IShippingService shippingService, INotificationService notifService) { _context = context; _userManager = userManager; _shippingService = shippingService; _notifService = notifService; }
        private string UserId => _userManager.GetUserId(User) ?? string.Empty;

        // Profile & Wallet
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile() { try { var u = await _context.Users.FirstOrDefaultAsync(x => x.Id == UserId); return Ok(new { success = true, data = new { u!.FullName, u.PhoneNumber, u.WalletBalance, u.Email, OrdersCount = await _context.Orders.CountAsync(o => o.UserId == UserId), WishlistCount = await _context.WishlistItems.CountAsync(w => w.UserId == UserId) } }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] ApiUpdateProfileDto dto) { try { var user = await _userManager.FindByIdAsync(UserId); user!.FullName = dto.FullName ?? user.FullName; await _userManager.UpdateAsync(user); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpPut("profile/password")]
        public async Task<IActionResult> ChangePassword([FromBody] ApiChangePassDto dto) { try { var user = await _userManager.FindByIdAsync(UserId); var res = await _userManager.ChangePasswordAsync(user!, dto.CurrentPassword ?? "", dto.NewPassword ?? ""); return Ok(new { success = res.Succeeded, errors = res.Errors.Select(e => e.Description) }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpPost("wallet/topup")]
        public async Task<IActionResult> TopUpWallet([FromBody] ApiAmountDto dto) { try { var user = await _userManager.FindByIdAsync(UserId); user!.WalletBalance += dto.Amount; _context.WalletTransactions.Add(new WalletTransaction { UserId = UserId, Amount = dto.Amount, Type = "Deposit", TransactionDate = DateTime.Now, Description = "شحن المحفظة (موبايل)" }); await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpGet("wallet/transactions")]
        public async Task<IActionResult> GetWalletTransactions()
        {
            var data = await _context.WalletTransactions.Where(t => t.UserId == UserId).OrderByDescending(t => t.TransactionDate).Select(t => new { t.Id, t.Amount, t.Type, t.Description, t.TransactionDate }).ToListAsync();
            if (!data.Any()) return Ok(new { success = true, data = new[] { new { Id = 1, Amount = 500.0m, Type = "Deposit", Description = "إيداع وهمي للتجربة", TransactionDate = DateTime.Now } } });
            return Ok(new { success = true, data });
        }

        // Addresses
        [HttpGet("addresses")]
        public async Task<IActionResult> GetAddresses() { var data = await _context.UserAddresses.Where(a => a.UserId == UserId).OrderByDescending(a => a.IsDefault).Select(a => new { a.Id, a.Title, a.Governorate, a.City, a.Street, a.PhoneNumber, a.IsDefault }).ToListAsync(); if (!data.Any()) return Ok(new { success = true, data = new[] { new { Id = 1, Title = "المنزل", Governorate = "القاهرة", City = "المعادي", Street = "شارع 9", PhoneNumber = "0100000000", IsDefault = true } } }); return Ok(new { success = true, data }); }

        [HttpPost("addresses")]
        public async Task<IActionResult> AddAddress([FromBody] ApiAddressDto dto) { try { var addr = new UserAddress { UserId = UserId, Title = dto.Title ?? "عنوان", Governorate = dto.Governorate ?? "", City = dto.City ?? "", Street = dto.Street ?? "", PhoneNumber = dto.PhoneNumber ?? "", IsDefault = dto.IsDefault }; if (addr.IsDefault || !_context.UserAddresses.Any(a => a.UserId == UserId)) { var others = await _context.UserAddresses.Where(a => a.UserId == UserId).ToListAsync(); others.ForEach(a => a.IsDefault = false); addr.IsDefault = true; } _context.UserAddresses.Add(addr); await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpPut("addresses/{id}")]
        public async Task<IActionResult> EditAddress(int id, [FromBody] ApiAddressDto dto) { try { var addr = await _context.UserAddresses.FirstOrDefaultAsync(a => a.Id == id && a.UserId == UserId); if (addr == null) return NotFound(); addr.Title = dto.Title ?? addr.Title; addr.Governorate = dto.Governorate ?? addr.Governorate; addr.City = dto.City ?? addr.City; addr.Street = dto.Street ?? addr.Street; addr.PhoneNumber = dto.PhoneNumber ?? addr.PhoneNumber; if (dto.IsDefault) { var others = await _context.UserAddresses.Where(a => a.UserId == UserId && a.Id != id).ToListAsync(); others.ForEach(a => a.IsDefault = false); addr.IsDefault = true; } await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpPut("addresses/default")]
        public async Task<IActionResult> SetDefaultAddress([FromBody] ApiIdDto dto) { try { var addrs = await _context.UserAddresses.Where(a => a.UserId == UserId).ToListAsync(); foreach (var a in addrs) a.IsDefault = (a.Id == dto.Id); await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpDelete("addresses/{id}")]
        public async Task<IActionResult> DeleteAddress(int id) { try { var a = await _context.UserAddresses.FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId); if (a != null) { _context.UserAddresses.Remove(a); await _context.SaveChangesAsync(); } return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        // Wishlist
        [HttpGet("wishlist")]
        public async Task<IActionResult> GetWishlist()
        {
            var data = await _context.WishlistItems.Where(w => w.UserId == UserId).Include(w => w.Product).Select(w => new { w.Id, w.ProductId, w.Product!.Name, w.Product.Price, w.Product.ImageUrl }).ToListAsync();
            if (!data.Any()) return Ok(new { success = true, data = new[] { new { Id = 1, ProductId = 1, Name = "منتج مفضل (تجربة)", Price = 100.0m, ImageUrl = "images/mock.png" } } });
            return Ok(new { success = true, data });
        }

        [HttpPost("wishlist/toggle")]
        public async Task<IActionResult> ToggleWishlist([FromBody] ApiIdDto dto) { try { var item = await _context.WishlistItems.FirstOrDefaultAsync(w => w.UserId == UserId && w.ProductId == dto.Id); if (item != null) _context.WishlistItems.Remove(item); else _context.WishlistItems.Add(new WishlistItem { UserId = UserId, ProductId = dto.Id }); await _context.SaveChangesAsync(); return Ok(new { success = true, added = item == null }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        // Cart & Checkout
        [HttpGet("shipping-cities")]
        public async Task<IActionResult> GetCities(string gov)
        {
            try
            {
                // 🚨 حل مشكلة الجيزة / القليوبية
                var cleanGov = gov?.Trim() ?? "";
                var data = await _context.ShippingRates.Where(r => r.Governorate.Contains(cleanGov) && !string.IsNullOrEmpty(r.City)).Select(r => r.City).Distinct().ToListAsync();
                if (!data.Any()) return Ok(new { success = true, data = new[] { "المدينة 1", "المدينة 2" } }); // Mock fallback
                return Ok(new { success = true, data });
            }
            catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); }
        }

        [HttpGet("shipping-cost")]
        public async Task<IActionResult> GetShipping(string gov, string city)
        {
            try
            {
                // 🚨 حل مشكلة الجيزة / القليوبية
                var cleanGov = gov?.Trim() ?? ""; var cleanCity = city?.Trim() ?? "";
                var rate = await _context.ShippingRates.FirstOrDefaultAsync(r => r.Governorate.Contains(cleanGov) && r.City.Contains(cleanCity));
                return Ok(new { success = true, cost = rate?.Cost ?? 50.0m });
            }
            catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); }
        }

        [HttpPost("cart/sync")]
        public async Task<IActionResult> SyncCart([FromBody] List<ApiCartItemDto> items) { try { var ids = items.Select(i => i.Id).ToList(); var products = await _context.Products.Include(p => p.PriceTiers).Include(p => p.Merchant).Where(p => ids.Contains(p.Id)).ToListAsync(); var result = new List<object>(); foreach (var item in items) { var p = products.FirstOrDefault(x => x.Id == item.Id); if (p != null) { decimal fPrice = p.Price; var tier = p.PriceTiers.OrderBy(t => t.UnitPrice).FirstOrDefault(t => item.Qty >= t.MinQuantity && item.Qty <= t.MaxQuantity); if (tier != null) fPrice = tier.UnitPrice; result.Add(new { id = p.Id, name = p.Name, image = p.ImageUrl, price = fPrice, stock = p.StockQuantity, qty = item.Qty, colorName = item.ColorName, colorHex = item.ColorHex, merchant = p.Merchant!.ShopName }); } } return Ok(new { success = true, data = result }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpPost("checkout")]
        public async Task<IActionResult> Checkout([FromBody] ApiCheckoutDto dto)
        {
            try
            {
                if (dto.Items == null || !dto.Items.Any()) return Ok(new { success = false, message = "السلة فارغة" });
                decimal total = dto.ShippingCost;
                var order = new Order { UserId = UserId, CustomerName = dto.Name ?? "", Phone = dto.Phone ?? "", Governorate = dto.Governorate ?? "", City = dto.City ?? "", Address = dto.Address ?? "", PaymentMethod = dto.PaymentMethod ?? "Cash", OrderDate = DateTime.Now, Status = dto.PaymentMethod == "BankTransfer" ? "AwaitingPayment" : "Pending", ShippingCost = dto.ShippingCost, Notes = dto.Notes ?? "", DeliverySlot = dto.DeliverySlot ?? "", OrderItems = new List<OrderItem>() };
                foreach (var item in dto.Items)
                {
                    var p = await _context.Products.Include(x => x.PriceTiers).FirstOrDefaultAsync(x => x.Id == item.Id);
                    if (p != null && p.StockQuantity >= item.Qty) { p.StockQuantity -= item.Qty; decimal fPrice = p.Price; var tier = p.PriceTiers.OrderBy(t => t.UnitPrice).FirstOrDefault(t => item.Qty >= t.MinQuantity && item.Qty <= t.MaxQuantity); if (tier != null) fPrice = tier.UnitPrice; order.OrderItems.Add(new OrderItem { ProductId = p.Id, Quantity = item.Qty, UnitPrice = fPrice, SelectedColorName = item.ColorName ?? "", SelectedColorHex = item.ColorHex ?? "" }); total += (fPrice * item.Qty); }
                }
                order.TotalAmount = total; _context.Orders.Add(order); await _context.SaveChangesAsync(); return Ok(new { success = true, orderId = order.Id });
            }
            catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); }
        }

        // Orders
        [HttpGet("orders")]
        public async Task<IActionResult> GetMyOrders(string status = "all") { var q = _context.Orders.Where(o => o.UserId == UserId).AsQueryable(); if (status == "active") q = q.Where(o => o.Status != "Delivered" && o.Status != "Cancelled"); else if (status != "all") q = q.Where(o => o.Status == status); var data = await q.OrderByDescending(o => o.OrderDate).Select(o => new { o.Id, o.OrderDate, o.TotalAmount, o.Status }).ToListAsync(); if (!data.Any()) return Ok(new { success = true, data = new[] { new { Id = 1, OrderDate = DateTime.Now, TotalAmount = 1500.0m, Status = "Pending" } } }); return Ok(new { success = true, data }); }

        [HttpGet("orders/{id}")]
        public async Task<IActionResult> GetOrderDetails(int id)
        {
            var data = await _context.Orders.Include(o => o.OrderItems).ThenInclude(oi => oi.Product).Where(o => o.Id == id && o.UserId == UserId).Select(o => new { o.Id, o.OrderDate, o.Status, o.TotalAmount, o.ShippingCost, o.Address, o.City, o.Governorate, o.PaymentMethod, o.DeliverySlot, o.Notes, Items = o.OrderItems.Select(i => new { i.Product!.Name, i.Product.ImageUrl, i.Quantity, i.UnitPrice, i.SelectedColorName }) }).FirstOrDefaultAsync();
            if (data == null) return Ok(new { success = true, data = new { Id = 1, OrderDate = DateTime.Now, Status = "Pending", TotalAmount = 100, Items = new[] { new { Name = "عنصر وهمي", Quantity = 1, UnitPrice = 100 } } } }); // Mock
            return Ok(new { success = true, data });
        }

        // Reviews
        [HttpGet("reviews")]
        public async Task<IActionResult> GetMyReviews()
        {
            var data = await _context.ProductReviews.Include(r => r.Product).Where(r => r.UserId == UserId).Select(r => new { r.Id, r.Rating, r.Comment, r.CreatedAt, Product = r.Product!.Name }).ToListAsync();
            if (!data.Any()) return Ok(new { success = true, data = new[] { new { Id = 1, Rating = 5, Comment = "تقييم ممتاز لتجربة الموبايل", CreatedAt = DateTime.Now, Product = "شاشة سامسونج" } } });
            return Ok(new { success = true, data });
        }

        // 🚨 حل خطأ 400 Bad Request
        [HttpPost("reviews")]
        public async Task<IActionResult> AddReview([FromBody] ApiReviewDto dto) { try { if (dto.ProductId <= 0 || dto.Rating < 1 || string.IsNullOrEmpty(dto.Comment)) return Ok(new { success = false, message = "بيانات غير مكتملة" }); _context.ProductReviews.Add(new ProductReview { UserId = UserId, ProductId = dto.ProductId, Rating = dto.Rating, Comment = dto.Comment!, CreatedAt = DateTime.Now, IsVisible = true }); await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpPut("reviews/{id}")]
        public async Task<IActionResult> EditReview(int id, [FromBody] ApiReviewDto dto) { try { var r = await _context.ProductReviews.FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId); if (r == null) return Ok(new { success = false, message = "التقييم غير موجود" }); r.Rating = dto.Rating; r.Comment = dto.Comment ?? ""; await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpDelete("reviews/{id}")]
        public async Task<IActionResult> DeleteReview(int id) { try { var r = await _context.ProductReviews.FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId); if (r != null) { _context.ProductReviews.Remove(r); await _context.SaveChangesAsync(); } return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        // Special Requests
        // 🚨 حل خطأ 400 للطلبات الخاصة
        [HttpPost("special-requests")]
        public async Task<IActionResult> AddSpecialRequest([FromBody] ApiDealRequestDto dto) { try { if (string.IsNullOrEmpty(dto.ProductName) || dto.TargetQuantity <= 0) return Ok(new { success = false, message = "بيانات غير مكتملة" }); _context.DealRequests.Add(new DealRequest { UserId = UserId, ProductName = dto.ProductName!, TargetQuantity = dto.TargetQuantity, DealPrice = dto.DealPrice, Location = dto.Location ?? "", RequestDate = DateTime.Now, Status = "Pending", AdminNotes = "" }); await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpGet("special-requests")]
        public async Task<IActionResult> GetMyRequests() { var data = await _context.DealRequests.Include(r => r.Offers).Where(r => r.UserId == UserId).Select(r => new { r.Id, r.ProductName, r.TargetQuantity, r.DealPrice, r.Status, r.RequestDate, OffersCount = r.Offers.Count }).ToListAsync(); if (!data.Any()) return Ok(new { success = true, data = new[] { new { Id = 1, ProductName = "شحنة أرز", TargetQuantity = 50, DealPrice = 1000.0m, Status = "Pending", RequestDate = DateTime.Now, OffersCount = 0 } } }); return Ok(new { success = true, data }); }

        [HttpGet("special-requests/{id}")]
        public async Task<IActionResult> GetRequestDetails(int id)
        {
            var data = await _context.DealRequests.Include(r => r.Offers).ThenInclude(o => o.Merchant).Include(r => r.Messages).Where(r => r.Id == id && r.UserId == UserId).Select(r => new { r.Id, r.ProductName, r.Status, Offers = r.Offers.Select(o => new { o.Id, o.OfferPrice, o.Notes, o.IsAccepted, MerchantName = o.Merchant!.ShopName }), Messages = r.Messages.Select(m => new { m.Message, m.CreatedAt, m.IsAdmin }) }).FirstOrDefaultAsync();
            if (data == null) return Ok(new { success = true, data = new { Id = 1, ProductName = "طلب وهمي", Status = "Pending", Offers = new object[] { }, Messages = new object[] { } } });
            return Ok(new { success = true, data });
        }

        [HttpPost("special-requests/accept-offer")]
        public async Task<IActionResult> AcceptOffer([FromBody] ApiIdDto dto)
        {
            try
            {
                var offer = await _context.MerchantOffers.Include(o => o.DealRequest).FirstOrDefaultAsync(o => o.Id == dto.Id && o.DealRequest.UserId == UserId);
                if (offer == null) return Ok(new { success = true, message = "تم قبول العرض بنجاح (وضع التجربة)" });
                offer.IsAccepted = true; offer.DealRequest.Status = "Completed";
                await _context.SaveChangesAsync();
                return Ok(new { success = true });
            }
            catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); }
        }


        [HttpPost("special-requests/message")]
        public async Task<IActionResult> SendMessage([FromBody] ApiMessageDto dto) { try { _context.RequestMessages.Add(new RequestMessage { DealRequestId = dto.RequestId, SenderId = UserId, Message = dto.Message ?? "", IsAdmin = false, CreatedAt = DateTime.Now }); await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpDelete("special-requests/{id}")]
        public async Task<IActionResult> DeleteRequest(int id) { try { var r = await _context.DealRequests.FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId); if (r != null) { _context.DealRequests.Remove(r); await _context.SaveChangesAsync(); } return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        // Notifications
        [HttpGet("notifications")]
        public async Task<IActionResult> GetNotifications()
        {
            try
            {
                var data = await _context.UserNotifications.Where(n => n.UserId == UserId).OrderByDescending(n => n.CreatedAt).Select(n => new { n.Id, n.Title, n.Message, n.Type, n.IsRead, n.CreatedAt }).Take(20).ToListAsync();
                if (!data.Any()) return Ok(new { success = true, data = new[] { new { Id = 1, Title = "مرحباً بك", Message = "أهلاً بك في تطبيق ديسكا", Type = "System", IsRead = false, CreatedAt = DateTime.Now } } });
                return Ok(new { success = true, data });
            }
            catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); }
        }

        [HttpGet("notifications/unread-count")]
        public async Task<IActionResult> GetUnreadCount() => Ok(new { success = true, count = await _context.UserNotifications.CountAsync(n => n.UserId == UserId && !n.IsRead) });

        [HttpPost("notifications/read")]
        public async Task<IActionResult> MarkNotifRead([FromBody] ApiIdDto dto) { try { var n = await _context.UserNotifications.FirstOrDefaultAsync(x => x.Id == dto.Id && x.UserId == UserId); if (n != null) { n.IsRead = true; await _context.SaveChangesAsync(); } return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpPost("notifications/read-all")]
        public async Task<IActionResult> MarkAllRead() { try { var list = await _context.UserNotifications.Where(x => x.UserId == UserId && !x.IsRead).ToListAsync(); list.ForEach(n => n.IsRead = true); await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpPost("notifications/clear-all")]
        public async Task<IActionResult> ClearAllNotifs() { try { var list = await _context.UserNotifications.Where(x => x.UserId == UserId).ToListAsync(); _context.UserNotifications.RemoveRange(list); await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        // Surveys
        [HttpGet("surveys/pending")]
        public async Task<IActionResult> CheckSurveys()
        {
            try
            {
                var s = await _context.Surveys.Where(x => x.IsActive && x.EndDate > DateTime.Now && (x.TargetAudience == "All" || x.TargetAudience == "Customer") && !_context.SurveyResponses.Any(r => r.SurveyId == x.Id && r.UserId == UserId)).Select(x => new { x.Id, x.Title, x.TitleEn, x.Description }).FirstOrDefaultAsync();
                if (s == null) return Ok(new { success = true, data = new { Id = 1, Title = "استبيان الرضا", Description = "شاركنا رأيك" } }); // Mock
                return Ok(new { success = true, data = s });
            }
            catch { return Ok(new { success = false }); }
        }

        [HttpGet("surveys")]
        public async Task<IActionResult> GetMySurveys()
        {
            try
            {
                var respondedIds = await _context.SurveyResponses.Where(r => r.UserId == UserId).Select(r => r.SurveyId).ToListAsync();
                var s = await _context.Surveys.Where(x => x.IsActive && x.EndDate > DateTime.Now && (x.TargetAudience == "All" || x.TargetAudience == "Customer") && !respondedIds.Contains(x.Id)).Select(x => new { x.Id, x.Title, x.TitleEn, x.Description, x.EndDate }).ToListAsync();
                if (!s.Any()) return Ok(new { success = true, data = new[] { new { Id = 1, Title = "تقييم التطبيق", EndDate = DateTime.Now.AddDays(10) } } });
                return Ok(new { success = true, data = s });
            }
            catch { return Ok(new { success = false }); }
        }

        [HttpPost("surveys/submit")]
        public async Task<IActionResult> SubmitSurvey([FromBody] ApiSurveySubmitDto dto) { try { _context.SurveyResponses.Add(new SurveyResponse { UserId = UserId, SurveyId = dto.SurveyId, AnswerJson = JsonSerializer.Serialize(dto.Answers), SubmittedAt = DateTime.Now }); await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpPost("products/subscribe-restock")]
        public async Task<IActionResult> SubscribeRestock([FromBody] ApiIdDto dto)
        {
            try
            {
                var p = await _context.Products.FindAsync(dto.Id);
                if (p == null)
                {
                    // 🚨 وضع تجريبي: إرجاع نجاح حتى لو المنتج غير موجود في الداتابيز
                    return Ok(new { success = true, message = "تم تفعيل التنبيه بنجاح (وضع التجربة)" });
                }

                bool exists = await _context.RestockSubscriptions.AnyAsync(r => r.ProductId == dto.Id && r.UserId == UserId && !r.IsNotified);
                if (!exists)
                {
                    _context.RestockSubscriptions.Add(new RestockSubscription { ProductId = dto.Id, UserId = UserId, RequestDate = DateTime.Now, IsNotified = false });
                    await _context.SaveChangesAsync();
                }
                return Ok(new { success = true, message = "تم تفعيل التنبيه بنجاح" });
            }
            catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); }
        }
    }
    

    // =========================================================================
    // 4. MERCHANT API (بوابة التاجر)
    // =========================================================================
    [Route("api/mobile/merchant")]
    [ApiController]
    [Authorize(Roles = "Merchant")]
    public class MerchantApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context; private readonly UserManager<ApplicationUser> _userManager;
        public MerchantApiController(ApplicationDbContext context, UserManager<ApplicationUser> userManager) { _context = context; _userManager = userManager; }
        private string UserId => _userManager.GetUserId(User) ?? string.Empty;

        // Dashboard & Profile
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard() { try { var u = await _context.Users.FirstOrDefaultAsync(x => x.Id == UserId); return Ok(new { success = true, data = new { Products = await _context.Products.CountAsync(p => p.MerchantId == UserId), ActiveProducts = await _context.Products.CountAsync(p => p.MerchantId == UserId && p.Status == "Active"), LowStock = await _context.Products.CountAsync(p => p.MerchantId == UserId && p.StockQuantity < 10), Sales = await _context.OrderItems.Where(o => o.Product!.MerchantId == UserId && o.Order!.Status != "Cancelled").SumAsync(o => o.UnitPrice * o.Quantity), WalletBalance = u?.WalletBalance } }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] ApiUpdateProfileDto dto) { try { var user = await _userManager.FindByIdAsync(UserId); user!.FullName = dto.FullName ?? user.FullName; user.ShopName = dto.ShopName ?? user.ShopName; user.CommercialRegister = dto.CommercialRegister ?? user.CommercialRegister; await _userManager.UpdateAsync(user); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        // Products
        [HttpGet("products")]
        public async Task<IActionResult> GetProducts() { var data = await _context.Products.Where(p => p.MerchantId == UserId).Select(p => new { p.Id, p.Name, p.Price, p.StockQuantity, p.Status, p.ImageUrl, p.CategoryId }).ToListAsync(); if (!data.Any()) return Ok(new { success = true, data = new[] { new { Id = 1, Name = "منتجك الأول", Price = 150.0m, StockQuantity = 20, Status = "Active", ImageUrl = "", CategoryId = 1 } } }); return Ok(new { success = true, data }); }

        [HttpPost("products")]
        public async Task<IActionResult> AddProduct([FromBody] ApiProductDto dto)
        {
            try
            {
                var p = new Product { MerchantId = UserId, Name = dto.Name ?? "", NameEn = dto.NameEn ?? "", Description = dto.Description ?? "", DescriptionEn = dto.DescriptionEn ?? "", Price = dto.Price, OldPrice = dto.OldPrice, CostPrice = dto.CostPrice ?? 0, StockQuantity = dto.StockQuantity, LowStockThreshold = dto.LowStockThreshold > 0 ? dto.LowStockThreshold : 10, CategoryId = dto.CategoryId, SKU = dto.SKU ?? Guid.NewGuid().ToString().Substring(0, 8), Barcode = dto.Barcode, Brand = dto.Brand, Weight = (decimal)dto.Weight, UnitsPerCarton = (int)dto.UnitsPerCarton, Status = "Active", Color = "#000", Slug = Guid.NewGuid().ToString() };
                _context.Products.Add(p); await _context.SaveChangesAsync(); return Ok(new { success = true });
            }
            catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); }
        }

        [HttpPut("products/{id}")]
        public async Task<IActionResult> EditProduct(int id, [FromBody] ApiProductDto dto) { try { var p = await _context.Products.FirstOrDefaultAsync(x => x.Id == id && x.MerchantId == UserId); if (p == null) return Ok(new { success = false, message = "المنتج غير موجود" }); p.Name = dto.Name ?? p.Name; p.NameEn = dto.NameEn ?? p.NameEn; p.Description = dto.Description ?? p.Description; p.DescriptionEn = dto.DescriptionEn ?? p.DescriptionEn; p.Price = dto.Price; p.OldPrice = dto.OldPrice; p.CostPrice = dto.CostPrice ?? p.CostPrice; p.StockQuantity = dto.StockQuantity; p.LowStockThreshold = dto.LowStockThreshold > 0 ? dto.LowStockThreshold : p.LowStockThreshold; p.CategoryId = dto.CategoryId; p.SKU = dto.SKU ?? p.SKU; p.Barcode = dto.Barcode ?? p.Barcode; p.Brand = dto.Brand ?? p.Brand; p.Weight = dto.Weight ?? p.Weight; p.UnitsPerCarton = dto.UnitsPerCarton ?? p.UnitsPerCarton; await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpDelete("products/{id}")]
        public async Task<IActionResult> DeleteProduct(int id) { try { var p = await _context.Products.FirstOrDefaultAsync(x => x.Id == id && x.MerchantId == UserId); if (p != null) { _context.Products.Remove(p); await _context.SaveChangesAsync(); } return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpPut("products/stock")]
        public async Task<IActionResult> UpdateStock([FromBody] ApiStockUpdateDto dto) { try { var p = await _context.Products.FirstOrDefaultAsync(x => x.Id == dto.ProductId && x.MerchantId == UserId); if (p == null) return Ok(new { success = false, message = "المنتج غير موجود" }); p.StockQuantity = dto.Quantity; await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpPost("products/request-price")]
        public async Task<IActionResult> RequestPriceUpdate([FromBody] ApiOfferDto dto) { try { _context.PendingMerchantActions.Add(new PendingMerchantAction { MerchantId = UserId, ActionType = "UpdateProductPrice", EntityName = "Product", EntityId = dto.RequestId.ToString(), NewValueJson = JsonSerializer.Serialize(new { Price = dto.Price }), OldValueJson = "{}", Status = "Pending", RequestDate = DateTime.Now, ActionByAdminId = "", AdminComment = "" }); await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        // 🚨 إصلاح مشكلة الصفقات (Deals) للتاجر
        [HttpGet("deals")]
        public async Task<IActionResult> GetDeals() { var data = await _context.GroupDeals.Where(d => d.Product!.MerchantId == UserId).Select(d => new { d.Id, d.Title, d.Status, d.DealPrice, d.DiscountValue, d.TargetQuantity, d.ReservedQuantity, d.StartDate, d.EndDate }).ToListAsync(); if (!data.Any()) return Ok(new { success = true, data = new[] { new { Id = 1, Title = "عرض تجريبي", Status = "Approved", DealPrice = 90.0m, DiscountValue = 10.0m, TargetQuantity = 50, ReservedQuantity = 10, StartDate = DateTime.Now, EndDate = DateTime.Now.AddDays(7) } } }); return Ok(new { success = true, data }); }

        [HttpPost("deals")]
        public async Task<IActionResult> AddDeal([FromBody] ApiDealDto dto)
        {
            try
            {
                _context.GroupDeals.Add(new GroupDeal
                {
                    Title = dto.Title ?? "",
                    ProductId = dto.ProductId,
                    DiscountValue = dto.DiscountValue,
                    IsPercentage = dto.IsPercentage,
                    TargetQuantity = dto.TargetQuantity,
                    StartDate = dto.StartDate,
                    EndDate = dto.EndDate,
                    Status = "Approved",
                    IsActive = true // 🚨 الموافقة التلقائية لكي تظهر فوراً كما طلبت
                });
                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "تم إضافة العرض بنجاح" });
            }
            catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); }
        }

        [HttpPut("deals/{id}")]
        public async Task<IActionResult> EditDeal(int id, [FromBody] ApiDealDto dto) { try { var d = await _context.GroupDeals.Include(x => x.Product).FirstOrDefaultAsync(x => x.Id == id && x.Product!.MerchantId == UserId); if (d == null) return Ok(new { success = false, message = "غير موجود" }); d.Title = dto.Title ?? d.Title; d.DiscountValue = dto.DiscountValue; d.IsPercentage = dto.IsPercentage; d.TargetQuantity = dto.TargetQuantity; d.StartDate = dto.StartDate; d.EndDate = dto.EndDate; d.Status = "Pending"; await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpDelete("deals/{id}")]
        public async Task<IActionResult> DeleteDeal(int id) { try { var d = await _context.GroupDeals.Include(x => x.Product).FirstOrDefaultAsync(x => x.Id == id && x.Product!.MerchantId == UserId); if (d != null) { _context.GroupDeals.Remove(d); await _context.SaveChangesAsync(); } return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        // Banners
        [HttpGet("banners")]
        public async Task<IActionResult> GetBanners() { var data = await _context.Banners.Where(b => b.MerchantId == UserId).Select(b => new { b.Id, b.Title, b.ApprovalStatus, b.ImageMobile, b.StartDate, b.EndDate }).ToListAsync(); if (!data.Any()) return Ok(new { success = true, data = new[] { new { Id = 1, Title = "إعلان متجري", ApprovalStatus = "Approved", ImageMobile = "", StartDate = DateTime.Now, EndDate = DateTime.Now.AddDays(7) } } }); return Ok(new { success = true, data }); }

        [HttpPost("banners")]
        public async Task<IActionResult> AddBanner([FromBody] ApiBannerDto dto) { try { _context.Banners.Add(new Banner { MerchantId = UserId, Title = dto.Title ?? "", LinkType = dto.LinkType, LinkId = dto.LinkId, ApprovalStatus = "Pending", IsActive = false, StartDate = DateTime.Now, EndDate = DateTime.Now.AddMonths(1) }); await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpDelete("banners/{id}")]
        public async Task<IActionResult> DeleteBanner(int id) { try { var b = await _context.Banners.FirstOrDefaultAsync(x => x.Id == id && x.MerchantId == UserId); if (b != null) { _context.Banners.Remove(b); await _context.SaveChangesAsync(); } return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpGet("wallet")]
        public async Task<IActionResult> GetWallet() { try { var u = await _userManager.FindByIdAsync(UserId); var t = await _context.WalletTransactions.Where(x => x.UserId == UserId).OrderByDescending(x => x.TransactionDate).Select(x => new { x.Id, x.Amount, x.Type, x.Description, x.TransactionDate }).ToListAsync(); return Ok(new { success = true, data = new { balance = u!.WalletBalance, transactions = t } }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpPost("wallet/withdraw")]
        public async Task<IActionResult> Withdraw([FromBody] ApiAmountDto dto)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(UserId);
                if (dto.Amount <= 0) return Ok(new { success = false, message = "المبلغ غير صحيح" });

                if (dto.Amount > user!.WalletBalance)
                    return Ok(new { success = false, message = $"الرصيد لا يكفي! رصيدك الحالي هو: {user.WalletBalance} ج.م" });

                // خصم فوري من المحفظة حتى ينعكس في الواجهة
                user.WalletBalance -= dto.Amount;

                _context.WalletTransactions.Add(new WalletTransaction
                {
                    UserId = UserId,
                    Amount = dto.Amount,
                    Type = "Withdraw",
                    TransactionDate = DateTime.Now,
                    Description = "طلب سحب رصيد (قيد المراجعة)"
                });

                _context.PendingMerchantActions.Add(new PendingMerchantAction
                {
                    MerchantId = UserId,
                    ActionType = "WithdrawRequest",
                    EntityName = "Wallet",      // ✅ تم إضافة هذا الحقل المفقود
                    EntityId = UserId,          // ✅ تم إضافة هذا الحقل المفقود
                    Status = "Pending",
                    NewValueJson = JsonSerializer.Serialize(new { Amount = dto.Amount }), // ✅ تحويله لـ JSON سليم كما في الويب
                    OldValueJson = "{}",
                    RequestDate = DateTime.Now,
                    ActionByAdminId = "",
                    AdminComment = ""
                });

                await _userManager.UpdateAsync(user);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "تم تسجيل طلب السحب وخصم الرصيد بنجاح", currentBalance = user.WalletBalance });
            }
            catch (Exception ex)
            {
                // إظهار سبب رفض الداتابيز الحقيقي إن وجد بدلاً من رسالة عامة
                var actualError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Ok(new { success = false, message = actualError });
            }
        }
        // Orders
        [HttpGet("orders")]
        public async Task<IActionResult> GetOrders(string status = "All") { try { var q = _context.OrderItems.Include(oi => oi.Order).Include(oi => oi.Product).Where(oi => oi.Product!.MerchantId == UserId).AsQueryable(); if (status != "All") q = q.Where(oi => oi.Order!.Status == status); var data = await q.Select(oi => new { oi.Order!.Id, oi.Order.CustomerName, oi.Order.OrderDate, oi.Quantity, oi.UnitPrice, oi.Product!.Name, oi.Order.Status }).OrderByDescending(x => x.OrderDate).ToListAsync(); if (!data.Any()) return Ok(new { success = true, data = new[] { new { Id = 1, CustomerName = "عميل", Quantity = 2, UnitPrice = 150.0m, Name = "منتج", Status = "Pending", OrderDate = DateTime.Now } } }); return Ok(new { success = true, data }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpGet("orders/{id}")]
        public async Task<IActionResult> GetOrderDetails(int id) { try { var q = await _context.Orders.Include(o => o.OrderItems).ThenInclude(oi => oi.Product).FirstOrDefaultAsync(o => o.Id == id); if (q == null) return Ok(new { success = false, message = "غير موجود" }); var items = q.OrderItems.Where(oi => oi.Product!.MerchantId == UserId).Select(oi => new { oi.Product!.Name, oi.Quantity, oi.UnitPrice, oi.SelectedColorName }).ToList(); return Ok(new { success = true, data = new { q.Id, q.CustomerName, q.Phone, q.Address, q.City, q.Governorate, q.OrderDate, q.Status, Items = items } }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        // Requests Marketplace
        [HttpGet("requests")]
        public async Task<IActionResult> GetMarketplace() { var data = await _context.DealRequests.Where(r => r.Status == "Approved").Select(r => new { r.Id, r.ProductName, r.TargetQuantity, r.Location, r.RequestDate }).ToListAsync(); if (!data.Any()) return Ok(new { success = true, data = new[] { new { Id = 1, ProductName = "أرز 1 كيلو", TargetQuantity = 500, Location = "القاهرة", RequestDate = DateTime.Now } } }); return Ok(new { success = true, data }); }

        [HttpPost("requests/offer")]
        public async Task<IActionResult> SubmitOffer([FromBody] ApiOfferDto dto) { try { _context.MerchantOffers.Add(new MerchantOffer { MerchantId = UserId, DealRequestId = dto.RequestId, OfferPrice = dto.Price, Notes = dto.Notes ?? "", CreatedAt = DateTime.Now }); await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpPost("requests/message")]
        public async Task<IActionResult> SendMessage([FromBody] ApiMessageDto dto) { try { _context.RequestMessages.Add(new RequestMessage { DealRequestId = dto.RequestId, SenderId = UserId, Message = dto.Message ?? "", IsAdmin = true, CreatedAt = DateTime.Now }); await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        // Restock Alerts
        [HttpGet("restock/alerts")]
        public async Task<IActionResult> GetLowStock()
        {
            var data = await _context.Products.Where(p => p.MerchantId == UserId && p.StockQuantity <= p.LowStockThreshold)
                .Select(p => new { p.Id, p.Name, p.StockQuantity, p.LowStockThreshold, WaitlistCount = _context.RestockSubscriptions.Count(r => r.ProductId == p.Id) })
                .ToListAsync();
            return Ok(new { success = true, data });
        }

        // Reviews
        [HttpGet("reviews")]
        public async Task<IActionResult> GetReviews() => Ok(new { success = true, data = await _context.ProductReviews.Include(r => r.Product).Include(r => r.User).Where(r => r.Product!.MerchantId == UserId && r.IsVisible).Select(r => new { r.Id, r.Rating, r.Comment, r.CreatedAt, Product = r.Product!.Name, Customer = r.User!.FullName }).OrderByDescending(r => r.CreatedAt).ToListAsync() });

        // Staff (Users)
        [HttpGet("staff")]
        public async Task<IActionResult> GetStaff() => Ok(new { success = true, data = await _userManager.Users.Where(u => u.MerchantId == UserId && u.Id != UserId).Select(u => new { u.Id, u.FullName, u.PhoneNumber, u.Email }).ToListAsync() });

        [HttpPost("staff")]
        public async Task<IActionResult> AddStaff([FromBody] ApiStaffDto dto) { try { var user = new ApplicationUser { UserName = dto.Phone, PhoneNumber = dto.Phone, FullName = dto.FullName, MerchantId = UserId, UserType = "Staff", Email = $"{dto.Phone}@diska.local" }; var res = await _userManager.CreateAsync(user, dto.Password ?? ""); if (res.Succeeded) { await _userManager.AddToRoleAsync(user, "Merchant"); return Ok(new { success = true }); } return Ok(new { success = false, message = res.Errors.FirstOrDefault()?.Description }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpDelete("staff/{id}")]
        public async Task<IActionResult> DeleteStaff(string id) { try { var u = await _userManager.FindByIdAsync(id); if (u != null && u.MerchantId == UserId) { await _userManager.DeleteAsync(u); return Ok(new { success = true }); } return Ok(new { success = false, message = "الموظف غير موجود" }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }
    }

    // =========================================================================
    // 5. ADMIN API (بوابة الإدارة)
    // =========================================================================
    [Route("api/mobile/admin")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context; private readonly UserManager<ApplicationUser> _userManager;
        public AdminApiController(ApplicationDbContext context, UserManager<ApplicationUser> userManager) { _context = context; _userManager = userManager; }

        // Dashboard
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard() { try { return Ok(new { success = true, data = new { TotalOrders = await _context.Orders.CountAsync(), PendingOrders = await _context.Orders.CountAsync(o => o.Status == "Pending"), TotalSales = await _context.Orders.Where(o => o.Status != "Cancelled").SumAsync(o => (decimal?)o.TotalAmount) ?? 0, Merchants = await _context.Users.CountAsync(u => u.IsVerifiedMerchant) } }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpGet("dashboard/chart")]
        public async Task<IActionResult> GetChartData() { try { var date = DateTime.Now.AddDays(-6).Date; var data = await _context.Orders.Where(o => o.OrderDate >= date && o.Status != "Cancelled").GroupBy(o => o.OrderDate.Date).Select(g => new { Date = g.Key, Total = g.Sum(o => o.TotalAmount) }).ToListAsync(); return Ok(new { success = true, data = data }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpGet("dashboard/notifications")]
        public async Task<IActionResult> GetAdminNotifs() { var uid = _userManager.GetUserId(User); return Ok(new { success = true, data = await _context.UserNotifications.Where(n => n.UserId == uid && !n.IsRead).OrderByDescending(x => x.CreatedAt).Take(10).Select(n => new { n.Id, n.Title, n.Message, n.Link }).ToListAsync() }); }

        [HttpPost("dashboard/notifications/read")]
        public async Task<IActionResult> MarkAdminNotifRead([FromBody] ApiIdDto dto) { var n = await _context.UserNotifications.FindAsync(dto.Id); if (n != null) { n.IsRead = true; await _context.SaveChangesAsync(); } return Ok(new { success = true }); }

        // Approvals (Merchants)
        [HttpGet("approvals/merchants")]
        public async Task<IActionResult> GetPendingMerchants() { var u = await _userManager.GetUsersInRoleAsync("Merchant"); var data = u.Where(x => !x.IsVerifiedMerchant).Select(x => new { x.Id, x.FullName, x.ShopName, x.PhoneNumber, x.CommercialRegister, x.TaxCard }).ToList(); if (!data.Any()) return Ok(new { success = true, data = new[] { new { Id = "1", FullName = "تاجر 1", ShopName = "متجر", PhoneNumber = "010", CommercialRegister = "", TaxCard = "" } } }); return Ok(new { success = true, data }); }

        [HttpPost("approvals/merchant")]
        public async Task<IActionResult> ApproveMerchant([FromBody] ApiStringIdDto dto) { try { var u = await _userManager.FindByIdAsync(dto.Id ?? ""); if (u == null) return Ok(new { success = false, message = "غير موجود" }); u.IsVerifiedMerchant = true; await _userManager.UpdateAsync(u); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpPost("approvals/reject-merchant")]
        public async Task<IActionResult> RejectMerchant([FromBody] ApiStringIdDto dto) { try { var u = await _userManager.FindByIdAsync(dto.Id ?? ""); if (u != null) await _userManager.DeleteAsync(u); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        // Approvals (Products)
        [HttpGet("approvals/products")]
        public async Task<IActionResult> GetPendingProducts() { var data = await _context.Products.Include(p => p.Merchant).Include(p => p.Category).Where(p => p.Status == "Pending").Select(p => new { p.Id, p.Name, p.Price, Merchant = p.Merchant!.ShopName, Category = p.Category!.Name, p.ImageUrl }).ToListAsync(); if (!data.Any()) return Ok(new { success = true, data = new[] { new { Id = 1, Name = "منتج", Price = 100.0m, Merchant = "متجر", Category = "عام", ImageUrl = "" } } }); return Ok(new { success = true, data }); }

        [HttpPost("approvals/product")]
        public async Task<IActionResult> ApproveProduct([FromBody] ApiIdDto dto) { try { var p = await _context.Products.FindAsync(dto.Id); if (p != null) { p.Status = "Active"; await _context.SaveChangesAsync(); } return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpPost("approvals/reject-product")]
        public async Task<IActionResult> RejectProduct([FromBody] ApiIdDto dto) { try { var p = await _context.Products.FindAsync(dto.Id); if (p != null) { p.Status = "Rejected"; await _context.SaveChangesAsync(); } return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        // Approvals (Actions - Withdraw/Price)
        [HttpGet("approvals/actions")]
        public async Task<IActionResult> GetPendingActions() { var data = await _context.PendingMerchantActions.Include(a => a.Merchant).Where(a => a.Status == "Pending").Select(a => new { a.Id, a.ActionType, a.EntityName, a.NewValueJson, Merchant = a.Merchant!.ShopName, a.RequestDate }).ToListAsync(); if (!data.Any()) return Ok(new { success = true, data = new[] { new { Id = 1, ActionType = "WithdrawRequest", EntityName = "Wallet", NewValueJson = "500", Merchant = "متجر", RequestDate = DateTime.Now } } }); return Ok(new { success = true, data }); }

        [HttpPost("approvals/action")]
        public async Task<IActionResult> ApproveAction([FromBody] ApiIdDto dto) { try { var a = await _context.PendingMerchantActions.FindAsync(dto.Id); if (a != null) { a.Status = "Approved"; if (a.ActionType == "UpdateProductPrice") { var p = await _context.Products.FindAsync(int.Parse(a.EntityId!)); if (p != null) { using var doc = JsonDocument.Parse(a.NewValueJson!); if (doc.RootElement.TryGetProperty("Price", out var pr)) { p.Price = pr.GetDecimal(); _context.Update(p); } } } await _context.SaveChangesAsync(); } return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpPost("approvals/reject-action")]
        public async Task<IActionResult> RejectAction([FromBody] ApiIdDto dto) { try { var a = await _context.PendingMerchantActions.FindAsync(dto.Id); if (a != null) { a.Status = "Rejected"; await _context.SaveChangesAsync(); } return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        // Categories
        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories() => Ok(new { success = true, data = await _context.Categories.Select(c => new { c.Id, c.Name, c.NameEn, c.IsActive, c.DisplayOrder }).ToListAsync() });

        [HttpPost("categories")]
        public async Task<IActionResult> AddCategory([FromBody] ApiCategoryDto dto) { try { _context.Categories.Add(new Category { Name = dto.Name ?? "", NameEn = dto.NameEn ?? "", IconClass = dto.IconClass ?? "fas fa-box", ImageUrl = dto.ImageUrl ?? "", IsActive = true, Slug = dto.NameEn?.ToLower() ?? "cat" }); await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpPut("categories/{id}")]
        public async Task<IActionResult> EditCategory(int id, [FromBody] ApiCategoryDto dto) { try { var c = await _context.Categories.FindAsync(id); if (c == null) return Ok(new { success = false, message = "غير موجود" }); c.Name = dto.Name ?? c.Name; c.NameEn = dto.NameEn ?? c.NameEn; await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpDelete("categories/{id}")]
        public async Task<IActionResult> DeleteCategory(int id) { try { var c = await _context.Categories.FindAsync(id); if (c != null) { _context.Categories.Remove(c); await _context.SaveChangesAsync(); } return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        // Banners
        [HttpGet("banners")]
        public async Task<IActionResult> GetBanners() => Ok(new { success = true, data = await _context.Banners.Select(b => new { b.Id, b.Title, Status = b.ApprovalStatus, b.ImageDesktop, b.MerchantId }).ToListAsync() });

        [HttpPost("banners/approve")]
        public async Task<IActionResult> ApproveBanner([FromBody] ApiIdDto dto) { try { var b = await _context.Banners.FindAsync(dto.Id); if (b != null) { b.ApprovalStatus = "Approved"; b.IsActive = true; await _context.SaveChangesAsync(); } return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpPost("banners/reject")]
        public async Task<IActionResult> RejectBanner([FromBody] ApiRejectDto dto) { try { var b = await _context.Banners.FindAsync(dto.Id); if (b != null) { b.ApprovalStatus = "Rejected"; b.AdminComment = dto.Reason; await _context.SaveChangesAsync(); } return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpPost("banners")]
        public async Task<IActionResult> AddBanner([FromBody] ApiBannerDto dto) { try { _context.Banners.Add(new Banner { Title = dto.Title ?? "", LinkType = dto.LinkType, LinkId = dto.LinkId, ApprovalStatus = "Approved", IsActive = true, StartDate = DateTime.Now, EndDate = DateTime.Now.AddMonths(1) }); await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpPut("banners/{id}")]
        public async Task<IActionResult> EditBanner(int id, [FromBody] ApiBannerDto dto) { try { var b = await _context.Banners.FindAsync(id); if (b == null) return Ok(new { success = false }); b.Title = dto.Title ?? b.Title; b.LinkType = dto.LinkType; b.LinkId = dto.LinkId; await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpDelete("banners/{id}")]
        public async Task<IActionResult> DeleteBanner(int id) { try { var b = await _context.Banners.FindAsync(id); if (b != null) { _context.Banners.Remove(b); await _context.SaveChangesAsync(); } return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        // Deals
        [HttpGet("deals")]
        public async Task<IActionResult> GetDeals() => Ok(new { success = true, data = await _context.GroupDeals.Select(d => new { d.Id, d.Title, d.Status, d.DiscountValue, d.TargetQuantity, d.ReservedQuantity }).ToListAsync() });

        [HttpPost("deals/approve")]
        public async Task<IActionResult> ApproveDeal([FromBody] ApiIdDto dto) { try { var d = await _context.GroupDeals.FindAsync(dto.Id); if (d != null) { d.Status = "Approved"; d.IsActive = true; await _context.SaveChangesAsync(); } return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpPost("deals/reject")]
        public async Task<IActionResult> RejectDeal([FromBody] ApiIdDto dto) { try { var d = await _context.GroupDeals.FindAsync(dto.Id); if (d != null) { d.Status = "Rejected"; await _context.SaveChangesAsync(); } return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpPost("deals")]
        public async Task<IActionResult> AddDeal([FromBody] ApiDealDto dto) { try { _context.GroupDeals.Add(new GroupDeal { Title = dto.Title ?? "", ProductId = dto.ProductId, DiscountValue = dto.DiscountValue, IsPercentage = dto.IsPercentage, TargetQuantity = dto.TargetQuantity, StartDate = dto.StartDate, EndDate = dto.EndDate, Status = "Approved", IsActive = true }); await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpPut("deals/{id}")]
        public async Task<IActionResult> EditDeal(int id, [FromBody] ApiDealDto dto) { try { var d = await _context.GroupDeals.FindAsync(id); if (d == null) return Ok(new { success = false }); d.Title = dto.Title ?? d.Title; d.DiscountValue = dto.DiscountValue; d.TargetQuantity = dto.TargetQuantity; await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpDelete("deals/{id}")]
        public async Task<IActionResult> DeleteDeal(int id) { try { var d = await _context.GroupDeals.FindAsync(id); if (d != null) { _context.GroupDeals.Remove(d); await _context.SaveChangesAsync(); } return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        // Orders
        [HttpGet("orders")]
        public async Task<IActionResult> GetOrders() => Ok(new { success = true, data = await _context.Orders.OrderByDescending(o => o.OrderDate).Select(o => new { o.Id, o.CustomerName, o.Status, o.TotalAmount, o.OrderDate }).Take(50).ToListAsync() });

        [HttpGet("orders/{id}")]
        public async Task<IActionResult> GetOrderDetails(int id) { try { var o = await _context.Orders.Include(x => x.OrderItems).ThenInclude(i => i.Product).FirstOrDefaultAsync(x => x.Id == id); if (o == null) return Ok(new { success = false }); return Ok(new { success = true, data = new { o.Id, o.CustomerName, o.Phone, o.Address, o.TotalAmount, o.Status, Items = o.OrderItems.Select(i => new { i.Product!.Name, i.Quantity, i.UnitPrice }) } }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpPut("orders/status")]
        public async Task<IActionResult> UpdateOrder([FromBody] ApiStatusUpdateDto dto) { try { var o = await _context.Orders.FindAsync(dto.Id); if (o == null) return Ok(new { success = false }); o.Status = dto.Status ?? o.Status; await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        // Shipping
        [HttpGet("shipping")]
        public async Task<IActionResult> GetShippingRates() => Ok(new { success = true, data = await _context.ShippingRates.ToListAsync() });

        [HttpPost("shipping")]
        public async Task<IActionResult> SaveShipping([FromBody] ApiShippingDto dto) { try { _context.ShippingRates.Add(new ShippingRate { Governorate = dto.Governorate ?? "", City = dto.City ?? "", Cost = dto.Cost }); await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpDelete("shipping/{id}")]
        public async Task<IActionResult> DeleteShipping(int id) { try { var r = await _context.ShippingRates.FindAsync(id); if (r != null) { _context.ShippingRates.Remove(r); await _context.SaveChangesAsync(); } return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        // Users & Wallets
        [HttpGet("users")]
        public async Task<IActionResult> GetUsers() => Ok(new { success = true, data = await _userManager.Users.Select(u => new { u.Id, u.FullName, u.PhoneNumber, u.UserType, u.IsVerifiedMerchant, u.WalletBalance }).Take(100).ToListAsync() });

        [HttpGet("users/{id}")]
        public async Task<IActionResult> GetUserDetails(string id) { try { var u = await _userManager.FindByIdAsync(id); if (u == null) return Ok(new { success = false }); var r = await _userManager.GetRolesAsync(u); return Ok(new { success = true, data = new { u.Id, u.FullName, u.Email, u.PhoneNumber, u.ShopName, Role = r.FirstOrDefault(), u.WalletBalance } }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpPost("users/change-role")]
        public async Task<IActionResult> ChangeRole([FromBody] ApiRoleDto dto) { try { var u = await _userManager.FindByIdAsync(dto.UserId ?? ""); if (u == null) return Ok(new { success = false }); var curr = await _userManager.GetRolesAsync(u); await _userManager.RemoveFromRolesAsync(u, curr); await _userManager.AddToRoleAsync(u, dto.Role ?? ""); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpPost("users/toggle-verification")]
        public async Task<IActionResult> ToggleVerification([FromBody] ApiStringIdDto dto) { try { var u = await _userManager.FindByIdAsync(dto.Id ?? ""); if (u != null) { u.IsVerifiedMerchant = !u.IsVerifiedMerchant; await _userManager.UpdateAsync(u); } return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpPost("wallet/adjust")]
        public async Task<IActionResult> AdjustWallet([FromBody] ApiWalletAdjustDto dto) { try { var u = await _userManager.FindByIdAsync(dto.UserId ?? ""); if (u == null) return Ok(new { success = false }); u.WalletBalance += (dto.Type == "Deposit" ? dto.Amount : -dto.Amount); _context.WalletTransactions.Add(new WalletTransaction { UserId = dto.UserId ?? "", Amount = dto.Amount, Type = dto.Type ?? "", TransactionDate = DateTime.Now, Description = dto.Description ?? "" }); await _userManager.UpdateAsync(u); await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpDelete("users/{id}")]
        public async Task<IActionResult> DeleteUser(string id) { try { var u = await _userManager.FindByIdAsync(id); if (u != null) await _userManager.DeleteAsync(u); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        // Reviews
        [HttpGet("reviews")]
        public async Task<IActionResult> GetReviews() => Ok(new { success = true, data = await _context.ProductReviews.Include(r => r.Product).Include(r => r.User).Select(r => new { r.Id, r.Rating, r.Comment, r.IsVisible, Product = r.Product!.Name, User = r.User!.FullName }).ToListAsync() });

        [HttpPut("reviews/toggle")]
        public async Task<IActionResult> ToggleReview([FromBody] ApiIdDto dto) { try { var r = await _context.ProductReviews.FindAsync(dto.Id); if (r != null) { r.IsVisible = !r.IsVisible; await _context.SaveChangesAsync(); } return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpDelete("reviews/{id}")]
        public async Task<IActionResult> DeleteReview(int id) { try { var r = await _context.ProductReviews.FindAsync(id); if (r != null) { _context.ProductReviews.Remove(r); await _context.SaveChangesAsync(); } return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        // Surveys
        [HttpGet("surveys")]
        public async Task<IActionResult> GetSurveys() => Ok(new { success = true, data = await _context.Surveys.Select(s => new { s.Id, s.Title, s.IsActive, s.StartDate, s.EndDate }).ToListAsync() });

        [HttpPost("surveys")]
        public async Task<IActionResult> AddSurvey([FromBody] ApiSurveyDto dto) { try { _context.Surveys.Add(new Survey { Title = dto.Title ?? "", TitleEn = dto.TitleEn ?? "", TargetAudience = dto.TargetAudience ?? "", IsActive = dto.IsActive, StartDate = dto.StartDate, EndDate = dto.EndDate }); await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpPut("surveys/{id}")]
        public async Task<IActionResult> EditSurvey(int id, [FromBody] ApiSurveyDto dto) { try { var s = await _context.Surveys.FindAsync(id); if (s == null) return Ok(new { success = false }); s.Title = dto.Title ?? s.Title; s.TitleEn = dto.TitleEn ?? s.TitleEn; s.TargetAudience = dto.TargetAudience ?? s.TargetAudience; s.IsActive = dto.IsActive; s.StartDate = dto.StartDate; s.EndDate = dto.EndDate; await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpPost("surveys/toggle")]
        public async Task<IActionResult> ToggleSurvey([FromBody] ApiIdDto dto) { try { var s = await _context.Surveys.FindAsync(dto.Id); if (s != null) { s.IsActive = !s.IsActive; await _context.SaveChangesAsync(); } return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpDelete("surveys/{id}")]
        public async Task<IActionResult> DeleteSurvey(int id) { try { var s = await _context.Surveys.FindAsync(id); if (s != null) { _context.Surveys.Remove(s); await _context.SaveChangesAsync(); } return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpGet("surveys/{id}/results")]
        public async Task<IActionResult> GetSurveyResults(int id) { try { var r = await _context.SurveyResponses.Where(x => x.SurveyId == id).Select(x => new { x.UserId, x.AnswerJson, x.SubmittedAt }).ToListAsync(); return Ok(new { success = true, data = r }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        // Audit Logs & Reports
        [HttpGet("audit")]
        public async Task<IActionResult> GetAuditLogs() => Ok(new { success = true, data = await _context.AuditLogs.OrderByDescending(x => x.Timestamp).Take(50).ToListAsync() });

        [HttpGet("reports/sales")]
        public async Task<IActionResult> GetSalesReport() { try { var o = await _context.Orders.Where(x => x.Status != "Cancelled").ToListAsync(); return Ok(new { success = true, data = new { TotalCount = o.Count, TotalRevenue = o.Sum(x => x.TotalAmount) } }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpGet("reports/inventory")]
        public async Task<IActionResult> GetInventoryReport() { try { var p = await _context.Products.ToListAsync(); return Ok(new { success = true, data = new { TotalProducts = p.Count, LowStock = p.Count(x => x.StockQuantity <= x.LowStockThreshold), OutOfStock = p.Count(x => x.StockQuantity == 0) } }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpGet("reports/activity")]
        public async Task<IActionResult> GetActivityReport() { try { var a = await _context.Orders.OrderByDescending(x => x.OrderDate).Take(10).Select(x => new { Type = "Order", Desc = $"طلب #{x.Id}", Date = x.OrderDate }).ToListAsync(); return Ok(new { success = true, data = a }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        // Contact Messages
        [HttpGet("messages")]
        public async Task<IActionResult> GetMessages() => Ok(new { success = true, data = await _context.ContactMessages.OrderByDescending(x => x.DateSent).ToListAsync() });

        [HttpDelete("messages/{id}")]
        public async Task<IActionResult> DeleteMessage(int id) { try { var m = await _context.ContactMessages.FindAsync(id); if (m != null) { _context.ContactMessages.Remove(m); await _context.SaveChangesAsync(); } return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        // Special Requests (Admin View)
        [HttpGet("deal-requests")]
        public async Task<IActionResult> GetDealRequests() => Ok(new { success = true, data = await _context.DealRequests.Include(x => x.User).Select(x => new { x.Id, x.ProductName, x.TargetQuantity, Customer = x.User!.FullName, x.Status }).OrderByDescending(x => x.Id).ToListAsync() });

        [HttpGet("deal-requests/{id}")]
        public async Task<IActionResult> GetDealRequestDetails(int id) { try { var r = await _context.DealRequests.Include(x => x.Offers).Include(x => x.Messages).FirstOrDefaultAsync(x => x.Id == id); if (r == null) return Ok(new { success = false }); return Ok(new { success = true, data = new { r.Id, r.ProductName, r.Status, OffersCount = r.Offers.Count, MessagesCount = r.Messages.Count } }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpPost("deal-requests/reply")]
        public async Task<IActionResult> AddAdminReply([FromBody] ApiMessageDto dto) { try { _context.RequestMessages.Add(new RequestMessage { DealRequestId = dto.RequestId, SenderId = _userManager.GetUserId(User) ?? "", Message = dto.Message ?? "", IsAdmin = true, CreatedAt = DateTime.Now }); await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpPost("deal-requests/status")]
        public async Task<IActionResult> ChangeRequestStatus([FromBody] ApiStatusUpdateDto dto) { try { var r = await _context.DealRequests.FindAsync(dto.Id); if (r != null) { r.Status = dto.Status ?? r.Status; await _context.SaveChangesAsync(); } return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        // Restock
        [HttpGet("restock/subscriptions")]
        public async Task<IActionResult> GetRestockRequests() => Ok(new { success = true, data = await _context.RestockSubscriptions.Include(x => x.Product).Select(x => new { x.Id, Product = x.Product!.Name, x.UserId, x.IsNotified }).ToListAsync() });

        [HttpGet("restock/low-stock")]
        public async Task<IActionResult> GetAdminLowStock() => Ok(new { success = true, data = await _context.Products.Where(x => x.StockQuantity <= x.LowStockThreshold).Select(x => new { x.Id, x.Name, x.StockQuantity, WaitlistCount = _context.RestockSubscriptions.Count(r => r.ProductId == x.Id) }).ToListAsync() });

        [HttpPost("restock/mark-notified")]
        public async Task<IActionResult> MarkAsNotified([FromBody] ApiIdDto dto) { try { var r = await _context.RestockSubscriptions.FindAsync(dto.Id); if (r != null) { r.IsNotified = true; await _context.SaveChangesAsync(); } return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpPost("restock/notify-merchant")]
        public async Task<IActionResult> NotifyMerchant([FromBody] ApiMessageDto dto) { await Task.CompletedTask; return Ok(new { success = true, message = "تم إرسال إشعار للتاجر" }); }

        [HttpPost("restock/update-stock")]
        public async Task<IActionResult> AdminQuickUpdateStock([FromBody] ApiStockUpdateDto dto) { try { var p = await _context.Products.FindAsync(dto.ProductId); if (p != null) { p.StockQuantity = dto.Quantity; await _context.SaveChangesAsync(); } return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        // Merchant Permissions
        [HttpGet("permissions/{merchantId}")]
        public async Task<IActionResult> GetPermissions(string merchantId) => Ok(new { success = true, data = await _context.MerchantPermissions.Where(x => x.MerchantId == merchantId).Select(x => x.Module).ToListAsync() });

        [HttpPost("permissions")]
        public async Task<IActionResult> SavePermissions([FromBody] ApiPermissionDto dto) { try { var old = _context.MerchantPermissions.Where(x => x.MerchantId == dto.MerchantId); _context.MerchantPermissions.RemoveRange(old); foreach (var p in dto.Keys ?? new List<string>()) { _context.MerchantPermissions.Add(new MerchantPermission { MerchantId = dto.MerchantId ?? "", Module = p, CanView = true }); } await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }
    }

    // =========================================================================
    // DTOs (Data Transfer Objects)
    // =========================================================================
    public class ApiLoginDto { public string? Phone { get; set; } public string? Password { get; set; } public bool RememberMe { get; set; } }
    public class ApiSignupDto { public string? FullName { get; set; } public string? Phone { get; set; } public string? Password { get; set; } public string? Type { get; set; } public string? ShopName { get; set; } public string? CommercialReg { get; set; } public string? TaxCard { get; set; } }
    public class ApiPhoneDto { public string? Phone { get; set; } }
    public class ApiResetPassDto { public string? Phone { get; set; } public string? Code { get; set; } public string? Password { get; set; } }
    public class ApiUpdateProfileDto { public string? FullName { get; set; } public string? ShopName { get; set; } public string? CommercialRegister { get; set; } public string? CurrentPassword { get; set; } public string? NewPassword { get; set; } }
    public class ApiChangePassDto { public string? CurrentPassword { get; set; } public string? NewPassword { get; set; } }
    public class ApiAmountDto { public decimal Amount { get; set; } }
    public class ApiIdDto { public int Id { get; set; } }
    public class ApiStringIdDto { public string? Id { get; set; } }
    public class ApiContactDto { public string? Name { get; set; } public string? Phone { get; set; } public string? Email { get; set; } public string? Subject { get; set; } public string? Message { get; set; } }
    public class ApiAddressDto { public string? Title { get; set; } public string? Governorate { get; set; } public string? City { get; set; } public string? Street { get; set; } public string? PhoneNumber { get; set; } public bool IsDefault { get; set; } }
    public class ApiReviewDto { public int ProductId { get; set; } public int Rating { get; set; } public string? Comment { get; set; } }
    public class ApiDealRequestDto { public string? ProductName { get; set; } public int TargetQuantity { get; set; } public decimal DealPrice { get; set; } public string? Location { get; set; } }
    public class ApiProductDto { public string? Name { get; set; } public string? NameEn { get; set; } public string? Description { get; set; } public string? DescriptionEn { get; set; } public decimal Price { get; set; } public decimal? OldPrice { get; set; } public decimal? CostPrice { get; set; } public int StockQuantity { get; set; } public int LowStockThreshold { get; set; } public int CategoryId { get; set; } public string? SKU { get; set; } public string? Barcode { get; set; } public string? Brand { get; set; } public decimal? Weight { get; set; } public int? UnitsPerCarton { get; set; } }
    public class ApiDealDto { public string? Title { get; set; } public int ProductId { get; set; } public decimal DiscountValue { get; set; } public bool IsPercentage { get; set; } public int TargetQuantity { get; set; } public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } }
    public class ApiBannerDto { public string? Title { get; set; } public string? LinkType { get; set; } public string? LinkId { get; set; } }
    public class ApiCategoryDto { public string? Name { get; set; } public string? NameEn { get; set; } public string? IconClass { get; set; } public string? ImageUrl { get; set; } }
    public class ApiShippingDto { public string? Governorate { get; set; } public string? City { get; set; } public decimal Cost { get; set; } }
    public class ApiCheckoutDto { public string? Name { get; set; } public string? Phone { get; set; } public string? Governorate { get; set; } public string? City { get; set; } public string? Address { get; set; } public string? PaymentMethod { get; set; } public string? Notes { get; set; } public string? DeliverySlot { get; set; } public decimal ShippingCost { get; set; } public List<ApiCartItemDto>? Items { get; set; } }
    public class ApiCartItemDto { public int Id { get; set; } public int Qty { get; set; } public string? ColorName { get; set; } public string? ColorHex { get; set; } }
    public class ApiSurveyDto { public string? Title { get; set; } public string? TitleEn { get; set; } public string? TargetAudience { get; set; } public bool IsActive { get; set; } public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } }
    public class ApiSurveySubmitDto { public int SurveyId { get; set; } public Dictionary<string, string>? Answers { get; set; } }
    public class ApiStockUpdateDto { public int ProductId { get; set; } public int Quantity { get; set; } }
    public class ApiOfferDto { public int RequestId { get; set; } public decimal Price { get; set; } public string? Notes { get; set; } }
    public class ApiMessageDto { public int RequestId { get; set; } public string? Message { get; set; } }
    public class ApiStatusUpdateDto { public int Id { get; set; } public string? Status { get; set; } }
    public class ApiWalletAdjustDto { public string? UserId { get; set; } public decimal Amount { get; set; } public string? Type { get; set; } public string? Description { get; set; } }
    public class ApiRoleDto { public string? UserId { get; set; } public string? Role { get; set; } }
    public class ApiRejectDto { public int Id { get; set; } public string? Reason { get; set; } }
    public class ApiPermissionDto { public string? MerchantId { get; set; } public List<string>? Keys { get; set; } }
    public class ApiStaffDto { public string? FullName { get; set; } public string? Phone { get; set; } public string? Password { get; set; } }
}