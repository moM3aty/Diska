using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Diska.Models;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using System.Linq;

namespace Diska.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IHttpContextAccessor httpContextAccessor)
            : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
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
        public DbSet<RequestMessage> RequestMessages { get; set; }

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
        public DbSet<ShippingRate> ShippingRates { get; set; }

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

            // --- حل مشكلة التكرار في مسارات الحذف (Cascade Paths) ---

            builder.Entity<Order>()
                .HasOne(o => o.User)
                .WithMany()
                .HasForeignKey(o => o.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<DealRequest>()
               .HasOne(r => r.User)
               .WithMany()
               .HasForeignKey(r => r.UserId)
               .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<MerchantOffer>()
                .HasOne(m => m.Merchant)
                .WithMany()
                .HasForeignKey(m => m.MerchantId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<ProductReview>()
                .HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            // 🟢 استخدام <ApplicationUser> مباشرة إذا كانت خاصية Navigation غير موجودة في الموديل
            builder.Entity<RestockSubscription>()
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            // 🟢 استخدام <ApplicationUser> مباشرة إذا كانت خاصية Navigation غير موجودة في الموديل
            builder.Entity<WishlistItem>()
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(w => w.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<WalletTransaction>()
                .HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<PendingMerchantAction>()
                .HasOne(p => p.Merchant)
                .WithMany()
                .HasForeignKey(p => p.MerchantId)
                .OnDelete(DeleteBehavior.NoAction);
        }

        // --- Audit Log Implementation ---
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var auditEntries = OnBeforeSaveChanges();
            var result = await base.SaveChangesAsync(cancellationToken);
            await OnAfterSaveChanges(auditEntries);
            return result;
        }

        private List<AuditEntry> OnBeforeSaveChanges()
        {
            ChangeTracker.DetectChanges();
            var auditEntries = new List<AuditEntry>();

            var userId = _httpContextAccessor?.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "Unknown";

            // ==========================================================
            // ✅ التعديل الهام هنا: تجنب قيمة الـ Null في الـ IP Address
            // ==========================================================
            var ip = _httpContextAccessor?.HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.Entity is AuditLog || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                    continue;

                var auditEntry = new AuditEntry(entry);
                auditEntry.TableName = entry.Entity.GetType().Name;
                auditEntry.UserId = userId;
                auditEntry.IpAddress = ip;
                auditEntries.Add(auditEntry);

                foreach (var property in entry.Properties)
                {
                    if (property.IsTemporary)
                    {
                        auditEntry.TemporaryProperties.Add(property);
                        continue;
                    }

                    string propertyName = property.Metadata.Name;
                    if (property.Metadata.IsPrimaryKey())
                    {
                        auditEntry.KeyValues[propertyName] = property.CurrentValue;
                        continue;
                    }

                    switch (entry.State)
                    {
                        case EntityState.Added:
                            auditEntry.NewValues[propertyName] = property.CurrentValue;
                            break;

                        case EntityState.Deleted:
                            auditEntry.OldValues[propertyName] = property.OriginalValue;
                            break;

                        case EntityState.Modified:
                            if (property.IsModified)
                            {
                                auditEntry.OldValues[propertyName] = property.OriginalValue;
                                auditEntry.NewValues[propertyName] = property.CurrentValue;
                            }
                            break;
                    }
                }
            }

            foreach (var auditEntry in auditEntries.Where(_ => !_.HasTemporaryProperties))
            {
                AuditLogs.Add(auditEntry.ToAudit());
            }

            return auditEntries.Where(_ => _.HasTemporaryProperties).ToList();
        }

        private Task OnAfterSaveChanges(List<AuditEntry> auditEntries)
        {
            if (auditEntries == null || auditEntries.Count == 0)
                return Task.CompletedTask;

            foreach (var auditEntry in auditEntries)
            {
                foreach (var prop in auditEntry.TemporaryProperties)
                {
                    if (prop.Metadata.IsPrimaryKey())
                    {
                        auditEntry.KeyValues[prop.Metadata.Name] = prop.CurrentValue;
                    }
                    else
                    {
                        auditEntry.NewValues[prop.Metadata.Name] = prop.CurrentValue;
                    }
                }
                AuditLogs.Add(auditEntry.ToAudit());
            }

            return base.SaveChangesAsync();
        }
    }
}