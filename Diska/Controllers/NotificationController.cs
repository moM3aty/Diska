using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Diska.Models;
using Microsoft.AspNetCore.Authorization;

namespace Diska.Controllers
{
    [Authorize]
    [Route("Notification")]
    public class NotificationController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public NotificationController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Returns count of unread notifications for the badge
        [HttpGet("GetUnreadCount")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(0);

            var count = await _context.UserNotifications
                .Where(n => n.UserId == user.Id && !n.IsRead)
                .CountAsync();

            return Json(count);
        }

        // Returns recent notifications as JSON
        [HttpGet("GetRecent")]
        public async Task<IActionResult> GetRecent()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new List<object>());

            var notifications = await _context.UserNotifications
                .Where(n => n.UserId == user.Id)
                .OrderByDescending(n => n.CreatedAt)
                .Take(20)
                .Select(n => new {
                    id = n.Id,
                    title = n.Title,
                    message = n.Message,
                    timeAgo = n.TimeAgo,
                    isRead = n.IsRead,
                    type = n.Type
                })
                .ToListAsync();

            return Json(notifications);
        }

        [HttpPost("MarkAsRead")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var user = await _userManager.GetUserAsync(User);
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
            var notifs = _context.UserNotifications.Where(n => n.UserId == user.Id);

            _context.UserNotifications.RemoveRange(notifs);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpGet("Index")]
        public IActionResult Index()
        {
            return View(); // يعرض صفحة Views/Notification/Index.cshtml التي تعتمد على JS
        }
    }
}