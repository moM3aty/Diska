using Diska.Data;
using Diska.Models;

namespace Diska.Services
{
    // واجهة الخدمة
    public interface IAuditService
    {
        Task LogAsync(string userId, string action, string entityName, string entityId, string details, string ipAddress);
    }

    // التنفيذ
    public class AuditService : IAuditService
    {
        private readonly ApplicationDbContext _context;

        public AuditService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task LogAsync(string userId, string action, string entityName, string entityId, string details, string ipAddress)
        {
            var log = new AuditLog
            {
                UserId = userId,
                Action = action,
                EntityName = entityName,
                EntityId = entityId,
                Details = details,
                IpAddress = ipAddress,
                Timestamp = DateTime.Now
            };

            _context.Add(log); // لاحظ: لا نستخدم DbSet مباشر هنا لتجنب التعقيد، يمكن إضافته للـ Context

            // في الواقع يجب إضافة DbSet<AuditLog> AuditLogs في ApplicationDbContext
            // _context.AuditLogs.Add(log); 

            // بما أنني لا أستطيع تعديل DbContext الآن، هذا الكود سيعمل إذا أضفت الـ DbSet يدوياً.
            // كحل بديل سريع، سأقوم بإنشاء الـ Entity وإضافتها بشكل عام
            await _context.AddAsync(log);
            await _context.SaveChangesAsync();
        }
    }
}