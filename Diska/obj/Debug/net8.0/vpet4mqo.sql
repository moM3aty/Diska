IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [AspNetRoles] (
    [Id] nvarchar(450) NOT NULL,
    [Name] nvarchar(256) NULL,
    [NormalizedName] nvarchar(256) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [AspNetUsers] (
    [Id] nvarchar(450) NOT NULL,
    [FullName] nvarchar(max) NOT NULL,
    [ShopName] nvarchar(max) NOT NULL,
    [CommercialRegister] nvarchar(max) NOT NULL,
    [TaxCard] nvarchar(max) NOT NULL,
    [WalletBalance] decimal(18,2) NOT NULL,
    [IsVerifiedMerchant] bit NOT NULL,
    [UserName] nvarchar(256) NULL,
    [NormalizedUserName] nvarchar(256) NULL,
    [Email] nvarchar(256) NULL,
    [NormalizedEmail] nvarchar(256) NULL,
    [EmailConfirmed] bit NOT NULL,
    [PasswordHash] nvarchar(max) NULL,
    [SecurityStamp] nvarchar(max) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    [PhoneNumber] nvarchar(max) NULL,
    [PhoneNumberConfirmed] bit NOT NULL,
    [TwoFactorEnabled] bit NOT NULL,
    [LockoutEnd] datetimeoffset NULL,
    [LockoutEnabled] bit NOT NULL,
    [AccessFailedCount] int NOT NULL,
    CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [AuditLogs] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(max) NOT NULL,
    [Action] nvarchar(max) NOT NULL,
    [EntityName] nvarchar(max) NOT NULL,
    [EntityId] nvarchar(max) NOT NULL,
    [Details] nvarchar(max) NOT NULL,
    [IpAddress] nvarchar(max) NOT NULL,
    [Timestamp] datetime2 NOT NULL,
    CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [Banners] (
    [Id] int NOT NULL IDENTITY,
    [Title] nvarchar(max) NOT NULL,
    [TitleEn] nvarchar(max) NOT NULL,
    [Subtitle] nvarchar(max) NOT NULL,
    [SubtitleEn] nvarchar(max) NOT NULL,
    [ImageDesktop] nvarchar(max) NOT NULL,
    [ImageMobile] nvarchar(max) NOT NULL,
    [LinkType] nvarchar(max) NOT NULL,
    [LinkId] nvarchar(max) NOT NULL,
    [ButtonText] nvarchar(max) NOT NULL,
    [ButtonTextEn] nvarchar(max) NOT NULL,
    [Priority] int NOT NULL,
    [IsActive] bit NOT NULL,
    [StartDate] datetime2 NOT NULL,
    [EndDate] datetime2 NOT NULL,
    CONSTRAINT [PK_Banners] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [Categories] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    [NameEn] nvarchar(max) NOT NULL,
    [IconClass] nvarchar(max) NOT NULL,
    [ImageUrl] nvarchar(max) NOT NULL,
    [IsActive] bit NOT NULL,
    [DisplayOrder] int NOT NULL,
    [ParentId] int NULL,
    [Slug] nvarchar(max) NOT NULL,
    [MetaTitle] nvarchar(max) NOT NULL,
    [MetaDescription] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_Categories] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Categories_Categories_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [Categories] ([Id])
);
GO

CREATE TABLE [ContactMessages] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    [Phone] nvarchar(max) NOT NULL,
    [Email] nvarchar(max) NOT NULL,
    [Subject] nvarchar(max) NOT NULL,
    [Message] nvarchar(max) NOT NULL,
    [DateSent] datetime2 NOT NULL,
    CONSTRAINT [PK_ContactMessages] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [Surveys] (
    [Id] int NOT NULL IDENTITY,
    [Title] nvarchar(max) NOT NULL,
    [TitleEn] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [IsActive] bit NOT NULL,
    [StartDate] datetime2 NOT NULL,
    [EndDate] datetime2 NOT NULL,
    [TargetAudience] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_Surveys] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [UserNotifications] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(max) NOT NULL,
    [Title] nvarchar(max) NOT NULL,
    [Message] nvarchar(max) NOT NULL,
    [Type] nvarchar(max) NOT NULL,
    [Link] nvarchar(max) NOT NULL,
    [IsRead] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_UserNotifications] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [AspNetRoleClaims] (
    [Id] int NOT NULL IDENTITY,
    [RoleId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [AspNetUserClaims] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [AspNetUserLogins] (
    [LoginProvider] nvarchar(450) NOT NULL,
    [ProviderKey] nvarchar(450) NOT NULL,
    [ProviderDisplayName] nvarchar(max) NULL,
    [UserId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
    CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [AspNetUserRoles] (
    [UserId] nvarchar(450) NOT NULL,
    [RoleId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [AspNetUserTokens] (
    [UserId] nvarchar(450) NOT NULL,
    [LoginProvider] nvarchar(450) NOT NULL,
    [Name] nvarchar(450) NOT NULL,
    [Value] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [DealRequests] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [ProductName] nvarchar(max) NOT NULL,
    [TargetQuantity] int NOT NULL,
    [DealPrice] decimal(18,2) NOT NULL,
    [Location] nvarchar(max) NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [AdminNotes] nvarchar(max) NOT NULL,
    [RequestDate] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_DealRequests] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_DealRequests_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id])
);
GO

CREATE TABLE [MerchantPermissions] (
    [Id] int NOT NULL IDENTITY,
    [MerchantId] nvarchar(450) NOT NULL,
    [Module] nvarchar(max) NOT NULL,
    [CanView] bit NOT NULL,
    [CanCreate] bit NOT NULL,
    [CanEdit] bit NOT NULL,
    [CanDelete] bit NOT NULL,
    [CanApprove] bit NOT NULL,
    CONSTRAINT [PK_MerchantPermissions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MerchantPermissions_AspNetUsers_MerchantId] FOREIGN KEY ([MerchantId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [Orders] (
    [Id] int NOT NULL IDENTITY,
    [OrderDate] datetime2 NOT NULL,
    [UserId] nvarchar(450) NOT NULL,
    [CustomerName] nvarchar(max) NOT NULL,
    [Phone] nvarchar(max) NOT NULL,
    [Address] nvarchar(max) NOT NULL,
    [Governorate] nvarchar(max) NOT NULL,
    [City] nvarchar(max) NOT NULL,
    [TotalAmount] decimal(18,2) NOT NULL,
    [ShippingCost] decimal(18,2) NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [PaymentMethod] nvarchar(max) NOT NULL,
    [DeliverySlot] nvarchar(max) NOT NULL,
    [Notes] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_Orders] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Orders_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id])
);
GO

CREATE TABLE [PendingMerchantActions] (
    [Id] int NOT NULL IDENTITY,
    [MerchantId] nvarchar(450) NOT NULL,
    [ActionType] nvarchar(max) NOT NULL,
    [EntityName] nvarchar(max) NOT NULL,
    [EntityId] nvarchar(max) NOT NULL,
    [OldValueJson] nvarchar(max) NOT NULL,
    [NewValueJson] nvarchar(max) NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [AdminComment] nvarchar(max) NOT NULL,
    [RequestDate] datetime2 NOT NULL,
    [ActionDate] datetime2 NULL,
    [ActionByAdminId] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_PendingMerchantActions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PendingMerchantActions_AspNetUsers_MerchantId] FOREIGN KEY ([MerchantId]) REFERENCES [AspNetUsers] ([Id])
);
GO

CREATE TABLE [UserAddresses] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [Title] nvarchar(max) NOT NULL,
    [Governorate] nvarchar(max) NOT NULL,
    [City] nvarchar(max) NOT NULL,
    [Street] nvarchar(max) NOT NULL,
    [PhoneNumber] nvarchar(max) NOT NULL,
    [Latitude] float NULL,
    [Longitude] float NULL,
    [IsDefault] bit NOT NULL,
    CONSTRAINT [PK_UserAddresses] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_UserAddresses_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [WalletTransactions] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [Type] nvarchar(max) NOT NULL,
    [TransactionDate] datetime2 NOT NULL,
    CONSTRAINT [PK_WalletTransactions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WalletTransactions_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id])
);
GO

CREATE TABLE [Products] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    [NameEn] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [DescriptionEn] nvarchar(max) NOT NULL,
    [Brand] nvarchar(max) NOT NULL,
    [SKU] nvarchar(max) NOT NULL,
    [Barcode] nvarchar(max) NOT NULL,
    [Price] decimal(18,2) NOT NULL,
    [OldPrice] decimal(18,2) NULL,
    [CostPrice] decimal(18,2) NOT NULL,
    [StockQuantity] int NOT NULL,
    [LowStockThreshold] int NOT NULL,
    [UnitsPerCarton] int NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [ImageUrl] nvarchar(max) NOT NULL,
    [Color] nvarchar(max) NOT NULL,
    [Weight] decimal(18,2) NOT NULL,
    [Slug] nvarchar(max) NOT NULL,
    [MetaTitle] nvarchar(max) NOT NULL,
    [MetaDescription] nvarchar(max) NOT NULL,
    [ProductionDate] datetime2 NULL,
    [ExpiryDate] datetime2 NULL,
    [MerchantId] nvarchar(450) NOT NULL,
    [CategoryId] int NOT NULL,
    CONSTRAINT [PK_Products] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Products_AspNetUsers_MerchantId] FOREIGN KEY ([MerchantId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Products_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [SurveyQuestions] (
    [Id] int NOT NULL IDENTITY,
    [SurveyId] int NOT NULL,
    [QuestionText] nvarchar(max) NOT NULL,
    [QuestionTextEn] nvarchar(max) NOT NULL,
    [Type] nvarchar(max) NOT NULL,
    [Options] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_SurveyQuestions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SurveyQuestions_Surveys_SurveyId] FOREIGN KEY ([SurveyId]) REFERENCES [Surveys] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [SurveyResponses] (
    [Id] int NOT NULL IDENTITY,
    [SurveyId] int NOT NULL,
    [UserId] nvarchar(max) NOT NULL,
    [AnswerJson] nvarchar(max) NOT NULL,
    [SubmittedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_SurveyResponses] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SurveyResponses_Surveys_SurveyId] FOREIGN KEY ([SurveyId]) REFERENCES [Surveys] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [MerchantOffers] (
    [Id] int NOT NULL IDENTITY,
    [DealRequestId] int NOT NULL,
    [MerchantId] nvarchar(450) NOT NULL,
    [OfferPrice] decimal(18,2) NOT NULL,
    [Notes] nvarchar(max) NOT NULL,
    [IsAccepted] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_MerchantOffers] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MerchantOffers_AspNetUsers_MerchantId] FOREIGN KEY ([MerchantId]) REFERENCES [AspNetUsers] ([Id]),
    CONSTRAINT [FK_MerchantOffers_DealRequests_DealRequestId] FOREIGN KEY ([DealRequestId]) REFERENCES [DealRequests] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [GroupDeals] (
    [Id] int NOT NULL IDENTITY,
    [Title] nvarchar(max) NOT NULL,
    [ProductId] int NULL,
    [CategoryId] int NULL,
    [IsPercentage] bit NOT NULL,
    [DealPrice] decimal(18,2) NOT NULL,
    [DiscountValue] decimal(18,2) NOT NULL,
    [TargetQuantity] int NOT NULL,
    [ReservedQuantity] int NOT NULL,
    [UsageLimit] int NULL,
    [StartDate] datetime2 NOT NULL,
    [EndDate] datetime2 NOT NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_GroupDeals] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_GroupDeals_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]),
    CONSTRAINT [FK_GroupDeals_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id])
);
GO

CREATE TABLE [OrderItems] (
    [Id] int NOT NULL IDENTITY,
    [OrderId] int NOT NULL,
    [ProductId] int NOT NULL,
    [Quantity] int NOT NULL,
    [UnitPrice] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_OrderItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_OrderItems_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_OrderItems_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [PriceTiers] (
    [Id] int NOT NULL IDENTITY,
    [ProductId] int NOT NULL,
    [MinQuantity] int NOT NULL,
    [MaxQuantity] int NOT NULL,
    [UnitPrice] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_PriceTiers] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PriceTiers_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [ProductColors] (
    [Id] int NOT NULL IDENTITY,
    [ColorName] nvarchar(max) NOT NULL,
    [ColorHex] nvarchar(max) NOT NULL,
    [ProductId] int NOT NULL,
    CONSTRAINT [PK_ProductColors] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProductColors_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [ProductImages] (
    [Id] int NOT NULL IDENTITY,
    [ImageUrl] nvarchar(max) NOT NULL,
    [ProductId] int NOT NULL,
    CONSTRAINT [PK_ProductImages] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProductImages_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [ProductReviews] (
    [Id] int NOT NULL IDENTITY,
    [ProductId] int NOT NULL,
    [UserId] nvarchar(450) NOT NULL,
    [Rating] int NOT NULL,
    [Comment] nvarchar(max) NOT NULL,
    [IsVisible] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_ProductReviews] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProductReviews_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]),
    CONSTRAINT [FK_ProductReviews_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [RestockSubscriptions] (
    [Id] int NOT NULL IDENTITY,
    [ProductId] int NOT NULL,
    [UserId] nvarchar(450) NULL,
    [Email] nvarchar(max) NOT NULL,
    [RequestDate] datetime2 NOT NULL,
    [IsNotified] bit NOT NULL,
    CONSTRAINT [PK_RestockSubscriptions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RestockSubscriptions_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]),
    CONSTRAINT [FK_RestockSubscriptions_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [WishlistItems] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(max) NOT NULL,
    [ProductId] int NOT NULL,
    CONSTRAINT [PK_WishlistItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WishlistItems_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
GO

CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;
GO

CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
GO

CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
GO

CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
GO

CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
GO

CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;
GO

CREATE INDEX [IX_Categories_ParentId] ON [Categories] ([ParentId]);
GO

CREATE INDEX [IX_DealRequests_UserId] ON [DealRequests] ([UserId]);
GO

CREATE INDEX [IX_GroupDeals_CategoryId] ON [GroupDeals] ([CategoryId]);
GO

CREATE INDEX [IX_GroupDeals_ProductId] ON [GroupDeals] ([ProductId]);
GO

CREATE INDEX [IX_MerchantOffers_DealRequestId] ON [MerchantOffers] ([DealRequestId]);
GO

CREATE INDEX [IX_MerchantOffers_MerchantId] ON [MerchantOffers] ([MerchantId]);
GO

CREATE INDEX [IX_MerchantPermissions_MerchantId] ON [MerchantPermissions] ([MerchantId]);
GO

CREATE INDEX [IX_OrderItems_OrderId] ON [OrderItems] ([OrderId]);
GO

CREATE INDEX [IX_OrderItems_ProductId] ON [OrderItems] ([ProductId]);
GO

CREATE INDEX [IX_Orders_UserId] ON [Orders] ([UserId]);
GO

CREATE INDEX [IX_PendingMerchantActions_MerchantId] ON [PendingMerchantActions] ([MerchantId]);
GO

CREATE INDEX [IX_PriceTiers_ProductId] ON [PriceTiers] ([ProductId]);
GO

CREATE INDEX [IX_ProductColors_ProductId] ON [ProductColors] ([ProductId]);
GO

CREATE INDEX [IX_ProductImages_ProductId] ON [ProductImages] ([ProductId]);
GO

CREATE INDEX [IX_ProductReviews_ProductId] ON [ProductReviews] ([ProductId]);
GO

CREATE INDEX [IX_ProductReviews_UserId] ON [ProductReviews] ([UserId]);
GO

CREATE INDEX [IX_Products_CategoryId] ON [Products] ([CategoryId]);
GO

CREATE INDEX [IX_Products_MerchantId] ON [Products] ([MerchantId]);
GO

CREATE INDEX [IX_RestockSubscriptions_ProductId] ON [RestockSubscriptions] ([ProductId]);
GO

CREATE INDEX [IX_RestockSubscriptions_UserId] ON [RestockSubscriptions] ([UserId]);
GO

CREATE INDEX [IX_SurveyQuestions_SurveyId] ON [SurveyQuestions] ([SurveyId]);
GO

CREATE INDEX [IX_SurveyResponses_SurveyId] ON [SurveyResponses] ([SurveyId]);
GO

CREATE INDEX [IX_UserAddresses_UserId] ON [UserAddresses] ([UserId]);
GO

CREATE INDEX [IX_WalletTransactions_UserId] ON [WalletTransactions] ([UserId]);
GO

CREATE INDEX [IX_WishlistItems_ProductId] ON [WishlistItems] ([ProductId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260111085928_iniialCreate', N'8.0.22');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [OrderItems] ADD [SelectedColorHex] nvarchar(max) NOT NULL DEFAULT N'';
GO

ALTER TABLE [OrderItems] ADD [SelectedColorName] nvarchar(max) NOT NULL DEFAULT N'';
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260113013430_New', N'8.0.22');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260113033330_New2', N'8.0.22');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [GroupDeals] ADD [TitleEn] nvarchar(max) NOT NULL DEFAULT N'';
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260115213427_EditDealTranslate', N'8.0.22');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260117185535_AddAuditLog', N'8.0.22');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

EXEC sp_rename N'[PendingMerchantActions].[ActionDate]', N'ProcessedDate', N'COLUMN';
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260117203450_AddProcessedDateToActions', N'8.0.22');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [RequestMessages] (
    [Id] int NOT NULL IDENTITY,
    [DealRequestId] int NOT NULL,
    [SenderId] nvarchar(450) NOT NULL,
    [Message] nvarchar(max) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [IsAdmin] bit NOT NULL,
    CONSTRAINT [PK_RequestMessages] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RequestMessages_AspNetUsers_SenderId] FOREIGN KEY ([SenderId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_RequestMessages_DealRequests_DealRequestId] FOREIGN KEY ([DealRequestId]) REFERENCES [DealRequests] ([Id]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_RequestMessages_DealRequestId] ON [RequestMessages] ([DealRequestId]);
GO

CREATE INDEX [IX_RequestMessages_SenderId] ON [RequestMessages] ([SenderId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260118125243_AddMsgRequest', N'8.0.22');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [ShippingRates] (
    [Id] int NOT NULL IDENTITY,
    [Governorate] nvarchar(max) NOT NULL,
    [City] nvarchar(max) NOT NULL,
    [Cost] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_ShippingRates] PRIMARY KEY ([Id])
);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260120222525_shipping', N'8.0.22');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [Banners] ADD [AdminComment] nvarchar(max) NOT NULL DEFAULT N'';
GO

ALTER TABLE [Banners] ADD [ApprovalStatus] nvarchar(max) NOT NULL DEFAULT N'';
GO

ALTER TABLE [Banners] ADD [MerchantId] nvarchar(450) NOT NULL DEFAULT N'';
GO

CREATE INDEX [IX_Banners_MerchantId] ON [Banners] ([MerchantId]);
GO

ALTER TABLE [Banners] ADD CONSTRAINT [FK_Banners_AspNetUsers_MerchantId] FOREIGN KEY ([MerchantId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260121210243_AddBanner', N'8.0.22');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DECLARE @var0 sysname;
SELECT @var0 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Banners]') AND [c].[name] = N'AdminComment');
IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [Banners] DROP CONSTRAINT [' + @var0 + '];');
ALTER TABLE [Banners] ALTER COLUMN [AdminComment] nvarchar(max) NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260121211506_BannerEdit', N'8.0.22');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [Banners] DROP CONSTRAINT [FK_Banners_AspNetUsers_MerchantId];
GO

DECLARE @var1 sysname;
SELECT @var1 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Banners]') AND [c].[name] = N'MerchantId');
IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Banners] DROP CONSTRAINT [' + @var1 + '];');
ALTER TABLE [Banners] ALTER COLUMN [MerchantId] nvarchar(450) NULL;
GO

ALTER TABLE [Banners] ADD CONSTRAINT [FK_Banners_AspNetUsers_MerchantId] FOREIGN KEY ([MerchantId]) REFERENCES [AspNetUsers] ([Id]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260121211701_BannerEdit2', N'8.0.22');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DECLARE @var2 sysname;
SELECT @var2 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WishlistItems]') AND [c].[name] = N'UserId');
IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [WishlistItems] DROP CONSTRAINT [' + @var2 + '];');
ALTER TABLE [WishlistItems] ALTER COLUMN [UserId] nvarchar(450) NOT NULL;
GO

ALTER TABLE [AspNetUsers] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
GO

ALTER TABLE [AspNetUsers] ADD [IsActive] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [AspNetUsers] ADD [MerchantId] nvarchar(max) NOT NULL DEFAULT N'';
GO

ALTER TABLE [AspNetUsers] ADD [UserType] nvarchar(max) NOT NULL DEFAULT N'';
GO

CREATE INDEX [IX_WishlistItems_UserId] ON [WishlistItems] ([UserId]);
GO

ALTER TABLE [WishlistItems] ADD CONSTRAINT [FK_WishlistItems_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260313144028_AddUserTypeAndCreatedAt', N'8.0.22');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DECLARE @var3 sysname;
SELECT @var3 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AspNetUsers]') AND [c].[name] = N'UserType');
IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [AspNetUsers] DROP CONSTRAINT [' + @var3 + '];');
ALTER TABLE [AspNetUsers] ALTER COLUMN [UserType] nvarchar(max) NULL;
GO

DECLARE @var4 sysname;
SELECT @var4 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AspNetUsers]') AND [c].[name] = N'TaxCard');
IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [AspNetUsers] DROP CONSTRAINT [' + @var4 + '];');
ALTER TABLE [AspNetUsers] ALTER COLUMN [TaxCard] nvarchar(max) NULL;
GO

DECLARE @var5 sysname;
SELECT @var5 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AspNetUsers]') AND [c].[name] = N'ShopName');
IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [AspNetUsers] DROP CONSTRAINT [' + @var5 + '];');
ALTER TABLE [AspNetUsers] ALTER COLUMN [ShopName] nvarchar(max) NULL;
GO

DECLARE @var6 sysname;
SELECT @var6 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AspNetUsers]') AND [c].[name] = N'MerchantId');
IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [AspNetUsers] DROP CONSTRAINT [' + @var6 + '];');
ALTER TABLE [AspNetUsers] ALTER COLUMN [MerchantId] nvarchar(max) NULL;
GO

DECLARE @var7 sysname;
SELECT @var7 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AspNetUsers]') AND [c].[name] = N'FullName');
IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [AspNetUsers] DROP CONSTRAINT [' + @var7 + '];');
ALTER TABLE [AspNetUsers] ALTER COLUMN [FullName] nvarchar(max) NULL;
GO

DECLARE @var8 sysname;
SELECT @var8 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AspNetUsers]') AND [c].[name] = N'CommercialRegister');
IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [AspNetUsers] DROP CONSTRAINT [' + @var8 + '];');
ALTER TABLE [AspNetUsers] ALTER COLUMN [CommercialRegister] nvarchar(max) NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260313144709_MakeUserFieldsNullable', N'8.0.22');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DECLARE @var9 sysname;
SELECT @var9 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Banners]') AND [c].[name] = N'TitleEn');
IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [Banners] DROP CONSTRAINT [' + @var9 + '];');
ALTER TABLE [Banners] ALTER COLUMN [TitleEn] nvarchar(max) NULL;
GO

DECLARE @var10 sysname;
SELECT @var10 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Banners]') AND [c].[name] = N'SubtitleEn');
IF @var10 IS NOT NULL EXEC(N'ALTER TABLE [Banners] DROP CONSTRAINT [' + @var10 + '];');
ALTER TABLE [Banners] ALTER COLUMN [SubtitleEn] nvarchar(max) NULL;
GO

DECLARE @var11 sysname;
SELECT @var11 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Banners]') AND [c].[name] = N'Subtitle');
IF @var11 IS NOT NULL EXEC(N'ALTER TABLE [Banners] DROP CONSTRAINT [' + @var11 + '];');
ALTER TABLE [Banners] ALTER COLUMN [Subtitle] nvarchar(max) NULL;
GO

DECLARE @var12 sysname;
SELECT @var12 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Banners]') AND [c].[name] = N'LinkType');
IF @var12 IS NOT NULL EXEC(N'ALTER TABLE [Banners] DROP CONSTRAINT [' + @var12 + '];');
ALTER TABLE [Banners] ALTER COLUMN [LinkType] nvarchar(max) NULL;
GO

DECLARE @var13 sysname;
SELECT @var13 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Banners]') AND [c].[name] = N'LinkId');
IF @var13 IS NOT NULL EXEC(N'ALTER TABLE [Banners] DROP CONSTRAINT [' + @var13 + '];');
ALTER TABLE [Banners] ALTER COLUMN [LinkId] nvarchar(max) NULL;
GO

DECLARE @var14 sysname;
SELECT @var14 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Banners]') AND [c].[name] = N'ImageMobile');
IF @var14 IS NOT NULL EXEC(N'ALTER TABLE [Banners] DROP CONSTRAINT [' + @var14 + '];');
ALTER TABLE [Banners] ALTER COLUMN [ImageMobile] nvarchar(max) NULL;
GO

DECLARE @var15 sysname;
SELECT @var15 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Banners]') AND [c].[name] = N'ImageDesktop');
IF @var15 IS NOT NULL EXEC(N'ALTER TABLE [Banners] DROP CONSTRAINT [' + @var15 + '];');
ALTER TABLE [Banners] ALTER COLUMN [ImageDesktop] nvarchar(max) NULL;
GO

DECLARE @var16 sysname;
SELECT @var16 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Banners]') AND [c].[name] = N'ButtonTextEn');
IF @var16 IS NOT NULL EXEC(N'ALTER TABLE [Banners] DROP CONSTRAINT [' + @var16 + '];');
ALTER TABLE [Banners] ALTER COLUMN [ButtonTextEn] nvarchar(max) NULL;
GO

DECLARE @var17 sysname;
SELECT @var17 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Banners]') AND [c].[name] = N'ButtonText');
IF @var17 IS NOT NULL EXEC(N'ALTER TABLE [Banners] DROP CONSTRAINT [' + @var17 + '];');
ALTER TABLE [Banners] ALTER COLUMN [ButtonText] nvarchar(max) NULL;
GO

DECLARE @var18 sysname;
SELECT @var18 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Banners]') AND [c].[name] = N'ApprovalStatus');
IF @var18 IS NOT NULL EXEC(N'ALTER TABLE [Banners] DROP CONSTRAINT [' + @var18 + '];');
ALTER TABLE [Banners] ALTER COLUMN [ApprovalStatus] nvarchar(max) NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260313145402_UpdateBannerLinks', N'8.0.22');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DECLARE @var19 sysname;
SELECT @var19 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[GroupDeals]') AND [c].[name] = N'TitleEn');
IF @var19 IS NOT NULL EXEC(N'ALTER TABLE [GroupDeals] DROP CONSTRAINT [' + @var19 + '];');
ALTER TABLE [GroupDeals] ALTER COLUMN [TitleEn] nvarchar(max) NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260313150115_FixGroupDealsNullable', N'8.0.22');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260415093610_edit', N'8.0.22');
GO

COMMIT;
GO

