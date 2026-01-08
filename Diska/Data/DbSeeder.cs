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

            // 1. الأدوار
            string[] roles = { "Admin", "Merchant", "Customer" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // 2. الأدمن (موجود سابقاً)
            var adminPhone = "01000000000";
            var adminUser = await userManager.FindByNameAsync(adminPhone);
            if (adminUser == null)
            {
                var newAdmin = new ApplicationUser
                {
                    UserName = adminPhone,
                    PhoneNumber = adminPhone,
                    Email = "admin@diska.com",
                    EmailConfirmed = true,
                    FullName = "System Admin",
                    ShopName = "Diska Management",
                    IsVerifiedMerchant = true,
                    CommercialRegister = "000000",
                    TaxCard = "000-000-000"
                };
                var result = await userManager.CreateAsync(newAdmin, "Admin@123");
                if (result.Succeeded) await userManager.AddToRoleAsync(newAdmin, "Admin");
            }

            // 3. (جديد) إضافة تاجر للتجربة
            var merchantPhone = "01000000001"; // رقم التاجر
            var merchantUser = await userManager.FindByNameAsync(merchantPhone);
            if (merchantUser == null)
            {
                var newMerchant = new ApplicationUser
                {
                    UserName = merchantPhone,
                    PhoneNumber = merchantPhone,
                    Email = "merchant@diska.com",
                    EmailConfirmed = true,
                    FullName = "تاجر تجريبي",
                    ShopName = "مخازن الأمانة",
                    WalletBalance = 5000,
                    IsVerifiedMerchant = true, // مفعل جاهز
                    CommercialRegister = "123456",
                    TaxCard = "987-654-321"
                };
                // كلمة المرور: Merchant@123
                var result = await userManager.CreateAsync(newMerchant, "Merchant@123");
                if (result.Succeeded) await userManager.AddToRoleAsync(newMerchant, "Merchant");
            }

            // 4. الأقسام
            if (!context.Categories.Any())
            {
                context.Categories.AddRange(new List<Category>
                {
                    new Category { Name = "أدوات مكتبية", NameEn = "Stationery", IconClass = "fas fa-pen-ruler" },
                    new Category { Name = "بقالة", NameEn = "Grocery", IconClass = "fas fa-utensils" },
                    new Category { Name = "أدوات منزلية", NameEn = "Home Appliances", IconClass = "fas fa-blender" },
                    new Category { Name = "عناية شخصية", NameEn = "Personal Care", IconClass = "fas fa-pump-soap" },
                    new Category { Name = "منظفات", NameEn = "Detergents", IconClass = "fas fa-spray-can" },
                    new Category { Name = "حيوانات أليفة", NameEn = "Pet Supplies", IconClass = "fas fa-paw" },
                    new Category { Name = "مشروبات", NameEn = "Beverages", IconClass = "fas fa-wine-bottle" }
                });
                await context.SaveChangesAsync();
            }
        }
    }
}