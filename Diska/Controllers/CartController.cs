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
    // السلة متاحة للجميع (حتى الزوار) ولكن الدفع يتطلب تسجيل دخول
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

        // 2. إضافة منتج للسلة (AddItem) - التحقق وإرجاع البيانات
        [HttpPost]
        public async Task<IActionResult> AddItem(int productId, int quantity)
        {
            var product = await _context.Products.FindAsync(productId);

            if (product == null || !product.IsActive)
            {
                return Json(new { success = false, message = "المنتج غير متوفر حالياً." });
            }

            if (product.StockQuantity < quantity)
            {
                return Json(new { success = false, message = $"الكمية المتاحة فقط {product.StockQuantity} قطعة." });
            }

            // إرجاع بيانات المنتج لتخزينها في LocalStorage
            return Json(new
            {
                success = true,
                message = "تمت الإضافة للسلة",
                product = new
                {
                    id = product.Id,
                    name = Thread.CurrentThread.CurrentCulture.Name.StartsWith("ar") ? product.Name : product.NameEn,
                    price = product.Price,
                    image = product.ImageUrl,
                    stock = product.StockQuantity
                }
            });
        }

        // 3. التحقق من المخزون قبل الدفع (Validate Cart)
        [HttpPost]
        public async Task<IActionResult> ValidateCart([FromBody] List<CartItemDto> items)
        {
            if (items == null || !items.Any()) return Json(new { valid = true });

            var ids = items.Select(i => int.Parse(i.Id)).ToList();
            var products = await _context.Products.Where(p => ids.Contains(p.Id)).ToListAsync();

            foreach (var item in items)
            {
                var product = products.FirstOrDefault(p => p.Id.ToString() == item.Id);
                if (product == null || product.StockQuantity < item.Qty)
                {
                    return Json(new { valid = false, message = $"المنتج {product?.Name ?? "غير معروف"} نفذت كميته أو غير كافية." });
                }
            }

            return Json(new { valid = true });
        }

        // --- Checkout Actions ---
        [Authorize]
        public async Task<IActionResult> Checkout()
        {
            var user = await _userManager.GetUserAsync(User);

            ViewBag.FullName = user.FullName;
            ViewBag.Phone = user.PhoneNumber;
            ViewBag.ShopName = user.ShopName;

            var defaultAddress = await _context.UserAddresses
                .FirstOrDefaultAsync(a => a.UserId == user.Id && a.IsDefault);

            if (defaultAddress != null)
            {
                ViewBag.Address = defaultAddress.Street;
                ViewBag.Governorate = defaultAddress.Governorate;
                ViewBag.City = defaultAddress.City;
            }

            return View();
        }

        // ... (PlaceOrder and other actions remain same as previous) ...
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> PlaceOrder([FromBody] OrderSubmissionModel model)
        {
            // ... (نفس كود PlaceOrder السابق) ...
            // للتبسيط سأعيد كتابة الجزء الأساسي فقط لضمان عمل الملف
            if (model == null || !model.Items.Any())
                return Json(new { success = false, message = "السلة فارغة" });

            var user = await _userManager.GetUserAsync(User);
            using var transaction = _context.Database.BeginTransaction();

            try
            {
                var order = new Order
                {
                    UserId = user.Id,
                    CustomerName = model.ShopName,
                    Phone = model.Phone,
                    Governorate = model.Governorate,
                    City = model.City,
                    Address = model.Address,
                    PaymentMethod = model.PaymentMethod,
                    Notes = model.Notes,
                    DeliverySlot = model.DeliverySlot,
                    Status = "Pending",
                    OrderDate = DateTime.Now,
                    ShippingCost = model.ShippingCost
                };

                decimal subTotal = 0;
                var orderItems = new List<OrderItem>();

                foreach (var itemDto in model.Items)
                {
                    if (int.TryParse(itemDto.Id, out int pid))
                    {
                        var product = await _context.Products.FindAsync(pid);
                        if (product == null || product.StockQuantity < itemDto.Qty)
                            return Json(new { success = false, message = $"الكمية غير متوفرة لمنتج: {product?.Name}" });

                        product.StockQuantity -= itemDto.Qty;
                        _context.Update(product);

                        decimal price = product.Price;
                        subTotal += price * itemDto.Qty;

                        orderItems.Add(new OrderItem { ProductId = pid, Quantity = itemDto.Qty, UnitPrice = price });
                    }
                }

                order.TotalAmount = subTotal + model.ShippingCost;
                order.OrderItems = orderItems;

                if (model.PaymentMethod == "Wallet")
                {
                    if (user.WalletBalance < order.TotalAmount)
                        return Json(new { success = false, message = "رصيد المحفظة لا يكفي." });

                    user.WalletBalance -= order.TotalAmount;
                    _context.WalletTransactions.Add(new WalletTransaction { UserId = user.Id, Amount = order.TotalAmount, Type = "Purchase", Description = "دفع طلب", TransactionDate = DateTime.Now });
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
                return Json(new { success = false, message = "حدث خطأ: " + ex.Message });
            }
        }

        public IActionResult OrderSuccess(int id) { ViewBag.OrderId = id; return View(); }
    }

    // DTO Classes
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
    public class CartItemDto { public string Id { get; set; } public int Qty { get; set; } }
}