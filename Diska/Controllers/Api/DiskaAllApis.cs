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
    // 1. AUTHENTICATION API (المصادقة)
    // =========================================================================
    [Route("api/mobile/auth")]
    [ApiController]
    public class AuthApiController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ISmsService _smsService; // ✅ إضافة خدمة الرسائل

        public AuthApiController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, ISmsService smsService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _smsService = smsService; // ✅ الحقن
        }

        // 🚨 الدالة الجديدة لإرسال كود الـ OTP
        [HttpPost("send-otp")]
        public async Task<IActionResult> SendOtp([FromBody] ApiPhoneDto model)
        {
            if (string.IsNullOrEmpty(model.Phone))
                return BadRequest(new { success = false, message = "رقم الهاتف مطلوب" });

            // توليد كود عشوائي من 6 أرقام
            Random rand = new Random();
            string otpCode = rand.Next(100000, 999999).ToString();

            // استدعاء خدمة WhySMS (الدالة التي أضفناها ستتكفل بتنظيف الرقم)
            bool isSent = await _smsService.SendOtpAsync(model.Phone, otpCode);

            if (isSent)
            {
                // نرجع الـ OTP مؤقتاً في الـ JSON لتسهيل الاختبار من بوستمان (في الإنتاج احذفه)
                return Ok(new { success = true, message = "تم إرسال رمز التحقق بنجاح.", test_otp = otpCode });
            }

            return Ok(new { success = false, message = "حدث خطأ أثناء إرسال الرسالة." });
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

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ApiPhoneDto model)
        {
            var user = await _userManager.FindByNameAsync(model.Phone!);
            if (user == null) return Ok(new { success = true, message = "إذا كان الرقم صحيحاً، سيتم إرسال كود التحقق." });

            var code = await _userManager.GeneratePasswordResetTokenAsync(user);

            // إرسال كود إعادة التعيين في رسالة
            await _smsService.SendSmsAsync(model.Phone, $"كود استعادة كلمة المرور الخاص بك هو: {code.Substring(0, 6)}");

            return Ok(new { success = true, message = "تم إرسال كود الاستعادة" });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ApiResetPassDto model)
        {
            var user = await _userManager.FindByNameAsync(model.Phone!);
            if (user == null) return NotFound();
            var result = await _userManager.ResetPasswordAsync(user, model.Code!, model.Password!);
            return result.Succeeded ? Ok(new { success = true }) : BadRequest(new { success = false });
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout() { await _signInManager.SignOutAsync(); return Ok(new { success = true }); }
    }

    // =========================================================================
    // 2. PUBLIC API (عام)
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
                var data = await _context.Categories
                    .Where(c => c.IsActive)
                    .Select(c => new { c.Id, c.Name, c.NameEn, c.ImageUrl, c.IconClass })
                    .ToListAsync();
                return Ok(new { success = true, data = data });
            }
            catch (Exception ex) { return Ok(new { success = false, message = "خطأ قاعدة بيانات", errorDetail = ex.Message }); }
        }

        [HttpGet("products")]
        public async Task<IActionResult> GetProducts(string? query, int? categoryId)
        {
            try
            {
                var q = _context.Products.Where(p => p.Status == "Active" && p.StockQuantity > 0).AsQueryable();
                if (categoryId.HasValue) q = q.Where(p => p.CategoryId == categoryId);
                if (!string.IsNullOrEmpty(query)) q = q.Where(p => p.Name.Contains(query) || p.SKU.Contains(query));
                return Ok(new { success = true, data = await q.Select(p => new { p.Id, p.Name, p.Price, p.OldPrice, p.ImageUrl, p.StockQuantity }).Take(50).ToListAsync() });
            }
            catch (Exception ex) { return Ok(new { success = false, message = "خطأ", errorDetail = ex.Message }); }
        }

        [HttpGet("deals")]
        public async Task<IActionResult> GetDeals() => Ok(new { success = true, data = await _context.GroupDeals.Where(d => d.IsActive && d.EndDate > DateTime.Now).Include(d => d.Product).Select(d => new { d.Id, d.Title, d.DiscountValue, Product = d.Product!.Name }).ToListAsync() });

        [HttpGet("banners")]
        public async Task<IActionResult> GetBanners() => Ok(new { success = true, data = await _context.Banners.Where(b => b.IsActive && b.EndDate > DateTime.Now).Select(b => new { b.Id, b.Title, b.ImageMobile, b.LinkId, b.LinkType }).ToListAsync() });

        [HttpPost("contact")]
        public async Task<IActionResult> ContactUs([FromBody] ContactMessage model)
        {
            model.DateSent = DateTime.Now;
            _context.ContactMessages.Add(model);
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }
    }

    // =========================================================================
    // 3. CUSTOMER API (بوابة العميل)
    // =========================================================================
    [Route("api/mobile/customer")]
    [ApiController]
    [Authorize] // يجب تسجيل الدخول
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
        public async Task<IActionResult> GetProfile() => Ok(new { success = true, data = await _context.Users.Where(u => u.Id == UserId).Select(u => new { u.FullName, u.PhoneNumber, u.WalletBalance }).FirstOrDefaultAsync() });

        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] ApiUpdateProfileDto dto)
        {
            var user = await _userManager.FindByIdAsync(UserId);
            user!.FullName = dto.FullName; await _userManager.UpdateAsync(user);
            if (!string.IsNullOrEmpty(dto.NewPassword)) await _userManager.ChangePasswordAsync(user, dto.CurrentPassword!, dto.NewPassword);
            return Ok(new { success = true });
        }

        [HttpPost("wallet/topup")]
        public async Task<IActionResult> TopUpWallet([FromBody] ApiAmountDto dto)
        {
            var user = await _userManager.FindByIdAsync(UserId); user!.WalletBalance += dto.Amount;
            _context.WalletTransactions.Add(new WalletTransaction { UserId = UserId, Amount = dto.Amount, Type = "Deposit", TransactionDate = DateTime.Now });
            await _context.SaveChangesAsync(); return Ok(new { success = true });
        }

        [HttpGet("addresses")]
        public async Task<IActionResult> GetAddresses() => Ok(new { success = true, data = await _context.UserAddresses.Where(a => a.UserId == UserId).ToListAsync() });

        [HttpPost("addresses")]
        public async Task<IActionResult> AddAddress([FromBody] UserAddress addr) { addr.UserId = UserId; _context.UserAddresses.Add(addr); await _context.SaveChangesAsync(); return Ok(new { success = true }); }

        [HttpGet("wishlist")]
        public async Task<IActionResult> GetWishlist() => Ok(new { success = true, data = await _context.WishlistItems.Where(w => w.UserId == UserId).Include(w => w.Product).Select(w => new { w.ProductId, w.Product!.Name, w.Product.Price, w.Product.ImageUrl }).ToListAsync() });

        [HttpPost("wishlist/toggle")]
        public async Task<IActionResult> ToggleWishlist([FromBody] ApiIdDto dto)
        {
            var item = await _context.WishlistItems.FirstOrDefaultAsync(w => w.UserId == UserId && w.ProductId == dto.Id);
            if (item != null) _context.WishlistItems.Remove(item); else _context.WishlistItems.Add(new WishlistItem { UserId = UserId, ProductId = dto.Id });
            await _context.SaveChangesAsync(); return Ok(new { success = true, added = item == null });
        }

        [HttpPost("reviews")]
        public async Task<IActionResult> AddReview([FromBody] ProductReview rev) { rev.UserId = UserId; rev.CreatedAt = DateTime.Now; rev.IsVisible = true; _context.ProductReviews.Add(rev); await _context.SaveChangesAsync(); return Ok(new { success = true }); }

        [HttpGet("shipping-cost")]
        public IActionResult GetShipping(string gov, string city) => Ok(new { success = true, cost = _shippingService.CalculateCost(gov, city) });

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

        [HttpPost("special-requests")]
        public async Task<IActionResult> AddSpecialRequest([FromBody] DealRequest req) { req.UserId = UserId; req.RequestDate = DateTime.Now; req.Status = "Pending"; _context.DealRequests.Add(req); await _context.SaveChangesAsync(); return Ok(new { success = true }); }

        [HttpPost("special-requests/accept-offer")]
        public async Task<IActionResult> AcceptOffer([FromBody] ApiIdDto dto)
        {
            var offer = await _context.MerchantOffers.Include(o => o.DealRequest).FirstOrDefaultAsync(o => o.Id == dto.Id && o.DealRequest.UserId == UserId);
            if (offer == null) return NotFound(); offer.IsAccepted = true; offer.DealRequest.Status = "Completed"; await _context.SaveChangesAsync(); return Ok(new { success = true });
        }

        [HttpGet("notifications")]
        public async Task<IActionResult> GetNotifications() => Ok(new { success = true, data = await _context.UserNotifications.Where(n => n.UserId == UserId).OrderByDescending(n => n.CreatedAt).Take(20).ToListAsync() });

        [HttpPost("surveys/submit")]
        public async Task<IActionResult> SubmitSurvey([FromBody] ApiSurveySubmitDto dto)
        {
            _context.SurveyResponses.Add(new SurveyResponse { UserId = UserId, SurveyId = dto.SurveyId, AnswerJson = JsonSerializer.Serialize(dto.Answers), SubmittedAt = DateTime.Now });
            await _context.SaveChangesAsync(); return Ok(new { success = true });
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
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        public MerchantApiController(ApplicationDbContext context, UserManager<ApplicationUser> userManager) { _context = context; _userManager = userManager; }
        private string UserId => _userManager.GetUserId(User) ?? string.Empty;

        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard() => Ok(new { success = true, Products = await _context.Products.CountAsync(p => p.MerchantId == UserId), Sales = await _context.OrderItems.Where(o => o.Product!.MerchantId == UserId && o.Order!.Status != "Cancelled").SumAsync(o => o.UnitPrice * o.Quantity) });

        [HttpPost("products")]
        public async Task<IActionResult> AddProduct([FromBody] Product p) { p.MerchantId = UserId; p.Status = "Active"; p.Color = p.Color ?? "#000"; _context.Products.Add(p); await _context.SaveChangesAsync(); return Ok(new { success = true }); }

        [HttpPut("products/stock")]
        public async Task<IActionResult> UpdateStock([FromBody] ApiStockUpdateDto dto)
        {
            var p = await _context.Products.FirstOrDefaultAsync(x => x.Id == dto.ProductId && x.MerchantId == UserId);
            if (p == null) return NotFound(); p.StockQuantity = dto.Quantity; await _context.SaveChangesAsync(); return Ok(new { success = true });
        }

        [HttpPost("deals")]
        public async Task<IActionResult> AddDeal([FromBody] GroupDeal d) { d.Status = "Pending"; _context.GroupDeals.Add(d); await _context.SaveChangesAsync(); return Ok(new { success = true }); }

        [HttpPost("banners")]
        public async Task<IActionResult> AddBanner([FromBody] Banner b) { b.MerchantId = UserId; b.ApprovalStatus = "Pending"; b.IsActive = false; _context.Banners.Add(b); await _context.SaveChangesAsync(); return Ok(new { success = true }); }

        [HttpPost("wallet/withdraw")]
        public async Task<IActionResult> Withdraw([FromBody] ApiAmountDto dto)
        {
            var user = await _userManager.FindByIdAsync(UserId);
            if (dto.Amount > user!.WalletBalance) return BadRequest(new { success = false, message = "الرصيد لا يكفي" });
            _context.PendingMerchantActions.Add(new PendingMerchantAction { MerchantId = UserId, ActionType = "WithdrawRequest", Status = "Pending", NewValueJson = dto.Amount.ToString(), RequestDate = DateTime.Now });
            await _context.SaveChangesAsync(); return Ok(new { success = true });
        }

        [HttpGet("orders")]
        public async Task<IActionResult> GetOrders() => Ok(new { success = true, data = await _context.OrderItems.Include(oi => oi.Order).Include(oi => oi.Product).Where(oi => oi.Product!.MerchantId == UserId).Select(oi => new { oi.Order!.Id, oi.Order.OrderDate, oi.Quantity, oi.UnitPrice, oi.Product!.Name, oi.Order.Status }).ToListAsync() });

        [HttpPost("requests/offer")]
        public async Task<IActionResult> SubmitOffer([FromBody] ApiOfferDto dto)
        {
            _context.MerchantOffers.Add(new MerchantOffer { MerchantId = UserId, DealRequestId = dto.RequestId, OfferPrice = dto.Price, Notes = dto.Notes!, CreatedAt = DateTime.Now });
            await _context.SaveChangesAsync(); return Ok(new { success = true });
        }
    }

    // =========================================================================
    // 5. ADMIN API (بوابة الإدارة)
    // =========================================================================
    [Route("api/mobile/admin")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        public AdminApiController(ApplicationDbContext context, UserManager<ApplicationUser> userManager) { _context = context; _userManager = userManager; }

        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard() => Ok(new { success = true, TotalOrders = await _context.Orders.CountAsync(), TotalSales = await _context.Orders.Where(o => o.Status != "Cancelled").SumAsync(o => (decimal?)o.TotalAmount) ?? 0, Merchants = await _context.Users.CountAsync(u => u.IsVerifiedMerchant) });

        [HttpPost("categories")]
        public async Task<IActionResult> AddCategory([FromBody] Category c) { _context.Categories.Add(c); await _context.SaveChangesAsync(); return Ok(new { success = true }); }

        [HttpPut("orders/status")]
        public async Task<IActionResult> UpdateOrder([FromBody] ApiStatusUpdateDto dto)
        {
            var o = await _context.Orders.FindAsync(dto.Id); if (o == null) return NotFound(); o.Status = dto.Status!; await _context.SaveChangesAsync(); return Ok(new { success = true });
        }

        [HttpPost("approvals/merchant")]
        public async Task<IActionResult> ApproveMerchant([FromBody] ApiStringIdDto dto)
        {
            var u = await _userManager.FindByIdAsync(dto.Id!); if (u == null) return NotFound(); u.IsVerifiedMerchant = true; await _userManager.UpdateAsync(u); return Ok(new { success = true });
        }

        [HttpPost("approvals/product")]
        public async Task<IActionResult> ApproveProduct([FromBody] ApiIdDto dto) { var p = await _context.Products.FindAsync(dto.Id); if (p != null) { p.Status = "Active"; await _context.SaveChangesAsync(); } return Ok(new { success = true }); }

        [HttpPost("approvals/action")]
        public async Task<IActionResult> ApproveAction([FromBody] ApiIdDto dto) { var a = await _context.PendingMerchantActions.FindAsync(dto.Id); if (a != null) { a.Status = "Approved"; await _context.SaveChangesAsync(); } return Ok(new { success = true }); }

        [HttpPost("shipping")]
        public async Task<IActionResult> SaveShipping([FromBody] ShippingRate r) { _context.ShippingRates.Update(r); await _context.SaveChangesAsync(); return Ok(new { success = true }); }

        [HttpPost("wallet/adjust")]
        public async Task<IActionResult> AdjustWallet([FromBody] ApiWalletAdjustDto dto)
        {
            var u = await _userManager.FindByIdAsync(dto.UserId!); if (u == null) return NotFound();
            u.WalletBalance += (dto.Type == "Deposit" ? dto.Amount : -dto.Amount);
            _context.WalletTransactions.Add(new WalletTransaction { UserId = dto.UserId!, Amount = dto.Amount, Type = dto.Type!, TransactionDate = DateTime.Now, Description = dto.Description! });
            await _userManager.UpdateAsync(u); await _context.SaveChangesAsync(); return Ok(new { success = true });
        }

        [HttpPut("reviews/toggle")]
        public async Task<IActionResult> ToggleReview([FromBody] ApiIdDto dto) { var r = await _context.ProductReviews.FindAsync(dto.Id); if (r != null) { r.IsVisible = !r.IsVisible; await _context.SaveChangesAsync(); } return Ok(new { success = true }); }

        [HttpPost("surveys/toggle")]
        public async Task<IActionResult> ToggleSurvey([FromBody] ApiIdDto dto) { var s = await _context.Surveys.FindAsync(dto.Id); if (s != null) { s.IsActive = !s.IsActive; await _context.SaveChangesAsync(); } return Ok(new { success = true }); }

        [HttpDelete("users")]
        public async Task<IActionResult> DeleteUser([FromBody] ApiStringIdDto dto) { var u = await _userManager.FindByIdAsync(dto.Id!); if (u != null) await _userManager.DeleteAsync(u); return Ok(new { success = true }); }
    }

    // =========================================================================
    // DTOs
    // =========================================================================
    public class ApiLoginDto { public string? Phone { get; set; } public string? Password { get; set; } public bool RememberMe { get; set; } }
    public class ApiSignupDto { public string? FullName { get; set; } public string? Phone { get; set; } public string? Password { get; set; } public string? Type { get; set; } public string? ShopName { get; set; } public string? CommercialReg { get; set; } public string? TaxCard { get; set; } }
    public class ApiPhoneDto { public string? Phone { get; set; } }
    public class ApiResetPassDto { public string? Phone { get; set; } public string? Code { get; set; } public string? Password { get; set; } }
    public class ApiUpdateProfileDto { public string? FullName { get; set; } public string? CurrentPassword { get; set; } public string? NewPassword { get; set; } }
    public class ApiAmountDto { public decimal Amount { get; set; } }
    public class ApiIdDto { public int Id { get; set; } }
    public class ApiStringIdDto { public string? Id { get; set; } }
    public class ApiCheckoutDto { public string? Name { get; set; } public string? Phone { get; set; } public string? Governorate { get; set; } public string? City { get; set; } public string? Address { get; set; } public string? PaymentMethod { get; set; } public decimal ShippingCost { get; set; } public List<ApiCartItemDto>? Items { get; set; } }
    public class ApiCartItemDto { public int Id { get; set; } public int Qty { get; set; } }
    public class ApiSurveySubmitDto { public int SurveyId { get; set; } public Dictionary<string, string>? Answers { get; set; } }
    public class ApiStockUpdateDto { public int ProductId { get; set; } public int Quantity { get; set; } }
    public class ApiOfferDto { public int RequestId { get; set; } public decimal Price { get; set; } public string? Notes { get; set; } }
    public class ApiStatusUpdateDto { public int Id { get; set; } public string? Status { get; set; } }
    public class ApiWalletAdjustDto { public string? UserId { get; set; } public decimal Amount { get; set; } public string? Type { get; set; } public string? Description { get; set; } }
}