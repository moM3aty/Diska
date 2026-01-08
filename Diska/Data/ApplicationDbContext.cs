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
        public DbSet<ProductColor> ProductColors { get; set; }
        public DbSet<ProductImage> ProductImages { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<PriceTier> PriceTiers { get; set; }
        public DbSet<UserAddress> UserAddresses { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<ContactMessage> ContactMessages { get; set; }
        public DbSet<DealRequest> DealRequests { get; set; }
        public DbSet<MerchantOffer> MerchantOffers { get; set; }
        public DbSet<WishlistItem> WishlistItems { get; set; }
        public DbSet<GroupDeal> GroupDeals { get; set; }
        public DbSet<UserNotification> UserNotifications { get; set; }
        public DbSet<Banner> Banners { get; set; }
        public DbSet<Survey> Surveys { get; set; }
        public DbSet<SurveyQuestion> SurveyQuestions { get; set; }
        public DbSet<SurveyResponse> SurveyResponses { get; set; }
        public DbSet<ProductReview> ProductReviews { get; set; }
        public DbSet<RestockSubscription> RestockSubscriptions { get; set; }
        public DbSet<WalletTransaction> WalletTransactions { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            foreach (var property in builder.Model.GetEntityTypes()
                .SelectMany(t => t.GetProperties())
                .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
            {
                property.SetColumnType("decimal(18,2)");
            }


            builder.Entity<Order>()
                .HasOne(o => o.User)
                .WithMany()
                .HasForeignKey(o => o.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<ProductReview>()
                .HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<MerchantOffer>()
                .HasOne(m => m.Merchant)
                .WithMany()
                .HasForeignKey(m => m.MerchantId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<DealRequest>()
               .HasOne<ApplicationUser>()
               .WithMany()
               .HasForeignKey(r => r.UserId)
               .OnDelete(DeleteBehavior.NoAction);


            builder.Entity<RestockSubscription>()
               .HasOne<ApplicationUser>()
               .WithMany()
               .HasForeignKey(s => s.UserId)
               .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<WalletTransaction>()
                .HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<Survey>()
                .HasMany(s => s.Questions).WithOne().HasForeignKey(q => q.SurveyId).OnDelete(DeleteBehavior.Cascade);
            builder.Entity<Survey>()
                .HasMany(s => s.Responses).WithOne().HasForeignKey(r => r.SurveyId).OnDelete(DeleteBehavior.Cascade);

            builder.Entity<DealRequest>()
                .HasMany(r => r.Offers)
                .WithOne(o => o.DealRequest)
                .HasForeignKey(o => o.DealRequestId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}