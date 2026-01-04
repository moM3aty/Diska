using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Diska.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;

namespace Diska.Controllers
{
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CartController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public IActionResult Index() => View();
        public IActionResult Checkout() => View();

        [HttpPost]
        public async Task<IActionResult> PlaceOrder([FromBody] OrderSubmissionModel model)
        {
            if (model == null || !model.Items.Any())
                return Json(new { success = false, message = "السلة فارغة" });

            ApplicationUser user = null;
            if (User.Identity.IsAuthenticated)
            {
                user = await _userManager.GetUserAsync(User);
            }

            // 1. Inventory Check & Locking
            using var transaction = _context.Database.BeginTransaction();
            try
            {
                decimal calculatedTotal = 0;
                var orderItems = new List<OrderItem>();

                foreach (var item in model.Items)
                {
                    if (int.TryParse(item.Id, out int productId))
                    {
                        var product = await _context.Products.FindAsync(productId);
                        if (product == null) throw new Exception($"المنتج رقم {item.Id} غير موجود");

                        // Check Stock
                        if (product.StockQuantity < item.Qty)
                        {
                            return Json(new { success = false, message = $"الكمية المتاحة من {product.Name} هي {product.StockQuantity} فقط." });
                        }

                        // Check Expiry
                        if (product.ExpiryDate.HasValue && product.ExpiryDate.Value < DateTime.Now)
                        {
                            return Json(new { success = false, message = $"عفواً، المنتج {product.Name} منتهي الصلاحية ولا يمكن بيعه." });
                        }

                        // Determine Price (Tiered or Base)
                        decimal finalPrice = product.Price;
                        // Logic for tiers could be added here if dynamic

                        calculatedTotal += finalPrice * item.Qty;

                        // Deduct Stock
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

                // 2. Shipping Calculation
                decimal shippingCost = CalculateShipping(model.Governorate);
                decimal grandTotal = calculatedTotal + shippingCost;

                // 3. Wallet Payment Logic
                if (model.PaymentMethod == "Wallet")
                {
                    if (user == null) return Json(new { success = false, message = "يجب تسجيل الدخول للدفع بالمحفظة" });
                    if (user.WalletBalance < grandTotal) return Json(new { success = false, message = "رصيد المحفظة غير كافٍ" });

                    user.WalletBalance -= grandTotal;
                    await _userManager.UpdateAsync(user);
                }

                // 4. Create Order
                var order = new Order
                {
                    UserId = user?.Id ?? "Guest",
                    CustomerName = model.ShopName, // Or User FullName
                    Phone = model.Phone,
                    Address = model.Address,
                    Governorate = model.Governorate,
                    City = model.City,
                    TotalAmount = grandTotal,
                    ShippingCost = shippingCost,
                    OrderDate = DateTime.Now,
                    Status = "Pending",
                    PaymentMethod = model.PaymentMethod,
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
                return Json(new { success = false, message = "حدث خطأ أثناء معالجة الطلب: " + ex.Message });
            }
        }

        private decimal CalculateShipping(string gov)
        {
            if (gov == "القاهرة" || gov == "الجيزة") return 50;
            if (gov == "الإسكندرية") return 75;
            return 100;
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
        public List<CartItemDto> Items { get; set; }
    }

    public class CartItemDto
    {
        public string Id { get; set; }
        public int Qty { get; set; }
    }
}