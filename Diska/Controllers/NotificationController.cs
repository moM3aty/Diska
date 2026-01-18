using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Diska.Models;

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

        public IActionResult Index()
        {
            return View();
        }

        // API لجلب الإشعارات
        [HttpGet]
        public async Task<IActionResult> GetRecent()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var notifications = await _context.UserNotifications
                .Where(n => n.UserId == user.Id)
                .OrderByDescending(n => n.CreatedAt)
                .Take(20)
                .Select(n => new
                {
                    id = n.Id,
                    title = n.Title,
                    message = n.Message,
                    type = n.Type,
                    link = n.Link,
                    isRead = n.IsRead,
                    timeAgo = TimeAgo(n.CreatedAt) // دالة مساعدة لحساب الوقت
                })
                .ToListAsync();

            return Json(notifications);
        }

        // API لجلب عدد غير المقروء (للـ Badge في الـ Layout)
        [HttpGet]
        public async Task<IActionResult> GetUnreadCount()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(0);

            var count = await _context.UserNotifications
                .CountAsync(n => n.UserId == user.Id && !n.IsRead);

            return Json(count);
        }

        // API لجعل كل الإشعارات مقروءة (عند فتح الصفحة)
        [HttpPost]
        public async Task<IActionResult> MarkAllAsSeen()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var unreadNotifications = await _context.UserNotifications
                .Where(n => n.UserId == user.Id && !n.IsRead)
                .ToListAsync();

            if (unreadNotifications.Any())
            {
                foreach (var n in unreadNotifications)
                {
                    n.IsRead = true;
                }
                await _context.SaveChangesAsync();
            }

            return Ok();
        }

        // API لتحديد إشعار واحد كمقروء
        [HttpPost]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var notification = await _context.UserNotifications.FindAsync(id);

            if (notification != null && notification.UserId == user.Id)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> ClearAll()
        {
            var user = await _userManager.GetUserAsync(User);
            var all = await _context.UserNotifications.Where(n => n.UserId == user.Id).ToListAsync();

            _context.UserNotifications.RemoveRange(all);
            await _context.SaveChangesAsync();

            return Ok();
        }

        // Helper for TimeAgo (ممكن وضعها في Helper Class منفصل)
        private static string TimeAgo(DateTime dateTime)
        {
            var span = DateTime.Now - dateTime;
            if (span.Days > 365) return "منذ سنوات";
            if (span.Days > 30) return "منذ شهور";
            if (span.Days > 0) return $"منذ {span.Days} يوم";
            if (span.Hours > 0) return $"منذ {span.Hours} ساعة";
            if (span.Minutes > 0) return $"منذ {span.Minutes} دقيقة";
            return "الآن";
        }
    }
}