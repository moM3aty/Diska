using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Microsoft.EntityFrameworkCore;
using Diska.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace Diska.Controllers
{
    [Authorize]
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public OrderController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> MyOrders()
        {
            var user = await _userManager.GetUserAsync(User);
            var orders = await _context.Orders
                .Where(o => o.UserId == user.Id)
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return View(orders);
        }

        public async Task<IActionResult> Details(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == user.Id);

            if (order == null) return NotFound();

            return View(order);
        }

        // صفحة تتبع الطلب الجديدة
        public async Task<IActionResult> Track(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == user.Id);

            if (order == null) return NotFound();

            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == user.Id);

            if (order == null) return NotFound();

            if (order.Status == "Pending")
            {
                // إرجاع المخزون
                foreach (var item in order.OrderItems)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product != null)
                    {
                        product.StockQuantity += item.Quantity;
                    }
                }

                // استرداد للمحفظة إذا كان الدفع بالمحفظة
                if (order.PaymentMethod == "Wallet")
                {
                    user.WalletBalance += order.TotalAmount;

                    _context.WalletTransactions.Add(new WalletTransaction
                    {
                        UserId = user.Id,
                        Amount = order.TotalAmount,
                        Type = "Refund",
                        Description = $"إلغاء الطلب #{order.Id}",
                        TransactionDate = DateTime.Now
                    });

                    await _userManager.UpdateAsync(user);
                }

                order.Status = "Cancelled";
                await _context.SaveChangesAsync();
                TempData["Message"] = "تم إلغاء الطلب بنجاح";
            }
            else
            {
                TempData["Error"] = "عفواً، لا يمكن إلغاء الطلب في هذه المرحلة (قد تم تأكيده أو شحنه).";
            }

            return RedirectToAction(nameof(MyOrders));
        }
    }
}