using Diska.Data;
using Microsoft.EntityFrameworkCore;

namespace Diska.Services
{
    public interface IPermissionService
    {
        Task<bool> UserHasPermissionAsync(string userId, string module, string action);
    }

    public class PermissionService : IPermissionService
    {
        private readonly ApplicationDbContext _context;

        public PermissionService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<bool> UserHasPermissionAsync(string userId, string module, string action)
        {
            // 1. التحقق من صلاحيات التاجر/الموظف المسجلة في الجدول
            var perm = await _context.MerchantPermissions
                .FirstOrDefaultAsync(p => p.MerchantId == userId && p.Module == module);

            if (perm == null) return false;

            return action switch
            {
                "View" => perm.CanView,
                "Create" => perm.CanCreate,
                "Edit" => perm.CanEdit,
                "Delete" => perm.CanDelete,
                _ => false
            };
        }
    }
}