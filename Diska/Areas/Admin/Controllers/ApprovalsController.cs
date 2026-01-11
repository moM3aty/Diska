using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Diska.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Diska.Services;
using System.Text.Json;

namespace Diska.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ApprovalsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notificationService;
        private readonly IAuditService _auditService;

        public ApprovalsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, INotificationService notificationService, IAuditService auditService)
        {
            _context = context;
            _userManager = userManager;
            _notificationService = notificationService;
            _auditService = auditService;
        }

        // عرض كل الطلبات المعلقة
        public async Task<IActionResult> Index()
        {
            var pending = await _context.PendingMerchantActions
                .Include(p => p.Merchant)
                .Where(p => p.Status == "Pending")
                .OrderByDescending(p => p.RequestDate)
                .ToListAsync();

            return View(pending);
        }

        // تفاصيل الطلب للمقارنة
        public async Task<IActionResult> Review(int id)
        {
            var action = await _context.PendingMerchantActions
                .Include(p => p.Merchant)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (action == null) return NotFound();
            return View(action);
        }

        // الموافقة على التغيير
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var action = await _context.PendingMerchantActions.FindAsync(id);
            if (action == null || action.Status != "Pending") return NotFound();

            var adminId = _userManager.GetUserId(User);

            try
            {
                // تنفيذ المنطق بناءً على النوع
                if (action.ActionType == "UpdateProductPrice")
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                    // استخدام JsonElement للتعامل الآمن مع البيانات الديناميكية
                    using (JsonDocument doc = JsonDocument.Parse(action.NewValueJson))
                    {
                        if (doc.RootElement.TryGetProperty("Price", out JsonElement priceElement) &&
                            int.TryParse(action.EntityId, out int productId))
                        {
                            var product = await _context.Products.FindAsync(productId);
                            if (product != null)
                            {
                                product.Price = priceElement.GetDecimal();
                                _context.Update(product);
                            }
                        }
                    }
                }

                // حالات أخرى (سحب رصيد)
                else if (action.ActionType == "WithdrawRequest")
                {
                    using (JsonDocument doc = JsonDocument.Parse(action.NewValueJson))
                    {
                        if (doc.RootElement.TryGetProperty("Amount", out JsonElement amountElement))
                        {
                            decimal amount = amountElement.GetDecimal();
                            var merchant = await _userManager.FindByIdAsync(action.MerchantId);

                            // خصم الرصيد فعلياً وتسجيل العملية
                            if (merchant != null && merchant.WalletBalance >= amount)
                            {
                                // (الخصم تم عند الطلب أو يتم الآن - حسب سياسة الموقع)
                                // هنا نفترض الخصم عند الموافقة لضمان وجود الرصيد
                                merchant.WalletBalance -= amount;

                                _context.WalletTransactions.Add(new WalletTransaction
                                {
                                    UserId = merchant.Id,
                                    Amount = amount,
                                    Type = "Withdraw", // سحب
                                    Description = $"تمت الموافقة على سحب الأرباح (Ref: #{action.Id})",
                                    TransactionDate = DateTime.Now
                                });
                                await _userManager.UpdateAsync(merchant);
                            }
                        }
                    }
                }

                action.Status = "Approved";
                action.ActionDate = DateTime.Now;
                action.ActionByAdminId = adminId;

                await _context.SaveChangesAsync();

                await _auditService.LogAsync(adminId, "Approve Request", action.EntityName, action.EntityId, $"تم قبول طلب {action.ActionType}", HttpContext.Connection.RemoteIpAddress?.ToString());
                await _notificationService.NotifyUserAsync(action.MerchantId, "تمت الموافقة", "تمت الموافقة على طلبك بنجاح.", "Success");

                TempData["Success"] = "تم اعتماد الطلب بنجاح.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "حدث خطأ: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        // رفض التغيير
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string reason)
        {
            var action = await _context.PendingMerchantActions.FindAsync(id);
            if (action == null) return NotFound();

            var adminId = _userManager.GetUserId(User);

            action.Status = "Rejected";
            action.AdminComment = reason;
            action.ActionDate = DateTime.Now;
            action.ActionByAdminId = adminId;

            await _context.SaveChangesAsync();

            await _auditService.LogAsync(adminId, "Reject Request", action.EntityName, action.EntityId, $"تم رفض الطلب. السبب: {reason}", HttpContext.Connection.RemoteIpAddress?.ToString());
            await _notificationService.NotifyUserAsync(action.MerchantId, "تم الرفض", $"عذراً، تم رفض طلبك. السبب: {reason}", "Alert");

            TempData["Success"] = "تم رفض الطلب.";
            return RedirectToAction(nameof(Index));
        }
    }
}