using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Diska.Models;
using Microsoft.AspNetCore.Authorization;
using System.Linq;
using System.Threading.Tasks;

namespace Diska.Controllers
{
    [Authorize]
    public class NotificationController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public NotificationController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // 1. عرض قائمة الإشعارات
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var notifications = await _context.UserNotifications
                .Where(n => n.UserId == user.Id)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            // تحديث حالة "مقروء" عند فتح الصفحة (اختياري، أو يتم يدوياً)
            // حالياً سنتركها للمستخدم ليضغط "تمييز كمقروء" أو عند النقر على الرابط

            return View(notifications);
        }

        // 2. تحديد كـ مقروء (API for AJAX)
        [HttpPost]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var notification = await _context.UserNotifications
                .FirstOrDefaultAsync(n => n.Id == id && n.UserId == user.Id);

            if (notification != null)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            return Json(new { success = false });
        }

        // 3. حذف جميع الإشعارات
        [HttpPost]
        public async Task<IActionResult> ClearAll()
        {
            var user = await _userManager.GetUserAsync(User);
            var notifications = _context.UserNotifications.Where(n => n.UserId == user.Id);

            _context.UserNotifications.RemoveRange(notifications);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        // 4. API للحصول على العدد غير المقروء (للـ Layout Badge)
        [HttpGet]
        public async Task<IActionResult> GetUnreadCount()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(0);

            var count = await _context.UserNotifications
                .CountAsync(n => n.UserId == user.Id && !n.IsRead);

            return Json(count);
        }
    }
}