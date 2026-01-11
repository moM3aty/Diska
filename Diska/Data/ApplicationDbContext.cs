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
        public DbSet<ProductImage> ProductImages { get; set; }
        public DbSet<ProductColor> ProductColors { get; set; }
        public DbSet<PriceTier> PriceTiers { get; set; }

        // جداول الطلبات والعمليات
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<GroupDeal> GroupDeals { get; set; }
        public DbSet<DealRequest> DealRequests { get; set; }
        public DbSet<MerchantOffer> MerchantOffers { get; set; }

        // جداول المستخدمين والدعم
        public DbSet<UserAddress> UserAddresses { get; set; }
        public DbSet<WishlistItem> WishlistItems { get; set; }
        public DbSet<ContactMessage> ContactMessages { get; set; }
        public DbSet<UserNotification> UserNotifications { get; set; }
        public DbSet<ProductReview> ProductReviews { get; set; }
        public DbSet<RestockSubscription> RestockSubscriptions { get; set; }
        public DbSet<WalletTransaction> WalletTransactions { get; set; }

        // جداول النظام والإدارة
        public DbSet<Banner> Banners { get; set; }
        public DbSet<Survey> Surveys { get; set; }
        public DbSet<SurveyQuestion> SurveyQuestions { get; set; }
        public DbSet<SurveyResponse> SurveyResponses { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }

        // جداول صلاحيات التجار
        public DbSet<MerchantPermission> MerchantPermissions { get; set; }
        public DbSet<PendingMerchantAction> PendingMerchantActions { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // ضبط دقة الأرقام العشرية (Currency)
            foreach (var property in builder.Model.GetEntityTypes()
                .SelectMany(t => t.GetProperties())
                .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
            {
                property.SetColumnType("decimal(18,2)");
            }

            // --- حل مشاكل الحذف التعاقبي (Multiple Cascade Paths) ---
            // نستخدم DeleteBehavior.NoAction لمنع الدورات (Cycles)

            // 1. Order -> User (العميل)
            builder.Entity<Order>()
                .HasOne(o => o.User)
                .WithMany()
                .HasForeignKey(o => o.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            // 2. DealRequest -> User (صاحب الطلب)
            builder.Entity<DealRequest>()
               .HasOne(r => r.User)
               .WithMany()
               .HasForeignKey(r => r.UserId)
               .OnDelete(DeleteBehavior.NoAction);

            // 3. MerchantOffer -> Merchant (التاجر)
            // هذا هو الحل للخطأ الذي ظهر لك FK_MerchantOffers_DealRequests_DealRequestId
            builder.Entity<MerchantOffer>()
                .HasOne(m => m.Merchant)
                .WithMany()
                .HasForeignKey(m => m.MerchantId)
                .OnDelete(DeleteBehavior.NoAction);

            // 4. ProductReview -> User (المقيم)
            builder.Entity<ProductReview>()
                .HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            // 5. RestockSubscription -> User (المشترك)
            builder.Entity<RestockSubscription>()
               .HasOne<ApplicationUser>()
               .WithMany()
               .HasForeignKey(s => s.UserId)
               .OnDelete(DeleteBehavior.NoAction);

            // 6. WalletTransaction -> User (صاحب المحفظة)
            builder.Entity<WalletTransaction>()
                .HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            // 7. PendingMerchantAction -> Merchant
            builder.Entity<PendingMerchantAction>()
                .HasOne(p => p.Merchant)
                .WithMany()
                .HasForeignKey(p => p.MerchantId)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}