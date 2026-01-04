using Diska.Data;
using Diska.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace Diska.Services
{
    // Interface Definition
    public interface INotificationService
    {
        Task NotifyUserAsync(string userId, string title, string message, string type = "Info", string link = "#");
        Task NotifyAdminsAsync(string title, string message, string link = "#");
        Task NotifyMerchantsAsync(string title, string message, string link = "#");
    }

    // Implementation
    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public NotificationService(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
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

            _context.UserNotifications.Add(notif);
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

        public async Task NotifyMerchantsAsync(string title, string message, string link = "#")
        {
            var merchants = await _userManager.GetUsersInRoleAsync("Merchant");
            foreach (var merchant in merchants)
            {
                await NotifyUserAsync(merchant.Id, title, message, "System", link);
            }
        }
    }
}