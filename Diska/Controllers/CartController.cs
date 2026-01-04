using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Diska.Models;
using Microsoft.AspNetCore.Identity;
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
            if (model == null || !model.Items.Any()) return Json(new { success = false, message = "Cart is empty" });

            ApplicationUser user = null;
            if (User.Identity.IsAuthenticated)
            {
                user = await _userManager.GetUserAsync(User);
            }

            if (model.PaymentMethod == "Wallet")
            {
                if (user == null) return Json(new { success = false, message = "يجب تسجيل الدخول للدفع بالمحفظة" });

                decimal orderTotal = model.Items.Sum(i => i.Price * i.Qty) + model.ShippingCost;

                if (user.WalletBalance < orderTotal)
                {
                    return Json(new { success = false, message = "رصيد المحفظة غير كافٍ لإتمام العملية" });
                }

                user.WalletBalance -= orderTotal;
                await _userManager.UpdateAsync(user);
            }

            var order = new Order
            {
                CustomerName = user != null ? user.FullName : model.ShopName,
                Phone = model.Phone,
                Address = model.Address,
                Governorate = model.Governorate,
                City = model.City,
                TotalAmount = model.Items.Sum(i => i.Price * i.Qty) + model.ShippingCost,
                OrderDate = DateTime.Now,
                Status = "Pending",
                PaymentMethod = model.PaymentMethod,
                UserId = user != null ? user.Id : "Guest"
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            foreach (var item in model.Items)
            {
                if (int.TryParse(item.Id, out int productId))
                {
                    var orderItem = new OrderItem
                    {
                        OrderId = order.Id,
                        ProductId = productId,
                        Quantity = item.Qty,
                        UnitPrice = item.Price
                    };
                    _context.OrderItems.Add(orderItem);

                    var product = await _context.Products.FindAsync(productId);
                    if (product != null)
                    {
                        product.StockQuantity -= item.Qty;
                        if (product.StockQuantity < 0) product.StockQuantity = 0;
                    }
                }
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true, orderId = order.Id });
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