using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Diska.Models;

namespace Diska.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // الجداول الأساسية
        public DbSet<Product> Products { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<PriceTier> PriceTiers { get; set; }

        // جداول الطلبات
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }

        // جداول التفاعل والخدمات
        public DbSet<ContactMessage> ContactMessages { get; set; }
        public DbSet<DealRequest> DealRequests { get; set; }
        public DbSet<WishlistItem> WishlistItems { get; set; }
        public DbSet<GroupDeal> GroupDeals { get; set; }
        public DbSet<UserNotification> UserNotifications { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // ضبط دقة الأرقام العشرية (العملات) لتجنب أخطاء SQL
            foreach (var property in builder.Model.GetEntityTypes()
                .SelectMany(t => t.GetProperties())
                .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
            {
                property.SetColumnType("decimal(18,2)");
            }

            // علاقات إضافية (اختياري)
            builder.Entity<Order>()
                .HasOne(o => o.User)
                .WithMany()
                .HasForeignKey(o => o.UserId)
                .OnDelete(DeleteBehavior.NoAction); // تجنب الدورات في الحذف
        }
    }
}