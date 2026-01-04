using Microsoft.AspNetCore.Identity;
using Diska.Models;
using Microsoft.EntityFrameworkCore; // هام جداً

namespace Diska.Data
{
    public static class DbSeeder
    {
        public static async Task SeedRolesAndAdminAsync(IServiceProvider service)
        {
            var userManager = service.GetService<UserManager<ApplicationUser>>();
            var roleManager = service.GetService<RoleManager<IdentityRole>>();
            var context = service.GetService<ApplicationDbContext>();

            // 1. إنشاء الأدوار
            if (!await roleManager.RoleExistsAsync("Admin")) await roleManager.CreateAsync(new IdentityRole("Admin"));
            if (!await roleManager.RoleExistsAsync("Merchant")) await roleManager.CreateAsync(new IdentityRole("Merchant"));
            if (!await roleManager.RoleExistsAsync("Customer")) await roleManager.CreateAsync(new IdentityRole("Customer"));

            // 2. إنشاء الأدمن
            var adminEmail = "admin@diska.com";
            var userInDb = await userManager.FindByEmailAsync(adminEmail);
            if (userInDb == null)
            {
                var adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true,
                    PhoneNumber = "01000000000",
                    FullName = "System Administrator",
                    ShopName = "Diska HQ",
                    IsVerifiedMerchant = true
                };
                var result = await userManager.CreateAsync(adminUser, "Admin@123");
                if (result.Succeeded) await userManager.AddToRoleAsync(adminUser, "Admin");
            }

            // 3. إضافة الأقسام الأساسية (Categories) - الحل لمشكلة الكاتيجوري
            if (!context.Categories.Any())
            {
                var categories = new List<Category>
                {
                    new Category { Name = "مشروبات", NameEn = "Beverages", IconClass = "fas fa-bottle-water" },    // ID 1
                    new Category { Name = "سناكس", NameEn = "Snacks", IconClass = "fas fa-cookie-bite" },         // ID 2
                    new Category { Name = "منظفات", NameEn = "Cleaning", IconClass = "fas fa-pump-soap" },        // ID 3
                    new Category { Name = "توصيل سريع", NameEn = "Fast Delivery", IconClass = "fas fa-truck" },   // ID 4
                    new Category { Name = "عروض خاصة", NameEn = "Special Offers", IconClass = "fas fa-box-open" } // ID 5
                };
                context.Categories.AddRange(categories);
                await context.SaveChangesAsync();
            }

            // 4. إضافة منتجات تجريبية (Products)
            if (!context.Products.Any())
            {
                var products = new List<Product>
                {
                    new Product
                    {
                        Name = "بيبسي 2.5 لتر (كرتونة)", NameEn = "Pepsi 2.5L (Carton)",
                        Price = 191.99m, OldPrice = 201.00m, StockQuantity = 100, UnitsPerCarton = 6,
                        Description = "مشروب غازي منعش", DescriptionEn = "Refreshing soft drink",
                        CategoryId = 1, ImageUrl = "images/pepsi.webp"
                    },
                    new Product
                    {
                        Name = "شاي العروسة 40 جم", NameEn = "El Arosa Tea 40g",
                        Price = 206.25m, StockQuantity = 50, UnitsPerCarton = 12,
                        Description = "شاي ناعم", DescriptionEn = "Fine tea",
                        CategoryId = 1, ImageUrl = "images/شاى العروسه.webp"
                    },
                    new Product
                    {
                        Name = "زيت كريستال 800 مل", NameEn = "Crystal Oil 800ml",
                        Price = 450.00m, StockQuantity = 200, UnitsPerCarton = 12,
                        Description = "زيت عباد الشمس", DescriptionEn = "Sunflower oil",
                        CategoryId = 3, ImageUrl = "images/زيت.webp"
                    },
                    new Product
                    {
                        Name = "حليب بخيره 500 مل", NameEn = "Bekhero Milk 500ml",
                        Price = 482.75m, OldPrice = 532.00m, StockQuantity = 80, UnitsPerCarton = 24,
                        Description = "حليب كامل الدسم", DescriptionEn = "Full cream milk",
                        CategoryId = 1, ImageUrl = "images/حليب.webp"
                    }
                };
                context.Products.AddRange(products);
                await context.SaveChangesAsync();
            }
        }
    }
}