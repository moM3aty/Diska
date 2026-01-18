using Diska.Data;
using Diska.Models;
using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

namespace Diska.Services
{
    public interface IAuditService
    {
        // تم تحديث التوقيع ليطابق استدعائك
        Task LogAsync(string userId, string action, string entityName, string entityId, string details, string ipAddress = null);
    }

    public class AuditService : IAuditService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task LogAsync(string userId, string action, string entityName, string entityId, string details, string ipAddress = null)
        {
            // إذا لم يتم تمرير IP، نحاول جلبه من السياق الحالي
            if (string.IsNullOrEmpty(ipAddress))
            {
                ipAddress = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "Unknown";
            }

            var log = new AuditLog
            {
                UserId = userId ?? "System",
                Action = action,
                EntityName = entityName,
                EntityId = entityId,
                Details = details,
                IpAddress = ipAddress,
                Timestamp = DateTime.Now
            };

            _context.AuditLogs.Add(log);
            await _context.SaveChangesAsync();
        }
    }
}