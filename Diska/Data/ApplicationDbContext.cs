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

        public DbSet<Product> Products { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<PriceTier> PriceTiers { get; set; } 
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<ContactMessage> ContactMessages { get; set; }
        public DbSet<DealRequest> DealRequests { get; set; }
        public DbSet<WishlistItem> WishlistItems { get; set; }
        public DbSet<GroupDeal> GroupDeals { get; set; }
        public DbSet<UserNotification> UserNotifications { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Product>().Property(p => p.Price).HasColumnType("decimal(18,2)");
            builder.Entity<PriceTier>().Property(p => p.UnitPrice).HasColumnType("decimal(18,2)");
            builder.Entity<Order>().Property(o => o.TotalAmount).HasColumnType("decimal(18,2)");
        }
    }
}