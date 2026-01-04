using Microsoft.AspNetCore.Identity;
using Diska.Models;

namespace Diska.Data
{
    public static class DbSeeder
    {
        public static async Task SeedRolesAndAdminAsync(IServiceProvider service)
        {
            var userManager = service.GetService<UserManager<IdentityUser>>();
            var roleManager = service.GetService<RoleManager<IdentityRole>>();

            // 1. إنشاء الأدوار (Roles)
            await roleManager.CreateAsync(new IdentityRole("Admin"));
            await roleManager.CreateAsync(new IdentityRole("Merchant"));
            await roleManager.CreateAsync(new IdentityRole("Customer"));

            // 2. إنشاء حساب الأدمن الافتراضي
            var adminUser = new IdentityUser
            {
                UserName = "admin@diska.com",
                Email = "admin@diska.com",
                EmailConfirmed = true,
                PhoneNumber = "01000000000"
            };

            var userInDb = await userManager.FindByEmailAsync(adminUser.Email);
            if (userInDb == null)
            {
                // كلمة المرور يجب أن تحتوي على حروف كبيرة وصغيرة وأرقام ورموز
                await userManager.CreateAsync(adminUser, "Admin@123");
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }
        }
    }
}