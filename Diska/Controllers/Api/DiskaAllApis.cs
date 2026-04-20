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
        private readonly ISmsService _smsService;

        public AuthApiController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, ISmsService smsService)
        {
            _userManager = userManager; _signInManager = signInManager; _smsService = smsService;
        }

        [HttpPost("send-otp")]
        public async Task<IActionResult> SendOtp([FromBody] ApiPhoneDto model)
        {
            try
            {
                if (string.IsNullOrEmpty(model.Phone)) return BadRequest(new { success = false, message = "رقم الهاتف مطلوب" });
                string otpCode = new Random().Next(100000, 999999).ToString();
                var smsResult = await _smsService.SendOtpAsync(model.Phone, otpCode);
                if (smsResult.IsSuccess) return Ok(new { success = true, message = "تم الإرسال", test_otp = otpCode });
                return Ok(new { success = false, message = "فشل الإرسال", provider_error = smsResult.Message });
            }
            catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] ApiLoginDto model)
        {
            try
            {
                if (string.IsNullOrEmpty(model.Phone) || string.IsNullOrEmpty(model.Password)) return BadRequest(new { success = false, message = "بيانات غير مكتملة" });
                var user = await _userManager.FindByNameAsync(model.Phone) ?? _userManager.Users.FirstOrDefault(u => u.PhoneNumber == model.Phone);
                if (user == null) return Unauthorized(new { success = false, message = "المستخدم غير موجود" });
                if (await _userManager.IsLockedOutAsync(user)) return StatusCode(403, new { success = false, message = "هذا الحساب محظور" });

                var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, true);
                if (result.Succeeded)
                {
                    if (await _userManager.IsInRoleAsync(user, "Merchant") && !user.IsVerifiedMerchant)
                    {
                        await _signInManager.SignOutAsync(); return StatusCode(403, new { success = false, message = "حساب التاجر قيد المراجعة" });
                    }
                    return Ok(new { success = true, data = new { userId = user.Id, name = user.FullName, role = user.UserType, balance = user.WalletBalance } });
                }
                return Unauthorized(new { success = false, message = "كلمة المرور غير صحيحة" });
            }
            catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); }
        }

        [HttpPost("signup")]
        public async Task<IActionResult> Signup([FromBody] ApiSignupDto model)
        {
            try
            {
                if (string.IsNullOrEmpty(model.Phone)) return BadRequest(new { success = false, message = "رقم الهاتف مطلوب" });
                if (await _userManager.FindByNameAsync(model.Phone) != null) return Conflict(new { success = false, message = "مسجل مسبقاً" });

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
                    return Ok(new { success = true, message = "تم الإنشاء" });
                }
                return BadRequest(new { success = false, errors = result.Errors.Select(e => e.Description) });
            }
            catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); }
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ApiPhoneDto model)
        {
            try
            {
                var user = await _userManager.FindByNameAsync(model.Phone!);
                if (user == null) return Ok(new { success = true, message = "تم الإرسال (للأمان)" });
                var code = await _userManager.GeneratePasswordResetTokenAsync(user);
                await _smsService.SendSmsAsync(model.Phone!, $"كود الاستعادة: {code.Substring(0, 6)}");
                return Ok(new { success = true, message = "تم إرسال كود الاستعادة", code = code.Substring(0, 6) });
            }
            catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); }
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ApiResetPassDto model)
        {
            try
            {
                var user = await _userManager.FindByNameAsync(model.Phone!);
                if (user == null) return NotFound();
                var result = await _userManager.ResetPasswordAsync(user, model.Code!, model.Password!);
                return result.Succeeded ? Ok(new { success = true }) : BadRequest(new { success = false });
            }
            catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); }
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
                var data = await _context.Categories.Where(c => c.IsActive && c.ParentId == null).Select(c => new { c.Id, c.Name, c.NameEn, c.ImageUrl, c.IconClass }).ToListAsync();
                return Ok(new { success = true, data = data });
            }
            catch (Exception ex) { return Ok(new { success = false, errorDetail = ex.Message }); }
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
                if (!string.IsNullOrEmpty(query)) q = q.Where(p => p.Name.Contains(query) || p.NameEn.Contains(query) || p.SKU.Contains(query) || p.Description.Contains(query));

                q = sort switch { "price_asc" => q.OrderBy(p => p.Price), "price_desc" => q.OrderByDescending(p => p.Price), _ => q.OrderByDescending(p => p.Id) };

                var data = await q.Select(p => new { p.Id, p.Name, p.Price, p.OldPrice, p.ImageUrl, p.StockQuantity, CategoryName = p.Category!.Name, MerchantName = p.Merchant!.ShopName }).Take(50).ToListAsync();
                return Ok(new { success = true, data = data });
            }
            catch (Exception ex) { return Ok(new { success = false, errorDetail = ex.Message }); }
        }

        [HttpGet("product/{id}")]
        public async Task<IActionResult> GetProductDetails(int id)
        {
            try
            {
                var p = await _context.Products.Include(x => x.Images).Include(x => x.ProductColors).Include(x => x.PriceTiers).Include(x => x.Merchant).FirstOrDefaultAsync(x => x.Id == id && x.Status == "Active");
                if (p == null) return NotFound();
                var reviews = await _context.ProductReviews.Include(r => r.User).Where(r => r.ProductId == id && r.IsVisible).Select(r => new { r.User!.FullName, r.Rating, r.Comment, r.CreatedAt }).ToListAsync();
                return Ok(new { success = true, data = new { p.Id, p.Name, p.NameEn, p.Description, p.Price, p.OldPrice, p.ImageUrl, p.StockQuantity, Merchant = p.Merchant!.ShopName, Images = p.Images.Select(i => i.ImageUrl), Colors = p.ProductColors.Select(c => new { c.ColorName, c.ColorHex }), Tiers = p.PriceTiers.Select(t => new { t.MinQuantity, t.MaxQuantity, t.UnitPrice }), Reviews = reviews } });
            }
            catch (Exception ex) { return Ok(new { success = false, errorDetail = ex.Message }); }
        }

        [HttpGet("deals")]
        public async Task<IActionResult> GetDeals() => Ok(new { success = true, data = await _context.GroupDeals.Where(d => d.IsActive && d.EndDate > DateTime.Now).Include(d => d.Product).Select(d => new { d.Id, d.Title, d.DiscountValue, Product = d.Product!.Name, d.DealPrice, d.EndDate }).ToListAsync() });

        [HttpGet("banners")]
        public async Task<IActionResult> GetBanners() => Ok(new { success = true, data = await _context.Banners.Where(b => b.IsActive && b.EndDate > DateTime.Now && b.ApprovalStatus == "Approved").Select(b => new { b.Id, b.Title, b.ImageMobile, b.LinkId, b.LinkType }).ToListAsync() });

        [HttpPost("contact")]
        public async Task<IActionResult> ContactUs([FromBody] ContactMessage model) { try { model.DateSent = DateTime.Now; _context.ContactMessages.Add(model); await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, errorDetail = ex.Message }); } }
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
        public async Task<IActionResult> UpdateProfile([FromBody] ApiUpdateProfileDto dto) { try { var user = await _userManager.FindByIdAsync(UserId); user!.FullName = dto.FullName; await _userManager.UpdateAsync(user); if (!string.IsNullOrEmpty(dto.NewPassword)) await _userManager.ChangePasswordAsync(user, dto.CurrentPassword!, dto.NewPassword); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpPost("wallet/topup")]
        public async Task<IActionResult> TopUpWallet([FromBody] ApiAmountDto dto) { try { var user = await _userManager.FindByIdAsync(UserId); user!.WalletBalance += dto.Amount; _context.WalletTransactions.Add(new WalletTransaction { UserId = UserId, Amount = dto.Amount, Type = "Deposit", TransactionDate = DateTime.Now, Description = "شحن من الموبايل" }); await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpGet("wallet/transactions")]
        public async Task<IActionResult> GetWalletTransactions() => Ok(new { success = true, data = await _context.WalletTransactions.Where(t => t.UserId == UserId).OrderByDescending(t => t.TransactionDate).ToListAsync() });

        // Addresses
        [HttpGet("addresses")]
        public async Task<IActionResult> GetAddresses() => Ok(new { success = true, data = await _context.UserAddresses.Where(a => a.UserId == UserId).OrderByDescending(a => a.IsDefault).ToListAsync() });

        [HttpPost("addresses")]
        public async Task<IActionResult> AddAddress([FromBody] UserAddress addr) { try { addr.UserId = UserId; if (addr.IsDefault || !_context.UserAddresses.Any(a => a.UserId == UserId)) { var others = await _context.UserAddresses.Where(a => a.UserId == UserId).ToListAsync(); others.ForEach(a => a.IsDefault = false); addr.IsDefault = true; } _context.UserAddresses.Add(addr); await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpPut("addresses/default")]
        public async Task<IActionResult> SetDefaultAddress([FromBody] ApiIdDto dto) { try { var addrs = await _context.UserAddresses.Where(a => a.UserId == UserId).ToListAsync(); foreach (var a in addrs) a.IsDefault = (a.Id == dto.Id); await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpDelete("addresses/{id}")]
        public async Task<IActionResult> DeleteAddress(int id) { try { var a = await _context.UserAddresses.FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId); if (a != null) { _context.UserAddresses.Remove(a); await _context.SaveChangesAsync(); } return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        // Wishlist
        [HttpGet("wishlist")]
        public async Task<IActionResult> GetWishlist() => Ok(new { success = true, data = await _context.WishlistItems.Where(w => w.UserId == UserId).Include(w => w.Product).Select(w => new { w.Id, w.ProductId, w.Product!.Name, w.Product.Price, w.Product.ImageUrl }).ToListAsync() });

        [HttpPost("wishlist/toggle")]
        public async Task<IActionResult> ToggleWishlist([FromBody] ApiIdDto dto) { try { var item = await _context.WishlistItems.FirstOrDefaultAsync(w => w.UserId == UserId && w.ProductId == dto.Id); if (item != null) _context.WishlistItems.Remove(item); else _context.WishlistItems.Add(new WishlistItem { UserId = UserId, ProductId = dto.Id }); await _context.SaveChangesAsync(); return Ok(new { success = true, added = item == null }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        // Cart & Orders
        [HttpGet("shipping-cities")]
        public async Task<IActionResult> GetCities(string gov) => Ok(new { success = true, data = await _context.ShippingRates.Where(r => r.Governorate == gov && !string.IsNullOrEmpty(r.City)).Select(r => r.City).Distinct().ToListAsync() });

        [HttpGet("shipping-cost")]
        public IActionResult GetShipping(string gov, string city) => Ok(new { success = true, cost = _shippingService.CalculateCost(gov, city) });

        [HttpPost("cart/sync")]
        public async Task<IActionResult> SyncCart([FromBody] List<ApiCartItemDto> items) { try { var ids = items.Select(i => i.Id).ToList(); var products = await _context.Products.Include(p => p.PriceTiers).Where(p => ids.Contains(p.Id)).ToListAsync(); var result = new List<object>(); foreach (var item in items) { var p = products.FirstOrDefault(x => x.Id == item.Id); if (p != null) { decimal fPrice = p.Price; var tier = p.PriceTiers.OrderBy(t => t.UnitPrice).FirstOrDefault(t => item.Qty >= t.MinQuantity && item.Qty <= t.MaxQuantity); if (tier != null) fPrice = tier.UnitPrice; result.Add(new { id = p.Id, name = p.Name, image = p.ImageUrl, price = fPrice, stock = p.StockQuantity, qty = item.Qty, colorName = item.ColorName, colorHex = item.ColorHex }); } } return Ok(new { success = true, data = result }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpPost("checkout")]
        public async Task<IActionResult> Checkout([FromBody] ApiCheckoutDto dto)
        {
            try
            {
                if (dto.Items == null || !dto.Items.Any()) return BadRequest(new { success = false, message = "السلة فارغة" });
                decimal total = dto.ShippingCost;
                var order = new Order { UserId = UserId, CustomerName = dto.Name!, Phone = dto.Phone!, Governorate = dto.Governorate!, City = dto.City!, Address = dto.Address!, PaymentMethod = dto.PaymentMethod ?? "Cash", OrderDate = DateTime.Now, Status = dto.PaymentMethod == "BankTransfer" ? "AwaitingPayment" : "Pending", ShippingCost = dto.ShippingCost, Notes = dto.Notes!, DeliverySlot = dto.DeliverySlot!, OrderItems = new List<OrderItem>() };

                foreach (var item in dto.Items)
                {
                    var p = await _context.Products.Include(x => x.PriceTiers).FirstOrDefaultAsync(x => x.Id == item.Id);
                    if (p != null && p.StockQuantity >= item.Qty)
                    {
                        p.StockQuantity -= item.Qty;
                        decimal fPrice = p.Price;
                        var tier = p.PriceTiers.OrderBy(t => t.UnitPrice).FirstOrDefault(t => item.Qty >= t.MinQuantity && item.Qty <= t.MaxQuantity);
                        if (tier != null) fPrice = tier.UnitPrice;
                        order.OrderItems.Add(new OrderItem { ProductId = p.Id, Quantity = item.Qty, UnitPrice = fPrice, SelectedColorName = item.ColorName!, SelectedColorHex = item.ColorHex! });
                        total += (fPrice * item.Qty);
                    }
                }
                order.TotalAmount = total; _context.Orders.Add(order); await _context.SaveChangesAsync(); return Ok(new { success = true, orderId = order.Id });
            }
            catch (Exception ex) { return Ok(new { success = false, errorDetail = ex.Message }); }
        }

        [HttpGet("orders")]
        public async Task<IActionResult> GetMyOrders(string status = "all")
        {
            try { var q = _context.Orders.Where(o => o.UserId == UserId).AsQueryable(); if (status == "active") q = q.Where(o => o.Status != "Delivered" && o.Status != "Cancelled"); else if (status != "all") q = q.Where(o => o.Status == status); return Ok(new { success = true, data = await q.OrderByDescending(o => o.OrderDate).Select(o => new { o.Id, o.OrderDate, o.TotalAmount, o.Status }).ToListAsync() }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); }
        }

        [HttpGet("orders/{id}")]
        public async Task<IActionResult> GetOrderDetails(int id) => Ok(new { success = true, data = await _context.Orders.Include(o => o.OrderItems).ThenInclude(oi => oi.Product).Where(o => o.Id == id && o.UserId == UserId).Select(o => new { o.Id, o.OrderDate, o.Status, o.TotalAmount, o.ShippingCost, o.Address, o.City, o.Governorate, o.PaymentMethod, o.DeliverySlot, o.Notes, Items = o.OrderItems.Select(i => new { i.Product!.Name, i.Product.ImageUrl, i.Quantity, i.UnitPrice, i.SelectedColorName }) }).FirstOrDefaultAsync() });

        // Reviews
        [HttpGet("reviews")]
        public async Task<IActionResult> GetMyReviews() => Ok(new { success = true, data = await _context.ProductReviews.Include(r => r.Product).Where(r => r.UserId == UserId).Select(r => new { r.Id, r.Rating, r.Comment, r.CreatedAt, Product = r.Product!.Name }).ToListAsync() });

        [HttpPost("reviews")]
        public async Task<IActionResult> AddReview([FromBody] ProductReview rev) { try { rev.UserId = UserId; rev.CreatedAt = DateTime.Now; rev.IsVisible = true; _context.ProductReviews.Add(rev); await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpDelete("reviews/{id}")]
        public async Task<IActionResult> DeleteReview(int id) { try { var r = await _context.ProductReviews.FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId); if (r != null) { _context.ProductReviews.Remove(r); await _context.SaveChangesAsync(); } return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        // Special Requests & Deals
        [HttpPost("special-requests")]
        public async Task<IActionResult> AddSpecialRequest([FromBody] DealRequest req) { try { req.UserId = UserId; req.RequestDate = DateTime.Now; req.Status = "Pending"; _context.DealRequests.Add(req); await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpGet("special-requests")]
        public async Task<IActionResult> GetMyRequests() => Ok(new { success = true, data = await _context.DealRequests.Include(r => r.Offers).Where(r => r.UserId == UserId).Select(r => new { r.Id, r.ProductName, r.TargetQuantity, r.DealPrice, r.Status, r.RequestDate, OffersCount = r.Offers.Count }).ToListAsync() });

        [HttpGet("special-requests/{id}")]
        public async Task<IActionResult> GetRequestDetails(int id) => Ok(new { success = true, data = await _context.DealRequests.Include(r => r.Offers).ThenInclude(o => o.Merchant).Include(r => r.Messages).Where(r => r.Id == id && r.UserId == UserId).Select(r => new { r.Id, r.ProductName, r.Status, Offers = r.Offers.Select(o => new { o.Id, o.OfferPrice, o.Notes, o.IsAccepted, MerchantName = o.Merchant!.ShopName }), Messages = r.Messages.Select(m => new { m.Message, m.CreatedAt, m.IsAdmin }) }).FirstOrDefaultAsync() });

        [HttpPost("special-requests/accept-offer")]
        public async Task<IActionResult> AcceptOffer([FromBody] ApiIdDto dto) { try { var offer = await _context.MerchantOffers.Include(o => o.DealRequest).FirstOrDefaultAsync(o => o.Id == dto.Id && o.DealRequest.UserId == UserId); if (offer == null) return NotFound(); offer.IsAccepted = true; offer.DealRequest.Status = "Completed"; await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpPost("special-requests/message")]
        public async Task<IActionResult> SendMessage([FromBody] ApiMessageDto dto) { try { _context.RequestMessages.Add(new RequestMessage { DealRequestId = dto.RequestId, SenderId = UserId, Message = dto.Message!, IsAdmin = false, CreatedAt = DateTime.Now }); await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        // Notifications & Surveys
        [HttpGet("notifications")]
        public async Task<IActionResult> GetNotifications() => Ok(new { success = true, data = await _context.UserNotifications.Where(n => n.UserId == UserId).OrderByDescending(n => n.CreatedAt).Take(20).ToListAsync() });

        [HttpPost("notifications/read")]
        public async Task<IActionResult> MarkNotifRead([FromBody] ApiIdDto dto) { try { var n = await _context.UserNotifications.FirstOrDefaultAsync(x => x.Id == dto.Id && x.UserId == UserId); if (n != null) { n.IsRead = true; await _context.SaveChangesAsync(); } return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpGet("surveys/pending")]
        public async Task<IActionResult> CheckSurveys() { try { var role = await _userManager.IsInRoleAsync(await _userManager.FindByIdAsync(UserId), "Merchant") ? "Merchant" : "Customer"; var s = await _context.Surveys.Where(x => x.IsActive && x.EndDate > DateTime.Now && (x.TargetAudience == "All" || x.TargetAudience == role) && !_context.SurveyResponses.Any(r => r.SurveyId == x.Id && r.UserId == UserId)).Select(x => new { x.Id, x.Title, x.TitleEn }).FirstOrDefaultAsync(); return Ok(new { success = true, data = s }); } catch { return Ok(new { success = false }); } }

        [HttpPost("surveys/submit")]
        public async Task<IActionResult> SubmitSurvey([FromBody] ApiSurveySubmitDto dto) { try { _context.SurveyResponses.Add(new SurveyResponse { UserId = UserId, SurveyId = dto.SurveyId, AnswerJson = JsonSerializer.Serialize(dto.Answers), SubmittedAt = DateTime.Now }); await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }
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

        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard() { try { return Ok(new { success = true, Products = await _context.Products.CountAsync(p => p.MerchantId == UserId), ActiveProducts = await _context.Products.CountAsync(p => p.MerchantId == UserId && p.Status == "Active"), LowStock = await _context.Products.CountAsync(p => p.MerchantId == UserId && p.StockQuantity < 10), Sales = await _context.OrderItems.Where(o => o.Product!.MerchantId == UserId && o.Order!.Status != "Cancelled").SumAsync(o => o.UnitPrice * o.Quantity) }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        // Products
        [HttpGet("products")]
        public async Task<IActionResult> GetMyProducts() => Ok(new { success = true, data = await _context.Products.Where(p => p.MerchantId == UserId).Select(p => new { p.Id, p.Name, p.Price, p.StockQuantity, p.Status, p.ImageUrl }).ToListAsync() });

        [HttpPost("products")] // In real app, image upload needs [FromForm], here using DTO for simplicity
        public async Task<IActionResult> AddProduct([FromBody] Product p) { try { p.MerchantId = UserId; p.Status = "Active"; p.Color = p.Color ?? "#000"; _context.Products.Add(p); await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpPut("products/stock")]
        public async Task<IActionResult> UpdateStock([FromBody] ApiStockUpdateDto dto) { try { var p = await _context.Products.FirstOrDefaultAsync(x => x.Id == dto.ProductId && x.MerchantId == UserId); if (p == null) return NotFound(); p.StockQuantity = dto.Quantity; await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpPost("products/request-price")]
        public async Task<IActionResult> RequestPriceUpdate([FromBody] ApiOfferDto dto) { try { _context.PendingMerchantActions.Add(new PendingMerchantAction { MerchantId = UserId, ActionType = "UpdateProductPrice", EntityName = "Product", EntityId = dto.RequestId.ToString(), NewValueJson = JsonSerializer.Serialize(new { Price = dto.Price }), Status = "Pending", RequestDate = DateTime.Now }); await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        // Deals & Banners
        [HttpGet("deals")]
        public async Task<IActionResult> GetMyDeals() => Ok(new { success = true, data = await _context.GroupDeals.Where(d => d.Product!.MerchantId == UserId).Select(d => new { d.Id, d.Title, d.Status, d.DealPrice, d.DiscountValue }).ToListAsync() });

        [HttpPost("deals")]
        public async Task<IActionResult> AddDeal([FromBody] GroupDeal d) { try { d.Status = "Pending"; _context.GroupDeals.Add(d); await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpGet("banners")]
        public async Task<IActionResult> GetMyBanners() => Ok(new { success = true, data = await _context.Banners.Where(b => b.MerchantId == UserId).Select(b => new { b.Id, b.Title, b.ApprovalStatus, b.ImageMobile }).ToListAsync() });

        [HttpPost("banners")]
        public async Task<IActionResult> AddBanner([FromBody] Banner b) { try { b.MerchantId = UserId; b.ApprovalStatus = "Pending"; b.IsActive = false; _context.Banners.Add(b); await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        // Wallet & Orders
        [HttpGet("wallet")]
        public async Task<IActionResult> GetWallet() { try { var u = await _userManager.FindByIdAsync(UserId); var t = await _context.WalletTransactions.Where(x => x.UserId == UserId).OrderByDescending(x => x.TransactionDate).ToListAsync(); return Ok(new { success = true, balance = u!.WalletBalance, transactions = t }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpPost("wallet/withdraw")]
        public async Task<IActionResult> Withdraw([FromBody] ApiAmountDto dto) { try { var user = await _userManager.FindByIdAsync(UserId); if (dto.Amount > user!.WalletBalance) return BadRequest(new { success = false, message = "الرصيد لا يكفي" }); _context.PendingMerchantActions.Add(new PendingMerchantAction { MerchantId = UserId, ActionType = "WithdrawRequest", Status = "Pending", NewValueJson = dto.Amount.ToString(), RequestDate = DateTime.Now }); await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpGet("orders")]
        public async Task<IActionResult> GetOrders(string status = "All") { try { var q = _context.OrderItems.Include(oi => oi.Order).Include(oi => oi.Product).Where(oi => oi.Product!.MerchantId == UserId).AsQueryable(); if (status != "All") q = q.Where(oi => oi.Order!.Status == status); return Ok(new { success = true, data = await q.Select(oi => new { oi.Order!.Id, oi.Order.CustomerName, oi.Order.OrderDate, oi.Quantity, oi.UnitPrice, oi.Product!.Name, oi.Order.Status }).OrderByDescending(x => x.OrderDate).ToListAsync() }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpGet("orders/{id}")]
        public async Task<IActionResult> GetOrderDetails(int id) { try { var q = await _context.Orders.Include(o => o.OrderItems).ThenInclude(oi => oi.Product).FirstOrDefaultAsync(o => o.Id == id); if (q == null) return NotFound(); var items = q.OrderItems.Where(oi => oi.Product!.MerchantId == UserId).Select(oi => new { oi.Product!.Name, oi.Quantity, oi.UnitPrice, oi.SelectedColorName }).ToList(); return Ok(new { success = true, data = new { q.Id, q.CustomerName, q.Phone, q.Address, q.City, q.Governorate, q.OrderDate, q.Status, Items = items } }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        // Requests Marketplace
        [HttpGet("requests")]
        public async Task<IActionResult> GetMarketplace() => Ok(new { success = true, data = await _context.DealRequests.Where(r => r.Status == "Approved").Select(r => new { r.Id, r.ProductName, r.TargetQuantity, r.Location }).ToListAsync() });

        [HttpPost("requests/offer")]
        public async Task<IActionResult> SubmitOffer([FromBody] ApiOfferDto dto) { try { _context.MerchantOffers.Add(new MerchantOffer { MerchantId = UserId, DealRequestId = dto.RequestId, OfferPrice = dto.Price, Notes = dto.Notes!, CreatedAt = DateTime.Now }); await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpPost("requests/message")]
        public async Task<IActionResult> SendMessage([FromBody] ApiMessageDto dto) { try { _context.RequestMessages.Add(new RequestMessage { DealRequestId = dto.RequestId, SenderId = UserId, Message = dto.Message!, IsAdmin = true /* as merchant */, CreatedAt = DateTime.Now }); await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        // Restock & Reviews
        [HttpGet("restock/alerts")]
        public async Task<IActionResult> GetLowStock() => Ok(new { success = true, data = await _context.Products.Where(p => p.MerchantId == UserId && p.StockQuantity <= p.LowStockThreshold).Select(p => new { p.Id, p.Name, p.StockQuantity, p.LowStockThreshold }).ToListAsync() });

        [HttpGet("reviews")]
        public async Task<IActionResult> GetReviews() => Ok(new { success = true, data = await _context.ProductReviews.Include(r => r.Product).Include(r => r.User).Where(r => r.Product!.MerchantId == UserId && r.IsVisible).Select(r => new { r.Id, r.Rating, r.Comment, r.CreatedAt, Product = r.Product!.Name, Customer = r.User!.FullName }).OrderByDescending(r => r.CreatedAt).ToListAsync() });
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

        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard() { try { return Ok(new { success = true, TotalOrders = await _context.Orders.CountAsync(), PendingOrders = await _context.Orders.CountAsync(o => o.Status == "Pending"), TotalSales = await _context.Orders.Where(o => o.Status != "Cancelled").SumAsync(o => (decimal?)o.TotalAmount) ?? 0, Merchants = await _context.Users.CountAsync(u => u.IsVerifiedMerchant) }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        // Approvals & Actions
        [HttpGet("approvals/merchants")]
        public async Task<IActionResult> GetPendingMerchants() { var u = await _userManager.GetUsersInRoleAsync("Merchant"); return Ok(new { success = true, data = u.Where(x => !x.IsVerifiedMerchant).Select(x => new { x.Id, x.FullName, x.ShopName, x.PhoneNumber }) }); }

        [HttpPost("approvals/merchant")]
        public async Task<IActionResult> ApproveMerchant([FromBody] ApiStringIdDto dto) { try { var u = await _userManager.FindByIdAsync(dto.Id!); if (u == null) return NotFound(); u.IsVerifiedMerchant = true; await _userManager.UpdateAsync(u); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpGet("approvals/products")]
        public async Task<IActionResult> GetPendingProducts() => Ok(new { success = true, data = await _context.Products.Include(p => p.Merchant).Where(p => p.Status == "Pending").Select(p => new { p.Id, p.Name, p.Price, Merchant = p.Merchant!.ShopName }).ToListAsync() });

        [HttpPost("approvals/product")]
        public async Task<IActionResult> ApproveProduct([FromBody] ApiIdDto dto) { try { var p = await _context.Products.FindAsync(dto.Id); if (p != null) { p.Status = "Active"; await _context.SaveChangesAsync(); } return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpGet("approvals/actions")]
        public async Task<IActionResult> GetPendingActions() => Ok(new { success = true, data = await _context.PendingMerchantActions.Include(a => a.Merchant).Where(a => a.Status == "Pending").Select(a => new { a.Id, a.ActionType, a.EntityName, a.NewValueJson, Merchant = a.Merchant!.ShopName, a.RequestDate }).ToListAsync() });

        [HttpPost("approvals/action")]
        public async Task<IActionResult> ApproveAction([FromBody] ApiIdDto dto) { try { var a = await _context.PendingMerchantActions.FindAsync(dto.Id); if (a != null) { a.Status = "Approved"; if (a.ActionType == "UpdateProductPrice") { var p = await _context.Products.FindAsync(int.Parse(a.EntityId)); if (p != null) { using var doc = JsonDocument.Parse(a.NewValueJson); if (doc.RootElement.TryGetProperty("Price", out var pr)) { p.Price = pr.GetDecimal(); _context.Update(p); } } } await _context.SaveChangesAsync(); } return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        // Categories & Banners & Deals
        [HttpPost("categories")]
        public async Task<IActionResult> AddCategory([FromBody] Category c) { try { _context.Categories.Add(c); await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpPost("banners/approve")]
        public async Task<IActionResult> ApproveBanner([FromBody] ApiIdDto dto) { try { var b = await _context.Banners.FindAsync(dto.Id); if (b != null) { b.ApprovalStatus = "Approved"; b.IsActive = true; await _context.SaveChangesAsync(); } return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpPost("deals/approve")]
        public async Task<IActionResult> ApproveDeal([FromBody] ApiIdDto dto) { try { var d = await _context.GroupDeals.FindAsync(dto.Id); if (d != null) { d.Status = "Approved"; d.IsActive = true; await _context.SaveChangesAsync(); } return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        // Orders & Shipping
        [HttpGet("orders")]
        public async Task<IActionResult> GetAllOrders() => Ok(new { success = true, data = await _context.Orders.OrderByDescending(o => o.OrderDate).Select(o => new { o.Id, o.CustomerName, o.Status, o.TotalAmount, o.OrderDate }).Take(50).ToListAsync() });

        [HttpPut("orders/status")]
        public async Task<IActionResult> UpdateOrder([FromBody] ApiStatusUpdateDto dto) { try { var o = await _context.Orders.FindAsync(dto.Id); if (o == null) return NotFound(); o.Status = dto.Status!; await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpGet("shipping")]
        public async Task<IActionResult> GetShippingRates() => Ok(new { success = true, data = await _context.ShippingRates.ToListAsync() });

        [HttpPost("shipping")]
        public async Task<IActionResult> SaveShipping([FromBody] ShippingRate r) { try { _context.ShippingRates.Update(r); await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpDelete("shipping/{id}")]
        public async Task<IActionResult> DeleteShipping(int id) { try { var r = await _context.ShippingRates.FindAsync(id); if (r != null) { _context.ShippingRates.Remove(r); await _context.SaveChangesAsync(); } return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        // Users & Wallets
        [HttpGet("users")]
        public async Task<IActionResult> GetUsers() => Ok(new { success = true, data = await _userManager.Users.Select(u => new { u.Id, u.FullName, u.PhoneNumber, u.UserType, u.IsVerifiedMerchant, u.WalletBalance }).Take(100).ToListAsync() });

        [HttpPost("wallet/adjust")]
        public async Task<IActionResult> AdjustWallet([FromBody] ApiWalletAdjustDto dto) { try { var u = await _userManager.FindByIdAsync(dto.UserId!); if (u == null) return NotFound(); u.WalletBalance += (dto.Type == "Deposit" ? dto.Amount : -dto.Amount); _context.WalletTransactions.Add(new WalletTransaction { UserId = dto.UserId!, Amount = dto.Amount, Type = dto.Type!, TransactionDate = DateTime.Now, Description = dto.Description! }); await _userManager.UpdateAsync(u); await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpDelete("users/{id}")]
        public async Task<IActionResult> DeleteUser(string id) { try { var u = await _userManager.FindByIdAsync(id); if (u != null) await _userManager.DeleteAsync(u); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        // Reviews, Surveys, Requests
        [HttpGet("reviews")]
        public async Task<IActionResult> GetReviews() => Ok(new { success = true, data = await _context.ProductReviews.Include(r => r.Product).Include(r => r.User).Select(r => new { r.Id, r.Rating, r.Comment, r.IsVisible, Product = r.Product!.Name, User = r.User!.FullName }).ToListAsync() });

        [HttpPut("reviews/toggle")]
        public async Task<IActionResult> ToggleReview([FromBody] ApiIdDto dto) { try { var r = await _context.ProductReviews.FindAsync(dto.Id); if (r != null) { r.IsVisible = !r.IsVisible; await _context.SaveChangesAsync(); } return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpGet("surveys")]
        public async Task<IActionResult> GetSurveys() => Ok(new { success = true, data = await _context.Surveys.Select(s => new { s.Id, s.Title, s.IsActive, s.StartDate, s.EndDate }).ToListAsync() });

        [HttpPost("surveys")]
        public async Task<IActionResult> AddSurvey([FromBody] Survey s) { try { _context.Surveys.Add(s); await _context.SaveChangesAsync(); return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpPost("surveys/toggle")]
        public async Task<IActionResult> ToggleSurvey([FromBody] ApiIdDto dto) { try { var s = await _context.Surveys.FindAsync(dto.Id); if (s != null) { s.IsActive = !s.IsActive; await _context.SaveChangesAsync(); } return Ok(new { success = true }); } catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); } }

        [HttpGet("audit")]
        public async Task<IActionResult> GetAuditLogs() => Ok(new { success = true, data = await _context.AuditLogs.OrderByDescending(x => x.Timestamp).Take(50).ToListAsync() });
    }

    // =========================================================================
    // DTOs (Data Transfer Objects)
    // =========================================================================
    public class ApiLoginDto { public string? Phone { get; set; } public string? Password { get; set; } public bool RememberMe { get; set; } }
    public class ApiSignupDto { public string? FullName { get; set; } public string? Phone { get; set; } public string? Password { get; set; } public string? Type { get; set; } public string? ShopName { get; set; } public string? CommercialReg { get; set; } public string? TaxCard { get; set; } }
    public class ApiPhoneDto { public string? Phone { get; set; } }
    public class ApiResetPassDto { public string? Phone { get; set; } public string? Code { get; set; } public string? Password { get; set; } }
    public class ApiUpdateProfileDto { public string? FullName { get; set; } public string? CurrentPassword { get; set; } public string? NewPassword { get; set; } }
    public class ApiAmountDto { public decimal Amount { get; set; } }
    public class ApiIdDto { public int Id { get; set; } }
    public class ApiStringIdDto { public string? Id { get; set; } }
    public class ApiCheckoutDto { public string? Name { get; set; } public string? Phone { get; set; } public string? Governorate { get; set; } public string? City { get; set; } public string? Address { get; set; } public string? PaymentMethod { get; set; } public string? Notes { get; set; } public string? DeliverySlot { get; set; } public decimal ShippingCost { get; set; } public List<ApiCartItemDto>? Items { get; set; } }
    public class ApiCartItemDto { public int Id { get; set; } public int Qty { get; set; } public string? ColorName { get; set; } public string? ColorHex { get; set; } }
    public class ApiSurveySubmitDto { public int SurveyId { get; set; } public Dictionary<string, string>? Answers { get; set; } }
    public class ApiStockUpdateDto { public int ProductId { get; set; } public int Quantity { get; set; } }
    public class ApiOfferDto { public int RequestId { get; set; } public decimal Price { get; set; } public string? Notes { get; set; } }
    public class ApiMessageDto { public int RequestId { get; set; } public string? Message { get; set; } }
    public class ApiStatusUpdateDto { public int Id { get; set; } public string? Status { get; set; } }
    public class ApiWalletAdjustDto { public string? UserId { get; set; } public decimal Amount { get; set; } public string? Type { get; set; } public string? Description { get; set; } }
}