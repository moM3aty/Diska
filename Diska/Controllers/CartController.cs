using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Diska.Models;
using Microsoft.AspNetCore.Identity; // Added
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims; // Added

namespace Diska.Controllers
{
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager; // Added

        public CartController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Checkout()
        {
            // إذا كان المستخدم مسجلاً، نمرر بياناته الافتراضية للصفحة لملء الحقول تلقائياً
            if (User.Identity.IsAuthenticated)
            {
                // يمكن تطوير هذا الجزء لجلب العنوان من جدول العناوين لاحقاً
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> PlaceOrder([FromBody] OrderSubmissionModel model)
        {
            if (model == null || !model.Items.Any())
            {
                return Json(new { success = false, message = "السلة فارغة" });
            }

            // تحديد معرف المستخدم (مسجل أو زائر)
            string userId = User.Identity.IsAuthenticated ? _userManager.GetUserId(User) : "Guest";

            // في حالة الزائر، يمكننا استخدام رقم الهاتف كمعرف مؤقت أو تركها Guest
            if (userId == null) userId = "Guest";

            var order = new Order
            {
                CustomerName = User.Identity.IsAuthenticated ? User.Identity.Name : model.ShopName,
                Phone = model.Phone,
                Address = model.Address,
                Governorate = model.Governorate,
                City = model.City,
                TotalAmount = model.Items.Sum(i => i.Price * i.Qty) + model.ShippingCost,
                OrderDate = DateTime.Now,
                Status = "Pending",
                PaymentMethod = model.PaymentMethod,
                UserId = userId
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync(); // Save to get Order ID

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

                    // تحديث المخزون
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