using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Diska.Services;
using Microsoft.AspNetCore.Identity;
using Diska.Models;

namespace Diska.Filters
{
    // فلتر للتحقق من الصلاحيات ديناميكياً قبل فتح أي صفحة
    public class CheckPermissionAttribute : TypeFilterAttribute
    {
        public CheckPermissionAttribute(string module, string action) : base(typeof(CheckPermissionFilter))
        {
            Arguments = new object[] { module, action };
        }
    }

    public class CheckPermissionFilter : IAsyncAuthorizationFilter
    {
        private readonly string _module;
        private readonly string _action;
        private readonly IPermissionService _permissionService;
        private readonly UserManager<ApplicationUser> _userManager;

        public CheckPermissionFilter(string module, string action, IPermissionService permissionService, UserManager<ApplicationUser> userManager)
        {
            _module = module;
            _action = action;
            _permissionService = permissionService;
            _userManager = userManager;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var user = await _userManager.GetUserAsync(context.HttpContext.User);

            // إذا كان أدمن، اسمح له
            if (user != null && await _userManager.IsInRoleAsync(user, "Admin")) return;

            // إذا كان تاجر أو موظف، تحقق من المصفوفة
            if (user != null)
            {
                // نستخدم الـ ID الخاص بالتاجر (إذا كان موظف نستخدم ParentId لو متاح، أو نفس الـ ID لو الصلاحيات مباشرة)
                bool hasPermission = await _permissionService.UserHasPermissionAsync(user.Id, _module, _action);

                if (!hasPermission)
                {
                    context.Result = new RedirectToActionResult("AccessDenied", "Account", new { area = "" });
                }
            }
        }
    }
}