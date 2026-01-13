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

        // 1. عرض صفحة السلة
        public IActionResult Index()
        {
            return View();
        }

        // 2. التحقق من المنتج وإضافته (API) - المصحح
        [HttpPost]
        public async Task<IActionResult> AddItem(int productId, int quantity, string colorHex = null, string colorName = null)
        {
            var product = await _context.Products
                .Include(p => p.ProductColors)
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (product == null || product.Status != "Active")
            {
                return Json(new { success = false, message = "عفواً، هذا المنتج غير متاح حالياً." });
            }

            if (product.StockQuantity < quantity)
            {
                return Json(new { success = false, message = $"الكمية المتاحة فقط {product.StockQuantity} قطعة." });
            }

            // --- تصحيح منطق الألوان ---
            // نعتمد القيم القادمة من الطلب أولاً (لأن المستخدم اختارها)
            string finalColorName = colorName;
            string finalColorHex = colorHex;

            // إذا كانت القيم فارغة (لم يختر المستخدم شيئاً)، نستخدم الافتراضي للمنتج
            if (string.IsNullOrEmpty(finalColorName) && string.IsNullOrEmpty(finalColorHex))
            {
                var firstColor = product.ProductColors.FirstOrDefault();
                if (firstColor != null)
                {
                    finalColorName = firstColor.ColorName;
                    finalColorHex = firstColor.ColorHex;
                }
                else
                {
                    finalColorName = product.Color ?? "Default";
                    finalColorHex = product.Color ?? "#000000";
                }
            }

            // ضمان عدم وجود Null
            if (string.IsNullOrEmpty(finalColorHex)) finalColorHex = "#000000";
            if (string.IsNullOrEmpty(finalColorName)) finalColorName = "Standard";

            return Json(new
            {
                success = true,
                message = "تمت الإضافة للسلة بنجاح",
                product = new
                {
                    id = product.Id,
                    name = System.Threading.Thread.CurrentThread.CurrentCulture.Name.StartsWith("ar") ? product.Name : product.NameEn,
                    price = product.Price,
                    image = product.ImageUrl,
                    stock = product.StockQuantity,
                    // إرجاع اللون المختار ليتم تخزينه في المتصفح
                    colorName = finalColorName,
                    colorHex = finalColorHex
                }
            });
        }

        // 3. صفحة الدفع
        [Authorize]
        public async Task<IActionResult> Checkout()
        {
            var user = await _userManager.GetUserAsync(User);

            ViewBag.FullName = user.FullName;
            ViewBag.Phone = user.PhoneNumber;
            ViewBag.ShopName = user.ShopName;

            var defaultAddress = await _context.UserAddresses
                .OrderByDescending(a => a.IsDefault)
                .ThenByDescending(a => a.Id)
                .FirstOrDefaultAsync(a => a.UserId == user.Id);

            if (defaultAddress != null)
            {
                ViewBag.Address = defaultAddress.Street;
                ViewBag.Governorate = defaultAddress.Governorate;
                ViewBag.City = defaultAddress.City;
            }

            return View();
        }

        // 4. تنفيذ الطلب (PlaceOrder) - المصحح
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> PlaceOrder([FromBody] OrderSubmissionModel model)
        {
            if (model == null || !model.Items.Any())
                return Json(new { success = false, message = "السلة فارغة." });

            var user = await _userManager.GetUserAsync(User);

            using var transaction = _context.Database.BeginTransaction();

            try
            {
                var order = new Order
                {
                    UserId = user.Id,
                    CustomerName = !string.IsNullOrEmpty(model.ShopName) ? model.ShopName : user.FullName,
                    Phone = model.Phone,
                    Governorate = model.Governorate,
                    City = model.City,
                    Address = model.Address,
                    DeliverySlot = model.DeliverySlot,
                    Notes = model.Notes,
                    PaymentMethod = model.PaymentMethod,
                    Status = "Pending",
                    OrderDate = DateTime.Now,
                    ShippingCost = _shippingService.CalculateCost(model.Governorate, model.City)
                };

                decimal subTotal = 0;
                var orderItems = new List<OrderItem>();

                foreach (var itemDto in model.Items)
                {
                    if (int.TryParse(itemDto.Id, out int pid))
                    {
                        var product = await _context.Products
                            .Include(p => p.PriceTiers)
                            .Include(p => p.ProductColors)
                            .FirstOrDefaultAsync(p => p.Id == pid);

                        if (product == null || product.Status != "Active")
                            return Json(new { success = false, message = $"المنتج {pid} غير متاح." });

                        if (product.StockQuantity < itemDto.Qty)
                            return Json(new { success = false, message = $"الكمية غير متوفرة لـ {product.Name}." });

                        product.StockQuantity -= itemDto.Qty;
                        _context.Update(product);

                        decimal finalPrice = product.Price;
                        if (product.PriceTiers != null && product.PriceTiers.Any())
                        {
                            var tier = product.PriceTiers.OrderBy(t => t.UnitPrice).FirstOrDefault(t => itemDto.Qty >= t.MinQuantity && itemDto.Qty <= t.MaxQuantity);
                            if (tier != null) finalPrice = tier.UnitPrice;
                        }

                        subTotal += finalPrice * itemDto.Qty;

                        // حفظ اللون المختار في الطلب
                        // الأولوية لما جاء من الـ DTO (الواجهة)، وإلا نستخدم الافتراضي
                        string cName = !string.IsNullOrEmpty(itemDto.ColorName) ? itemDto.ColorName : (product.ProductColors.FirstOrDefault()?.ColorName ?? "Standard");
                        string cHex = !string.IsNullOrEmpty(itemDto.ColorHex) ? itemDto.ColorHex : (product.ProductColors.FirstOrDefault()?.ColorHex ?? "#000000");

                        orderItems.Add(new OrderItem
                        {
                            ProductId = pid,
                            Quantity = itemDto.Qty,
                            UnitPrice = finalPrice,
                            SelectedColorName = cName,
                            SelectedColorHex = cHex
                        });
                    }
                }

                order.TotalAmount = subTotal + order.ShippingCost;
                order.OrderItems = orderItems;

                if (model.PaymentMethod == "Wallet")
                {
                    if (user.WalletBalance < order.TotalAmount)
                        return Json(new { success = false, message = "رصيد المحفظة لا يكفي." });

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
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return Json(new { success = false, message = "خطأ: " + ex.Message });
            }
        }

        public IActionResult OrderSuccess(int id) { ViewBag.OrderId = id; return View(); }
        public IActionResult OrderFailed(int id) { ViewBag.OrderId = id; return View(); }
    }

    public class OrderSubmissionModel
    {
        public string ShopName { get; set; }
        public string Phone { get; set; }
        public string Governorate { get; set; }
        public string City { get; set; }
        public string Address { get; set; }
        public string PaymentMethod { get; set; }
        public string DeliverySlot { get; set; }
        public string Notes { get; set; }
        public decimal ShippingCost { get; set; }
        public List<CartItemDto> Items { get; set; }
    }

    public class CartItemDto
    {
        public string Id { get; set; }
        public int Qty { get; set; }
        public string ColorName { get; set; }
        public string ColorHex { get; set; }
    }
}