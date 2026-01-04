using Microsoft.AspNetCore.Identity;
using Diska.Models;
using Microsoft.EntityFrameworkCore;

namespace Diska.Data
{
    public static class DbSeeder
    {
        public static async Task SeedRolesAndAdminAsync(IServiceProvider service)
        {
            var userManager = service.GetService<UserManager<ApplicationUser>>();
            var roleManager = service.GetService<RoleManager<IdentityRole>>();
            var context = service.GetService<ApplicationDbContext>();

            // 1. إنشاء الأدوار (Roles)
            string[] roles = { "Admin", "Merchant", "Customer" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // 2. إنشاء حساب الأدمن الافتراضي
            var adminPhone = "01000000000"; // رقم الموبايل للدخول
            var adminUser = await userManager.FindByNameAsync(adminPhone);

            if (adminUser == null)
            {
                var newAdmin = new ApplicationUser
                {
                    UserName = adminPhone,
                    PhoneNumber = adminPhone,
                    Email = "admin@diska.com", // وهمي لمتطلبات Identity
                    EmailConfirmed = true,
                    FullName = "System Admin",
                    ShopName = "Diska Management",
                    IsVerifiedMerchant = true,
                    WalletBalance = 0,
                    CommercialRegister = "000000",
                    TaxCard = "000-000-000"
                };

                var result = await userManager.CreateAsync(newAdmin, "Admin@123");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(newAdmin, "Admin");
                }
            }

            // 3. إضافة الأقسام الأساسية إذا كانت فارغة
            if (!context.Categories.Any())
            {
                context.Categories.AddRange(new List<Category>
                {
                    new Category { Name = "بقالة جافة", NameEn = "Dry Grocery", IconClass = "fas fa-utensils" },
                    new Category { Name = "مشروبات وعصائر", NameEn = "Beverages", IconClass = "fas fa-wine-bottle" },
                    new Category { Name = "منظفات وعناية", NameEn = "Cleaning & Care", IconClass = "fas fa-pump-soap" },
                    new Category { Name = "حلويات وسناكس", NameEn = "Sweets & Snacks", IconClass = "fas fa-cookie-bite" },
                    new Category { Name = "منتجات ألبان", NameEn = "Dairy Products", IconClass = "fas fa-cheese" }
                });
                await context.SaveChangesAsync();
            }
        }
    }
}