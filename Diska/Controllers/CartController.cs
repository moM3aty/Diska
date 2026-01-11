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
using System.Text.Json;

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

        // 1. عرض السلة (Index)
        public IActionResult Index()
        {
            return View();
        }

        // 2. إضافة منتج للسلة (AddItem)
        [HttpPost]
        public async Task<IActionResult> AddItem(int productId, int quantity)
        {
            var product = await _context.Products.FindAsync(productId);

            if (product == null || !product.IsActive)
            {
                return Json(new { success = false, message = "المنتج غير موجود أو غير متاح." });
            }

            if (product.StockQuantity < quantity)
            {
                return Json(new { success = false, message = $"الكمية المتاحة فقط {product.StockQuantity} قطعة." });
            }

            // هنا يمكن إضافة منطق تخزين السلة في السيشن أو قاعدة البيانات
            // للتبسيط، سنعتمد على أن الواجهة الأمامية تدير السلة وترسل البيانات،
            // ولكن هذا الـ Action يستخدم للتحقق (Validation) قبل الإضافة في الـ JS

            return Json(new { success = true, message = "تمت الإضافة للسلة", productName = product.Name });
        }

        // 3. تحديث الكمية (UpdateQuantity)
        [HttpPost]
        public async Task<IActionResult> UpdateQuantity(int productId, int quantity)
        {
            if (quantity <= 0) return Json(new { success = false, message = "الكمية غير صحيحة" });

            var product = await _context.Products.FindAsync(productId);

            if (product == null) return Json(new { success = false, message = "المنتج غير موجود" });

            if (product.StockQuantity < quantity)
            {
                return Json(new { success = false, message = $"عفواً، المخزون المتاح {product.StockQuantity} فقط." });
            }

            return Json(new { success = true });
        }

        // 4. حذف منتج (RemoveItem) - عادة يتم في الـ Client Side ولكن يمكن توفيره كـ API
        [HttpPost]
        public IActionResult RemoveItem(int productId)
        {
            // منطق حذف من السيشن/DB لو مستخدم
            return Json(new { success = true, message = "تم الحذف" });
        }

        // --- Checkout Actions (موجودة سابقاً ولكن مدمجة هنا للتكامل) ---

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

        [HttpGet]
        public IActionResult GetShippingCost(string gov, string city)
        {
            decimal cost = _shippingService.CalculateCost(gov, city);
            return Json(new { cost = cost });
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> PlaceOrder([FromBody] OrderSubmissionModel model)
        {
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
                        var product = await _context.Products
                            .Include(p => p.PriceTiers) // لتطبيق عروض الجملة
                            .FirstOrDefaultAsync(p => p.Id == pid);

                        if (product == null) continue;

                        // Validate Stock
                        if (product.StockQuantity < itemDto.Qty)
                        {
                            return Json(new { success = false, message = $"الكمية المطلوبة من {product.Name} غير متوفرة (المتاح: {product.StockQuantity})." });
                        }

                        // Apply B2B Pricing Logic (Deals)
                        decimal price = product.Price;
                        if (product.PriceTiers != null && product.PriceTiers.Any())
                        {
                            var tier = product.PriceTiers
                                .Where(t => itemDto.Qty >= t.MinQuantity && itemDto.Qty <= t.MaxQuantity)
                                .OrderBy(t => t.UnitPrice) // أرخص سعر متاح للكمية
                                .FirstOrDefault();

                            if (tier != null)
                            {
                                price = tier.UnitPrice;
                            }
                            // التعامل مع الكميات الأكبر من آخر شريحة
                            else if (itemDto.Qty > product.PriceTiers.Max(t => t.MaxQuantity))
                            {
                                price = product.PriceTiers.OrderBy(t => t.UnitPrice).First().UnitPrice;
                            }
                        }

                        // Update Stock
                        product.StockQuantity -= itemDto.Qty;
                        _context.Update(product);

                        subTotal += price * itemDto.Qty;

                        orderItems.Add(new OrderItem
                        {
                            ProductId = pid,
                            Quantity = itemDto.Qty,
                            UnitPrice = price
                        });
                    }
                }

                order.TotalAmount = subTotal + model.ShippingCost;
                order.OrderItems = orderItems;

                // Payment: Wallet
                if (model.PaymentMethod == "Wallet")
                {
                    if (user.WalletBalance < order.TotalAmount)
                    {
                        return Json(new { success = false, message = $"رصيد المحفظة ({user.WalletBalance}) لا يكفي لإتمام الطلب ({order.TotalAmount})." });
                    }

                    user.WalletBalance -= order.TotalAmount;

                    _context.WalletTransactions.Add(new WalletTransaction
                    {
                        UserId = user.Id,
                        Amount = order.TotalAmount,
                        Type = "Purchase",
                        Description = $"دفع للطلب",
                        TransactionDate = DateTime.Now
                    });

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
                return Json(new { success = false, message = "خطأ في النظام: " + ex.Message });
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
    }
}