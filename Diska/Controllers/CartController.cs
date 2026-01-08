using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Diska.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Diska.Services;
using System;
using System.Threading.Tasks;

namespace Diska.Controllers
{
    // تم إضافة [Authorize] لإجبار تسجيل الدخول قبل الوصول للسلة أو الدفع
    [Authorize]
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IShippingService _shippingService;
        private readonly IPaymentService _paymentService;

        public CartController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IShippingService shippingService, IPaymentService paymentService)
        {
            _context = context;
            _userManager = userManager;
            _shippingService = shippingService;
            _paymentService = paymentService;
        }

        public IActionResult Index() => View();

        public async Task<IActionResult> Checkout()
        {
            // تحسين: جلب بيانات المستخدم المسجل لتعبئة الحقول تلقائياً
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                ViewBag.FullName = user.FullName;
                ViewBag.Phone = user.PhoneNumber;
                ViewBag.ShopName = user.ShopName;

                // محاولة جلب العنوان الافتراضي
                var defaultAddress = await _context.UserAddresses
                    .FirstOrDefaultAsync(a => a.UserId == user.Id && a.IsDefault);

                if (defaultAddress != null)
                {
                    ViewBag.Address = defaultAddress.Street;
                    ViewBag.Governorate = defaultAddress.Governorate;
                    ViewBag.City = defaultAddress.City;
                }
            }
            return View();
        }

        public IActionResult OrderSuccess(int id)
        {
            ViewBag.OrderId = id;
            return View();
        }

        public IActionResult OrderFailed(int id)
        {
            ViewBag.OrderId = id;
            return View();
        }

        [HttpGet]
        public IActionResult GetShippingCost(string gov, string city)
        {
            decimal cost = _shippingService.CalculateCost(gov, city);
            return Json(new { cost = cost });
        }

        [HttpPost]
        public async Task<IActionResult> PlaceOrder([FromBody] OrderSubmissionModel model)
        {
            if (model == null || !model.Items.Any())
                return Json(new { success = false, message = "السلة فارغة" });

            var user = await _userManager.GetUserAsync(User); // المستخدم مضمون بسبب Authorize

            using var transaction = _context.Database.BeginTransaction();
            try
            {
                decimal calculatedTotal = 0;
                var orderItems = new List<OrderItem>();

                // 1. التحقق من المخزون
                foreach (var item in model.Items)
                {
                    if (int.TryParse(item.Id, out int productId))
                    {
                        var product = await _context.Products.FindAsync(productId);
                        if (product == null) throw new Exception($"المنتج رقم {item.Id} غير موجود");

                        if (product.StockQuantity < item.Qty)
                        {
                            return Json(new { success = false, message = $"الكمية المتاحة من {product.Name} هي {product.StockQuantity} فقط." });
                        }

                        decimal finalPrice = product.Price;
                        calculatedTotal += finalPrice * item.Qty;

                        // خصم المخزون
                        product.StockQuantity -= item.Qty;
                        _context.Update(product);

                        orderItems.Add(new OrderItem
                        {
                            ProductId = productId,
                            Quantity = item.Qty,
                            UnitPrice = finalPrice
                        });
                    }
                }

                // 2. حساب الشحن
                decimal shippingCost = _shippingService.CalculateCost(model.Governorate, model.City);
                decimal grandTotal = calculatedTotal + shippingCost;

                string orderStatus = "Pending";

                // 3. معالجة الدفع
                if (model.PaymentMethod == "Online")
                {
                    var onlineOrder = new Order
                    {
                        UserId = user.Id,
                        CustomerName = model.ShopName,
                        Phone = model.Phone,
                        Address = model.Address,
                        Governorate = model.Governorate,
                        City = model.City,
                        TotalAmount = grandTotal,
                        ShippingCost = shippingCost,
                        OrderDate = DateTime.Now,
                        Status = "Pending Payment",
                        PaymentMethod = "Online",
                        DeliverySlot = model.DeliverySlot,
                        Notes = model.Notes,
                        OrderItems = orderItems
                    };

                    _context.Orders.Add(onlineOrder);
                    await _context.SaveChangesAsync();

                    var paymentUrl = await _paymentService.InitiatePaymentAsync(grandTotal, "EGP", new { OrderId = onlineOrder.Id });
                    await transaction.CommitAsync();

                    return Json(new { success = true, redirectUrl = paymentUrl });
                }
                else if (model.PaymentMethod == "Wallet")
                {
                    if (user.WalletBalance < grandTotal) return Json(new { success = false, message = "رصيد المحفظة غير كافٍ" });

                    user.WalletBalance -= grandTotal;

                    _context.WalletTransactions.Add(new WalletTransaction
                    {
                        UserId = user.Id,
                        Amount = grandTotal,
                        Type = "Purchase",
                        Description = "شراء منتجات (دفع بالمحفظة)",
                        TransactionDate = DateTime.Now
                    });

                    await _userManager.UpdateAsync(user);
                }

                // 4. حفظ الطلب (كاش أو محفظة)
                var order = new Order
                {
                    UserId = user.Id,
                    CustomerName = model.ShopName,
                    Phone = model.Phone,
                    Address = model.Address,
                    Governorate = model.Governorate,
                    City = model.City,
                    TotalAmount = grandTotal,
                    ShippingCost = shippingCost,
                    OrderDate = DateTime.Now,
                    Status = orderStatus,
                    PaymentMethod = model.PaymentMethod,
                    DeliverySlot = model.DeliverySlot,
                    Notes = model.Notes,
                    OrderItems = orderItems
                };

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
        public decimal Price { get; set; }
    }
}