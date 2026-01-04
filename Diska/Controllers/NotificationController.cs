using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Diska.Models;

namespace Diska.Controllers
{
    [Route("Notification")]
    public class NotificationController : Controller
    {
        private readonly ApplicationDbContext _context;
        // تصحيح: استخدام ApplicationUser
        private readonly UserManager<ApplicationUser> _userManager;

        public NotificationController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet("GetUnreadCount")]
        public async Task<IActionResult> GetUnreadCount()
        {
            if (!User.Identity.IsAuthenticated) return Json(0);

            var user = await _userManager.GetUserAsync(User);
            // حماية إضافية في حالة عدم العثور على المستخدم
            if (user == null) return Json(0);

            var count = await _context.UserNotifications
                .Where(n => n.UserId == user.Id && !n.IsRead)
                .CountAsync();

            return Json(count);
        }

        [HttpGet("GetRecent")]
        public async Task<IActionResult> GetRecent()
        {
            if (!User.Identity.IsAuthenticated) return Json(new List<object>());

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new List<object>());

            var notifications = await _context.UserNotifications
                .Where(n => n.UserId == user.Id)
                .OrderByDescending(n => n.CreatedAt)
                .Take(5)
                .Select(n => new {
                    id = n.Id,
                    title = n.Title,
                    message = n.Message,
                    timeAgo = n.TimeAgo,
                    isRead = n.IsRead,
                    link = n.Link,
                    type = n.Type
                })
                .ToListAsync();

            return Json(notifications);
        }

        [HttpPost("MarkAsRead")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new { success = false });

            var notif = await _context.UserNotifications
                .FirstOrDefaultAsync(n => n.Id == id && n.UserId == user.Id);

            if (notif != null)
            {
                notif.IsRead = true;
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            return Json(new { success = false });
        }

        [HttpPost("ClearAll")]
        public async Task<IActionResult> ClearAll()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new { success = false });

            var notifs = _context.UserNotifications.Where(n => n.UserId == user.Id);

            _context.UserNotifications.RemoveRange(notifs);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }
    }
}