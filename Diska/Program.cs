using Diska.Data;
using Diska.Services;
using Diska.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Localization;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// 1. الاتصال بقاعدة البيانات
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// 2. تسجيل الخدمات
builder.Services.AddScoped<INotificationService, NotificationService>();

// 3. إعداد Identity (تم تعديل شروط كلمة المرور هنا)
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // إعدادات الدخول
    options.SignIn.RequireConfirmedAccount = false;

    // تبسيط كلمة المرور (مهم جداً لحل مشكلتك)
    options.Password.RequireDigit = false; // لا يشترط أرقام
    options.Password.RequireLowercase = false; // لا يشترط حروف صغيرة
    options.Password.RequireUppercase = false; // لا يشترط حروف كبيرة
    options.Password.RequireNonAlphanumeric = false; // لا يشترط رموز (!@#)
    options.Password.RequiredLength = 6; // الحد الأدنى 6 خانات فقط

    // جعل رقم الهاتف هو المعرف الفريد
    options.User.RequireUniqueEmail = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddControllersWithViews()
    .AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix)
    .AddDataAnnotationsLocalization();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Seeding
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        context.Database.Migrate();
        await DbSeeder.SeedRolesAndAdminAsync(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

var supportedCultures = new[] { new CultureInfo("ar-EG"), new CultureInfo("en-US") };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("ar-EG"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();