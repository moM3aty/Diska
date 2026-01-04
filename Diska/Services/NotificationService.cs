using Diska.Data;
using Diska.Models;
using Microsoft.AspNetCore.Identity;

namespace Diska.Services
{
    // خدمة يمكن حقنها في أي Controller لإرسال إشعار بسهولة
    public interface INotificationService
    {
        Task NotifyUserAsync(string userId, string title, string message, string type = "Info", string link = "#");
        Task NotifyAdminsAsync(string title, string message, string link = "#");
    }

    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public NotificationService(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task NotifyUserAsync(string userId, string title, string message, string type = "Info", string link = "#")
        {
            var notif = new UserNotification
            {
                UserId = userId,
                Title = title,
                Message = message,
                Type = type,
                Link = link,
                CreatedAt = DateTime.Now,
                IsRead = false
            };

            _context.Add(notif); // سنحتاج لإضافة DbSet<UserNotification> في DbContext
            await _context.SaveChangesAsync();
        }

        public async Task NotifyAdminsAsync(string title, string message, string link = "#")
        {
            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            foreach (var admin in admins)
            {
                await NotifyUserAsync(admin.Id, title, message, "Alert", link);
            }
        }
    }
}