using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Diska.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Diska.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Diska.Controllers
{
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IShippingService _shippingService;

        public CartController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IShippingService shippingService)
        {
            _context = context;
            _userManager = userManager;
            _shippingService = shippingService;
        }

        public IActionResult Index()
        {
            return View();
        }

        // 2. إضافة عنصر (AddItem) - محدث لإرجاع بيانات التاجر
        [HttpPost]
        public async Task<IActionResult> AddItem(int productId, int quantity, string colorHex = null, string colorName = null)
        {
            var product = await _context.Products
                .Include(p => p.ProductColors)
                .Include(p => p.Merchant) // ضروري لمعرفة التاجر
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (product == null || product.Status != "Active")
                return Json(new { success = false, message = "عفواً، المنتج غير متاح." });

            if (product.StockQuantity < quantity)
                return Json(new { success = false, message = $"الكمية المتاحة: {product.StockQuantity}" });

            string finalColorName = colorName ?? (product.ProductColors.FirstOrDefault()?.ColorName ?? (product.Color ?? "Default"));
            string finalColorHex = colorHex ?? (product.ProductColors.FirstOrDefault()?.ColorHex ?? (product.Color ?? "#000000"));

            return Json(new
            {
                success = true,
                message = "تمت الإضافة للسلة",
                product = new
                {
                    id = product.Id,
                    name = product.Name,
                    price = product.Price,
                    image = product.ImageUrl,
                    colorName = finalColorName,
                    colorHex = finalColorHex,
                    // بيانات التاجر للتحقق في الـ Front-end
                    merchantId = product.MerchantId,
                    shopName = product.Merchant?.ShopName ?? "متجر عام"
                }
            });
        }

        // ... (باقي الكنترولر كما هو بدون تغيير: GetCartDetails, GetCities, Checkout, PlaceOrder) ...
        [HttpPost]
        public async Task<IActionResult> GetCartDetails([FromBody] List<CartItemDto> items)
        {
            if (items == null || !items.Any()) return Json(new List<object>());

            var ids = new List<int>();
            foreach (var i in items) { if (int.TryParse(i.Id, out int pid)) ids.Add(pid); }

            var products = await _context.Products.Include(p => p.PriceTiers).Where(p => ids.Contains(p.Id)).ToListAsync();
            var isAr = System.Threading.Thread.CurrentThread.CurrentCulture.Name.StartsWith("ar");
            var result = new List<object>();

            foreach (var item in items)
            {
                if (!int.TryParse(item.Id, out int pid)) continue;
                var product = products.FirstOrDefault(p => p.Id == pid);
                if (product != null)
                {
                    decimal finalPrice = product.Price;
                    if (product.PriceTiers != null && product.PriceTiers.Any())
                    {
                        var tier = product.PriceTiers.OrderBy(t => t.UnitPrice)
                            .FirstOrDefault(t => item.Qty >= t.MinQuantity && item.Qty <= t.MaxQuantity);
                        if (tier != null) finalPrice = tier.UnitPrice;
                    }
                    result.Add(new
                    {
                        id = product.Id,
                        name = isAr ? product.Name : (product.NameEn ?? product.Name),
                        image = product.ImageUrl,
                        price = finalPrice,
                        stock = product.StockQuantity,
                        qty = item.Qty,
                        colorName = item.ColorName,
                        colorHex = item.ColorHex
                    });
                }
            }
            return Json(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetCities(string governorate)
        {
            if (string.IsNullOrEmpty(governorate)) return Json(new List<string>());
            var cities = await _context.ShippingRates
                .Where(r => r.Governorate == governorate && !string.IsNullOrEmpty(r.City) && r.City != "All")
                .Select(r => r.City).Distinct().OrderBy(c => c).ToListAsync();
            return Json(cities);
        }

        [HttpGet]
        public IActionResult GetShippingCost(string governorate, string city)
        {
            try { return Json(new { cost = _shippingService.CalculateCost(governorate, city) }); }
            catch { return Json(new { cost = 0 }); }
        }

        [Authorize]
        public async Task<IActionResult> Checkout()
        {
            var user = await _userManager.GetUserAsync(User);
            ViewBag.FullName = user.FullName;
            ViewBag.Phone = user.PhoneNumber;
            ViewBag.ShopName = user.ShopName;
            var governorates = await _context.ShippingRates.Select(r => r.Governorate).Distinct().OrderBy(g => g).ToListAsync();
            ViewBag.GovernoratesList = new SelectList(governorates);
            var defaultAddress = await _context.UserAddresses.OrderByDescending(a => a.IsDefault).FirstOrDefaultAsync(a => a.UserId == user.Id);
            if (defaultAddress != null)
            {
                ViewBag.Address = defaultAddress.Street;
                ViewBag.Governorate = defaultAddress.Governorate;
                ViewBag.City = defaultAddress.City;
            }
            return View();
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> PlaceOrder([FromBody] OrderSubmissionModel model)
        {
            if (model == null || !model.Items.Any()) return Json(new { success = false, message = "السلة فارغة." });
            var user = await _userManager.GetUserAsync(User);
            using var transaction = _context.Database.BeginTransaction();
            try
            {
                decimal shippingCost = _shippingService.CalculateCost(model.Governorate, model.City);
                var order = new Order
                {
                    UserId = user.Id,
                    CustomerName = model.ShopName ?? user.FullName,
                    Phone = model.Phone,
                    Governorate = model.Governorate,
                    City = model.City,
                    Address = model.Address,
                    DeliverySlot = model.DeliverySlot,
                    Notes = model.Notes,
                    PaymentMethod = model.PaymentMethod,
                    Status = "Pending",
                    OrderDate = DateTime.Now,
                    ShippingCost = shippingCost
                };
                decimal subTotal = 0;
                var orderItems = new List<OrderItem>();
                foreach (var itemDto in model.Items)
                {
                    if (int.TryParse(itemDto.Id, out int pid))
                    {
                        var product = await _context.Products.Include(p => p.PriceTiers).FirstOrDefaultAsync(p => p.Id == pid);
                        if (product == null || product.StockQuantity < itemDto.Qty) return Json(new { success = false, message = $"المنتج {product?.Name} غير متوفر." });
                        product.StockQuantity -= itemDto.Qty;
                        _context.Update(product);
                        decimal finalPrice = product.Price;
                        if (product.PriceTiers != null)
                        {
                            var tier = product.PriceTiers.OrderBy(t => t.UnitPrice).FirstOrDefault(t => itemDto.Qty >= t.MinQuantity && itemDto.Qty <= t.MaxQuantity);
                            if (tier != null) finalPrice = tier.UnitPrice;
                        }
                        subTotal += finalPrice * itemDto.Qty;
                        orderItems.Add(new OrderItem { ProductId = pid, Quantity = itemDto.Qty, UnitPrice = finalPrice, SelectedColorName = itemDto.ColorName, SelectedColorHex = itemDto.ColorHex });
                    }
                }
                if (subTotal == 0) return Json(new { success = false, message = "خطأ في الحساب." });
                decimal taxAmount = subTotal * 0.14m;
                order.TotalAmount = subTotal + shippingCost + taxAmount;
                order.OrderItems = orderItems;
                if (model.PaymentMethod == "Wallet")
                {
                    if (user.WalletBalance < order.TotalAmount) return Json(new { success = false, message = "رصيد المحفظة لا يكفي." });
                    user.WalletBalance -= order.TotalAmount;
                    _context.WalletTransactions.Add(new WalletTransaction { UserId = user.Id, Amount = order.TotalAmount, Type = "Purchase", Description = "شراء طلب", TransactionDate = DateTime.Now });
                    await _userManager.UpdateAsync(user);
                    order.Status = "Confirmed";
                }
                _context.Orders.Add(order);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return Json(new { success = true, orderId = order.Id });
            }
            catch (Exception ex) { await transaction.RollbackAsync(); return Json(new { success = false, message = "خطأ: " + ex.Message }); }
        }
        public IActionResult OrderSuccess(int id) { ViewBag.OrderId = id; return View(); }
        public IActionResult OrderFailed(int id) { ViewBag.OrderId = id; return View(); }
    }
    public class OrderSubmissionModel { public string ShopName { get; set; } public string Phone { get; set; } public string Governorate { get; set; } public string City { get; set; } public string Address { get; set; } public string PaymentMethod { get; set; } public string DeliverySlot { get; set; } public string Notes { get; set; } public decimal ShippingCost { get; set; } public List<CartItemDto> Items { get; set; } }
    public class CartItemDto { public string Id { get; set; } public int Qty { get; set; } public string ColorName { get; set; } public string ColorHex { get; set; } }
}