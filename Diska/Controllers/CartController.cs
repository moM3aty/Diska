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

        // 2. التحقق من المنتج وإضافته (API)
        // هذا الـ Action يستخدمه الـ JavaScript للتأكد من الكمية قبل الإضافة للـ LocalStorage
        [HttpPost]
        public async Task<IActionResult> AddItem(int productId, int quantity)
        {
            var product = await _context.Products.FindAsync(productId);

            if (product == null || product.Status != "Active")
            {
                return Json(new { success = false, message = "عفواً، هذا المنتج غير متاح حالياً." });
            }

            if (product.StockQuantity < quantity)
            {
                return Json(new { success = false, message = $"الكمية المتاحة فقط {product.StockQuantity} قطعة." });
            }

            // إرجاع بيانات المنتج ليستخدمها الـ Front-end
            return Json(new
            {
                success = true,
                message = "تمت الإضافة للسلة بنجاح",
                product = new
                {
                    id = product.Id,
                    name = System.Threading.Thread.CurrentThread.CurrentCulture.Name.StartsWith("ar") ? product.Name : product.NameEn,
                    price = product.Price, // السعر الأساسي (يمكن تطبيق خصم الكميات لاحقاً)
                    image = product.ImageUrl,
                    stock = product.StockQuantity
                }
            });
        }

        // 3. صفحة الدفع (Checkout View)
        [Authorize]
        public async Task<IActionResult> Checkout()
        {
            var user = await _userManager.GetUserAsync(User);

            ViewBag.FullName = user.FullName;
            ViewBag.Phone = user.PhoneNumber;
            ViewBag.ShopName = user.ShopName;

            // جلب آخر عنوان تم استخدامه أو العنوان الافتراضي
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

        // 4. تنفيذ الطلب (Place Order API) - العقل المدبر
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> PlaceOrder([FromBody] OrderSubmissionModel model)
        {
            if (model == null || !model.Items.Any())
                return Json(new { success = false, message = "السلة فارغة، لا يمكن إتمام الطلب." });

            var user = await _userManager.GetUserAsync(User);

            // استخدام Transaction لضمان أن كل العمليات (خصم مخزون، خصم رصيد، إنشاء طلب) تتم معاً أو تفشل معاً
            using var transaction = _context.Database.BeginTransaction();

            try
            {
                // 1. إنشاء كائن الطلب الأساسي
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
                    ShippingCost = _shippingService.CalculateCost(model.Governorate, model.City) // إعادة حساب الشحن في السيرفر للأمان
                };

                decimal subTotal = 0;
                var orderItems = new List<OrderItem>();

                // 2. معالجة المنتجات (Validation & Calculation)
                foreach (var itemDto in model.Items)
                {
                    if (int.TryParse(itemDto.Id, out int pid))
                    {
                        // جلب المنتج مع قفل (اختياري، هنا نعتمد على Concurrency Check عند الحفظ)
                        var product = await _context.Products
                            .Include(p => p.PriceTiers)
                            .FirstOrDefaultAsync(p => p.Id == pid);

                        if (product == null || product.Status != "Active")
                        {
                            return Json(new { success = false, message = $"المنتج رقم {pid} لم يعد متاحاً." });
                        }

                        // التحقق من المخزون
                        if (product.StockQuantity < itemDto.Qty)
                        {
                            return Json(new { success = false, message = $"عفواً، الكمية المطلوبة من '{product.Name}' غير متوفرة. المتاح: {product.StockQuantity}" });
                        }

                        // خصم المخزون
                        product.StockQuantity -= itemDto.Qty;
                        _context.Update(product); // سيتم الحفظ في النهاية

                        // تحديد السعر (تطبيق شرائح الجملة Server-Side)
                        decimal finalPrice = product.Price;
                        if (product.PriceTiers != null && product.PriceTiers.Any())
                        {
                            var tier = product.PriceTiers
                                .Where(t => itemDto.Qty >= t.MinQuantity && itemDto.Qty <= t.MaxQuantity)
                                .OrderBy(t => t.UnitPrice)
                                .FirstOrDefault();

                            // إذا كانت الكمية أكبر من أكبر شريحة، نطبق سعر أكبر شريحة (أرخص سعر)
                            if (tier == null && itemDto.Qty > product.PriceTiers.Max(t => t.MaxQuantity))
                            {
                                tier = product.PriceTiers.OrderBy(t => t.UnitPrice).FirstOrDefault();
                            }

                            if (tier != null) finalPrice = tier.UnitPrice;
                        }

                        subTotal += finalPrice * itemDto.Qty;

                        orderItems.Add(new OrderItem
                        {
                            ProductId = pid,
                            Quantity = itemDto.Qty,
                            UnitPrice = finalPrice
                        });
                    }
                }

                order.TotalAmount = subTotal + order.ShippingCost;
                order.OrderItems = orderItems;

                // 3. معالجة الدفع (Wallet)
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
                        Description = $"شراء طلب جديد",
                        TransactionDate = DateTime.Now
                    });

                    await _userManager.UpdateAsync(user);
                    order.Status = "Confirmed"; // الدفع تم، نؤكد الطلب مباشرة
                }

                // 4. الحفظ النهائي
                _context.Orders.Add(order);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Json(new { success = true, orderId = order.Id });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                // في الإنتاج، سجل الخطأ في Logger ولا ترجع التفاصيل للعميل
                return Json(new { success = false, message = "حدث خطأ غير متوقع أثناء معالجة الطلب. يرجى المحاولة مرة أخرى." });
            }
        }

        // صفحات النجاح والفشل
        public IActionResult OrderSuccess(int id) { ViewBag.OrderId = id; return View(); }
        public IActionResult OrderFailed(int id) { ViewBag.OrderId = id; return View(); }
    }

    // نماذج استقبال البيانات (DTOs)
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
        public decimal ShippingCost { get; set; } // للمرجعية، لكن السيرفر يعيد حسابها
        public List<CartItemDto> Items { get; set; }
    }

    public class CartItemDto
    {
        public string Id { get; set; }
        public int Qty { get; set; }
    }
}