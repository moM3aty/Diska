using Diska.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;

namespace Diska.Data
{
    public static class DbSeeder
    {
        public static async Task SeedData(IServiceProvider serviceProvider)
        {
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            // 1. Self-Healing: Fix existing users with null required fields
            var usersWithNullFields = await context.Users
                .Where(u => u.ShopName == null || u.CommercialRegister == null || u.TaxCard == null)
                .ToListAsync();

            if (usersWithNullFields.Any())
            {
                foreach (var user in usersWithNullFields)
                {
                    if (string.IsNullOrEmpty(user.ShopName)) user.ShopName = "متجر " + (user.FullName ?? "مستخدم");
                    if (string.IsNullOrEmpty(user.CommercialRegister)) user.CommercialRegister = "000000";
                    if (string.IsNullOrEmpty(user.TaxCard)) user.TaxCard = "000000";
                }
                await context.SaveChangesAsync();
            }

            // 2. Create Roles
            string[] roles = { "Admin", "Merchant", "Customer" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // 3. Create Users
            // Admin
            var adminUser = await userManager.FindByEmailAsync("admin@diska.com");
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = "01000000000",
                    PhoneNumber = "01000000000",
                    Email = "admin@diska.com",
                    FullName = "Admin User",
                    IsVerifiedMerchant = true,
                    EmailConfirmed = true,
                    ShopName = "إدارة الموقع",
                    CommercialRegister = "000000",
                    TaxCard = "000000",
                    WalletBalance = 0
                };
                var result = await userManager.CreateAsync(adminUser, "Admin@123");
                if (result.Succeeded) await userManager.AddToRoleAsync(adminUser, "Admin");
            }

            // Merchant 1
            var merch1 = await userManager.FindByNameAsync("01111111111");
            if (merch1 == null)
            {
                merch1 = new ApplicationUser
                {
                    UserName = "01111111111",
                    PhoneNumber = "01111111111",
                    Email = "tech@diska.com",
                    FullName = "أحمد محمد",
                    ShopName = "تكنو ستور",
                    IsVerifiedMerchant = true,
                    WalletBalance = 5000,
                    EmailConfirmed = true,
                    CommercialRegister = "123456",
                    TaxCard = "987654"
                };
                var result = await userManager.CreateAsync(merch1, "Merch@123");
                if (result.Succeeded) await userManager.AddToRoleAsync(merch1, "Merchant");
            }

            // Merchant 2
            var merch2 = await userManager.FindByNameAsync("01222222222");
            if (merch2 == null)
            {
                merch2 = new ApplicationUser
                {
                    UserName = "01222222222",
                    PhoneNumber = "01222222222",
                    Email = "baraka@diska.com",
                    FullName = "سعيد علي",
                    ShopName = "أسواق البركة",
                    IsVerifiedMerchant = true,
                    WalletBalance = 1200,
                    EmailConfirmed = true,
                    CommercialRegister = "654321",
                    TaxCard = "456789"
                };
                var result = await userManager.CreateAsync(merch2, "Merch@123");
                if (result.Succeeded) await userManager.AddToRoleAsync(merch2, "Merchant");
            }

            // Customer
            var cust = await userManager.FindByNameAsync("01099999999");
            if (cust == null)
            {
                cust = new ApplicationUser
                {
                    UserName = "01099999999",
                    PhoneNumber = "01099999999",
                    Email = "customer@diska.com",
                    FullName = "عميل تجريبي",
                    ShopName = "شخصي",
                    IsVerifiedMerchant = false,
                    WalletBalance = 2000,
                    EmailConfirmed = true,
                    CommercialRegister = "000000",
                    TaxCard = "000000"
                };
                var result = await userManager.CreateAsync(cust, "Guest@123");
                if (result.Succeeded) await userManager.AddToRoleAsync(cust, "Customer");
            }

            // 4. Create Categories
            var categoriesList = new List<Category>
            {
                new Category { Name = "أجهزة إلكترونية", NameEn = "Electronics", IconClass = "fas fa-laptop", Slug = "electronics", DisplayOrder = 1, ImageUrl = "images/categories/electronics.png", MetaTitle = "Electronics", MetaDescription = "Best Electronics", IsActive = true },
                new Category { Name = "سوبر ماركت", NameEn = "Grocery", IconClass = "fas fa-shopping-basket", Slug = "grocery", DisplayOrder = 2, ImageUrl = "images/categories/grocery.png", MetaTitle = "Grocery", MetaDescription = "Fresh Food", IsActive = true },
                new Category { Name = "ملابس وأزياء", NameEn = "Fashion", IconClass = "fas fa-tshirt", Slug = "fashion", DisplayOrder = 3, ImageUrl = "images/categories/fashion.png", MetaTitle = "Fashion", MetaDescription = "Trendy Clothes", IsActive = true },
                new Category { Name = "مستلزمات منزلية", NameEn = "Home", IconClass = "fas fa-couch", Slug = "home", DisplayOrder = 4, ImageUrl = "images/categories/home.png", MetaTitle = "Home", MetaDescription = "Home Decor", IsActive = true },
                new Category { Name = "صحة وجمال", NameEn = "Health", IconClass = "fas fa-heartbeat", Slug = "health", DisplayOrder = 5, ImageUrl = "images/categories/health.png", MetaTitle = "Health", MetaDescription = "Beauty Products", IsActive = true },
                new Category { Name = "رياضة", NameEn = "Sports", IconClass = "fas fa-dumbbell", Slug = "sports", DisplayOrder = 6, ImageUrl = "images/categories/sports.png", MetaTitle = "Sports", MetaDescription = "Gym Gear", IsActive = true }
            };

            foreach (var cat in categoriesList)
            {
                var existingCat = await context.Categories.FirstOrDefaultAsync(c => c.Slug == cat.Slug);
                if (existingCat == null)
                {
                    context.Categories.Add(cat);
                }
                else
                {
                    // Update missing fields
                    bool updated = false;
                    if (string.IsNullOrEmpty(existingCat.ImageUrl)) { existingCat.ImageUrl = cat.ImageUrl; updated = true; }
                    if (string.IsNullOrEmpty(existingCat.MetaDescription)) { existingCat.MetaDescription = cat.MetaDescription; updated = true; }

                    if (updated) context.Update(existingCat);
                }
            }
            await context.SaveChangesAsync();

            // 5. Create Products
            var elecCat = await context.Categories.FirstOrDefaultAsync(c => c.Slug == "electronics");
            var grocCat = await context.Categories.FirstOrDefaultAsync(c => c.Slug == "grocery");
            var catFash = await context.Categories.FirstOrDefaultAsync(c => c.Slug == "fashion");
            var catHome = await context.Categories.FirstOrDefaultAsync(c => c.Slug == "home");

            // Re-fetch users
            merch1 = await userManager.FindByNameAsync("01111111111");
            merch2 = await userManager.FindByNameAsync("01222222222");

            if (elecCat != null && grocCat != null && catFash != null && merch1 != null && merch2 != null)
            {
                var products = new List<Product>
                {
                    // Electronics
                    new Product {
                        Name = "ايفون 15", NameEn = "iPhone 15", Price = 45000, StockQuantity = 20, SKU = "IP15",
                        CategoryId = elecCat.Id, MerchantId = merch1.Id, Status = "Active",
                        ImageUrl = "images/products/iphone.png", Description = "موبايل ايفون", DescriptionEn = "iPhone Mobile",
                        Slug = "iphone-15", MetaTitle = "iPhone", MetaDescription = "Apple iPhone", Brand = "Apple",
                        Barcode = "1234567890123", Color = "#000000"
                    },
                    new Product {
                        Name = "لابتوب ديل", NameEn = "Dell Laptop", Price = 35000, StockQuantity = 10, SKU = "DELL",
                        CategoryId = elecCat.Id, MerchantId = merch1.Id, Status = "Active",
                        ImageUrl = "images/products/dell.png", Description = "لابتوب قوي", DescriptionEn = "Powerful Laptop",
                        Slug = "dell-laptop", MetaTitle = "Dell", MetaDescription = "Dell Laptop", Brand = "Dell",
                        Barcode = "1234567890124", Color = "#C0C0C0"
                    },
                    // Grocery
                    new Product {
                        Name = "أرز 5 كيلو", NameEn = "Rice 5KG", Price = 150, StockQuantity = 100, SKU = "RICE5",
                        CategoryId = grocCat.Id, MerchantId = merch2.Id, Status = "Active",
                        ImageUrl = "images/products/rice.png", Description = "أرز مصري", DescriptionEn = "Egyptian Rice",
                        Slug = "rice-5kg", MetaTitle = "Rice", MetaDescription = "Food", Brand = "Doha",
                        Barcode = "1234567890125", Color = "#FFFFFF"
                    },
                    // Fashion (New)
                    new Product {
                        Name = "تيشيرت بولو", NameEn = "Polo T-Shirt", Price = 350, StockQuantity = 50, SKU = "TSHIRT",
                        CategoryId = catFash.Id, MerchantId = merch1.Id, Status = "Active",
                        ImageUrl = "images/products/tshirt.png", Description = "قطن 100%", DescriptionEn = "100% Cotton",
                        Slug = "polo-shirt", MetaTitle = "T-Shirt", MetaDescription = "Fashion", Brand = "Polo",
                        Barcode = "1234567890126", Color = "#0000FF"
                    },
                    // Home (New)
                    new Product {
                        Name = "طقم أطباق", NameEn = "Dinner Set", Price = 2500, StockQuantity = 15, SKU = "DISHES",
                        CategoryId = catHome.Id, MerchantId = merch2.Id, Status = "Active",
                        ImageUrl = "images/products/dishes.png", Description = "بورسلين فاخر", DescriptionEn = "Fine Porcelain",
                        Slug = "dinner-set", MetaTitle = "Dishes", MetaDescription = "Kitchenware", Brand = "Luminarc",
                        Barcode = "1234567890127", Color = "#FFFFFF"
                    }
                };

                foreach (var p in products)
                {
                    var existingProd = await context.Products.FirstOrDefaultAsync(x => x.SKU == p.SKU);
                    if (existingProd == null)
                    {
                        context.Products.Add(p);
                    }
                    else
                    {
                        // Update missing fields
                        bool updated = false;
                        if (string.IsNullOrEmpty(existingProd.Barcode)) { existingProd.Barcode = p.Barcode; updated = true; }
                        if (string.IsNullOrEmpty(existingProd.Color)) { existingProd.Color = p.Color; updated = true; }

                        if (updated) context.Update(existingProd);
                    }
                }
                await context.SaveChangesAsync();
            }

            // 6. Create Banners
            if (!await context.Banners.AnyAsync())
            {
                var banners = new List<Banner>
                {
                    new Banner
                    {
                        Title = "عروض الصيف",
                        TitleEn = "Summer Sale",
                        Subtitle = "خصومات حصرية",
                        SubtitleEn = "Exclusive Discounts",
                        ButtonText = "تسوق الآن",
                        ButtonTextEn = "Shop Now",
                        ImageDesktop = "images/banners/summer.png",
                        ImageMobile = "images/banners/summer_mob.png",
                        LinkType = "External",
                        LinkId = "#",
                        Priority = 1,
                        IsActive = true,
                        StartDate = DateTime.Now,
                        EndDate = DateTime.Now.AddDays(30)
                    },
                    new Banner
                    {
                        Title = "إلكترونيات حديثة",
                        TitleEn = "Modern Electronics",
                        Subtitle = "أحدث الأجهزة",
                        SubtitleEn = "Latest Gadgets",
                        ButtonText = "اكتشف المزيد",
                        ButtonTextEn = "Discover More",
                        ImageDesktop = "images/banners/electronics.png",
                        ImageMobile = "images/banners/electronics_mob.png",
                        LinkType = "Category",
                        LinkId = elecCat?.Id.ToString(),
                        Priority = 2,
                        IsActive = true,
                        StartDate = DateTime.Now,
                        EndDate = DateTime.Now.AddDays(30)
                    }
                };

                context.Banners.AddRange(banners);
                await context.SaveChangesAsync();
            }
            else
            {
                // Self-healing for banners
                var banners = await context.Banners.Where(b => b.ImageMobile == null || b.Subtitle == null).ToListAsync();
                foreach (var b in banners)
                {
                    if (b.ImageMobile == null) b.ImageMobile = b.ImageDesktop ?? "images/default.png";
                    if (b.Subtitle == null) b.Subtitle = "";
                }
                if (banners.Any()) await context.SaveChangesAsync();
            }

            // 7. Group Deals
            if (!await context.GroupDeals.AnyAsync())
            {
                var product = await context.Products.FirstOrDefaultAsync(p => p.SKU == "IP15");
                if (product != null)
                {
                    context.GroupDeals.Add(new GroupDeal
                    {
                        Title = "عرض الجمعة البيضاء",
                        ProductId = product.Id,
                        DiscountValue = 10,
                        IsPercentage = true,
                        DealPrice = product.Price * 0.9m,
                        TargetQuantity = 50,
                        ReservedQuantity = 5,
                        StartDate = DateTime.Now,
                        EndDate = DateTime.Now.AddDays(7),
                        IsActive = true
                    });
                    await context.SaveChangesAsync();
                }
            }

            // 8. Create Orders (Missing in previous version!)
            cust = await userManager.FindByNameAsync("01099999999");
            if (cust != null && !await context.Orders.AnyAsync(o => o.UserId == cust.Id))
            {
                var prod1 = await context.Products.FirstOrDefaultAsync(p => p.SKU == "IP15");
                var prod2 = await context.Products.FirstOrDefaultAsync(p => p.SKU == "RICE5");

                if (prod1 != null && prod2 != null)
                {
                    var orders = new List<Order>
                    {
                        // Order 1: iPhone (Black Color)
                        new Order {
                            UserId = cust.Id, CustomerName = cust.FullName, Phone = cust.PhoneNumber,
                            Governorate = "Cairo", City = "Nasr City", Address = "123 Main St",
                            PaymentMethod = "Cash", Status = "Delivered", OrderDate = DateTime.Now.AddDays(-5),
                            TotalAmount = prod1.Price + 50, ShippingCost = 50,
                            OrderItems = new List<OrderItem>
                            {
                                new OrderItem {
                                    ProductId = prod1.Id,
                                    Quantity = 1,
                                    UnitPrice = prod1.Price,
                                    SelectedColorName = "أسود فلكي",
                                    SelectedColorHex = "#1a1a1a"
                                }
                            }
                        },
                        // Order 2: Rice (No specific color, maybe default) & iPhone (Blue)
                        new Order {
                            UserId = cust.Id, CustomerName = cust.FullName, Phone = cust.PhoneNumber,
                            Governorate = "Giza", City = "Dokki", Address = "456 Side St",
                            PaymentMethod = "Wallet", Status = "Pending", OrderDate = DateTime.Now.AddHours(-2),
                            TotalAmount = (prod2.Price * 5) + prod1.Price + 50, ShippingCost = 50,
                            OrderItems = new List<OrderItem>
                            {
                                new OrderItem {
                                    ProductId = prod2.Id,
                                    Quantity = 5,
                                    UnitPrice = prod2.Price,
                                    SelectedColorName = "أبيض", // Rice bag color example
                                    SelectedColorHex = "#ffffff"
                                },
                                new OrderItem {
                                    ProductId = prod1.Id,
                                    Quantity = 1,
                                    UnitPrice = prod1.Price,
                                    SelectedColorName = "أزرق تيتانيوم",
                                    SelectedColorHex = "#2f3e59"
                                }
                            }
                        }
                    };
                    context.Orders.AddRange(orders);
                    await context.SaveChangesAsync();
                }
            }

            // 9. PendingMerchantActions
            if (!await context.PendingMerchantActions.AnyAsync() && merch1 != null)
            {
                var prod = await context.Products.FirstOrDefaultAsync(p => p.MerchantId == merch1.Id);
                if (prod != null)
                {
                    context.PendingMerchantActions.Add(new PendingMerchantAction
                    {
                        MerchantId = merch1.Id,
                        ActionType = "UpdateProductPrice",
                        EntityName = "Product",
                        EntityId = prod.Id.ToString(),
                        OldValueJson = JsonSerializer.Serialize(new { Price = prod.Price }),
                        NewValueJson = JsonSerializer.Serialize(new { Price = prod.Price + 500 }),
                        Status = "Pending",
                        RequestDate = DateTime.Now.AddHours(-2),
                        ActionByAdminId = "System"
                    });
                    await context.SaveChangesAsync();
                }
            }

            // 10. RestockSubscriptions
            if (!await context.RestockSubscriptions.AnyAsync() && cust != null && merch2 != null)
            {
                var prod = await context.Products.FirstOrDefaultAsync(p => p.MerchantId == merch2.Id);
                if (prod != null)
                {
                    context.RestockSubscriptions.Add(new RestockSubscription
                    {
                        ProductId = prod.Id,
                        UserId = cust.Id,
                        Email = cust.Email,
                        RequestDate = DateTime.Now.AddDays(-1),
                        IsNotified = false
                    });
                    await context.SaveChangesAsync();
                }
            }

            // 11. DealRequests
            if (!await context.DealRequests.AnyAsync() && cust != null)
            {
                context.DealRequests.AddRange(new List<DealRequest>
                {
                    new DealRequest { UserId = cust.Id, ProductName = "زيت عباد الشمس 50 لتر", TargetQuantity = 20, DealPrice = 8000, Location = "الجيزة", Status = "Pending", RequestDate = DateTime.Now.AddDays(-1) },
                    new DealRequest { UserId = cust.Id, ProductName = "سكر أبيض 1 طن", TargetQuantity = 1, DealPrice = 25000, Location = "القاهرة", Status = "Approved", RequestDate = DateTime.Now.AddDays(-3) }
                });
                await context.SaveChangesAsync();
            }

            // 12. ProductReviews
            if (!await context.ProductReviews.AnyAsync() && cust != null)
            {
                var prod = await context.Products.FirstOrDefaultAsync(p => p.SKU == "IP15");
                if (prod != null)
                {
                    context.ProductReviews.Add(new ProductReview
                    {
                        ProductId = prod.Id,
                        UserId = cust.Id,
                        Rating = 5,
                        Comment = "منتج ممتاز وتوصيل سريع جداً!",
                        IsVisible = true,
                        CreatedAt = DateTime.Now.AddDays(-2)
                    });
                    await context.SaveChangesAsync();
                }
            }

            // 13. WalletTransactions
            if (!await context.WalletTransactions.AnyAsync() && merch1 != null && cust != null)
            {
                context.WalletTransactions.AddRange(new List<WalletTransaction>
                {
                    new WalletTransaction { UserId = cust.Id, Amount = 2000, Type = "Deposit", Description = "شحن رصيد مبدئي", TransactionDate = DateTime.Now.AddDays(-5) },
                    new WalletTransaction { UserId = merch1.Id, Amount = 5000, Type = "Sale", Description = "أرباح مبيعات سابقة", TransactionDate = DateTime.Now.AddDays(-2) },
                    new WalletTransaction { UserId = merch1.Id, Amount = -1000, Type = "Withdraw", Description = "سحب أرباح", TransactionDate = DateTime.Now.AddDays(-1) }
                });
                await context.SaveChangesAsync();
            }

            // 14. Surveys
            if (!await context.Surveys.AnyAsync())
            {
                var survey = new Survey
                {
                    Title = "رأيك يهمنا",
                    TitleEn = "Your Feedback Matters",
                    Description = "استطلاع قصير حول تجربتك في الموقع",
                    StartDate = DateTime.Now,
                    EndDate = DateTime.Now.AddDays(10),
                    IsActive = true,
                    TargetAudience = "All"
                };
                context.Surveys.Add(survey);
                await context.SaveChangesAsync();

                context.SurveyQuestions.AddRange(new List<SurveyQuestion>
                {
                    new SurveyQuestion { SurveyId = survey.Id, QuestionText = "كيف تقيم سرعة التوصيل؟", QuestionTextEn = "Rate delivery speed", Type = "Star" },
                    new SurveyQuestion { SurveyId = survey.Id, QuestionText = "هل واجهت أي مشاكل؟", QuestionTextEn = "Any issues?", Type = "YesNo" },
                    new SurveyQuestion { SurveyId = survey.Id, QuestionText = "اقتراحات لتحسين الخدمة", QuestionTextEn = "Suggestions", Type = "Text" }
                });
                await context.SaveChangesAsync();
            }

            // 15. ContactMessages
            if (!await context.ContactMessages.AnyAsync())
            {
                context.ContactMessages.AddRange(new List<ContactMessage>
                {
                    new ContactMessage { Name = "محمد علي", Email = "m.ali@test.com", Phone = "01012345678", Subject = "Inquiry", Message = "هل يوجد شحن لمحافظة أسوان؟", DateSent = DateTime.Now.AddHours(-4) },
                    new ContactMessage { Name = "سارة حسن", Email = "sara@test.com", Phone = "01298765432", Subject = "Complaint", Message = "طلبي تأخر أكثر من يومين.", DateSent = DateTime.Now.AddDays(-1) }
                });
                await context.SaveChangesAsync();
            }

            // 16. AuditLogs
            if (!await context.AuditLogs.AnyAsync() && adminUser != null)
            {
                context.AuditLogs.AddRange(new List<AuditLog>
                {
                    new AuditLog { UserId = adminUser.Id, Action = "Login", EntityName = "System", Details = "تسجيل دخول ناجح", IpAddress = "127.0.0.1", Timestamp = DateTime.Now.AddHours(-1) },
                    new AuditLog { UserId = adminUser.Id, Action = "Update", EntityName = "Product", EntityId = "1", Details = "تعديل سعر منتج ايفون", IpAddress = "127.0.0.1", Timestamp = DateTime.Now.AddMinutes(-30) }
                });
                await context.SaveChangesAsync();
            }
        }
    }
}