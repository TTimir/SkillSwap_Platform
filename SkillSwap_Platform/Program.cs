using AspNet.Security.OAuth.Apple;
using AspNet.Security.OAuth.GitHub;
using Hangfire;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Controllers;
using SkillSwap_Platform.Middlewares;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Services;
using SkillSwap_Platform.Services.AdminControls;
using SkillSwap_Platform.Services.AdminControls.Certificate;
using SkillSwap_Platform.Services.AdminControls.Escrow;
using SkillSwap_Platform.Services.AdminControls.UserManagement;
using SkillSwap_Platform.Services.Contracts;
using SkillSwap_Platform.Services.DigitalToken;
using SkillSwap_Platform.Services.Email;
using SkillSwap_Platform.Services.Meeting;
using SkillSwap_Platform.Services.Newsletter;
using SkillSwap_Platform.Services.NotificationTrack;
using SkillSwap_Platform.Services.PasswordReset;
using SkillSwap_Platform.Services.PDF;
using SkillSwap_Platform.Services.Repository;
using SkillSwap_Platform.Services.ReviewReply;
using SkillSwap_Platform.Services.TokenMining;
using SkillSwap_Platform.Services.Wishlist;
using System.Diagnostics;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();

// ✅ Load Encryption Key and store securely
string encryptionKey = builder.Configuration.GetValue<string>("EncryptionConfig:EncryptionKey");
if (string.IsNullOrEmpty(encryptionKey) || encryptionKey.Length != 32)
{
    throw new Exception("❌ Encryption key is invalid or missing. Ensure it is 32 characters long.");
}

// ✅ Register configuration
builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Configure DB Context
var config = builder.Configuration;
builder.Services.AddDbContext<SkillSwapDbContext>(item =>
        item.UseSqlServer(config.GetConnectionString("dbcs")));

builder.Services.AddHttpClient();

// Register user service
builder.Services.AddScoped<IContractPreparationService, ContractPreparationService>();
builder.Services.AddScoped<IContractHandlerService, ContractHandlerService>();
builder.Services.AddScoped<IViewRenderService, ViewRenderService>();
builder.Services.AddScoped<IPdfGenerator, PuppeteerPdfGenerator>();
builder.Services.AddScoped<IMessageRepository, MessageRepository>();
builder.Services.AddScoped<IMessagingService, MessagingService>();
builder.Services.AddScoped<IUserServices, UserServices>();
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<ISensitiveWordService, SensitiveWordService>();
builder.Services.AddScoped<UserProfilesFilter>();
builder.Services.AddScoped<ExchangeMeetingService>();
builder.Services.AddHostedService<ContractExpirationService>();
builder.Services.AddHostedService<MeetingTimeoutHostedService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IPasswordResetService, PasswordResetService>();
builder.Services.AddScoped<IPasswordHasher<TblUser>, PasswordHasher<TblUser>>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<INewsletterService, NewsletterService>();
builder.Services.AddScoped<IDigitalTokenService, DigitalTokenService>();
builder.Services
    .AddTransient<IEmailService, SmtpEmailService>();
builder.Services
       .AddScoped<IWishlistService, WishlistService>();
builder.Services.AddHostedService<MiningHostedService>();
builder.Services.AddHostedService<SeedDataService>();


// Admin Services
builder.Services.AddScoped<IAdminDashboardService, AdminDashboardService>();
builder.Services.AddScoped<ICertificateReviewService, CertificateReviewService>();
builder.Services.AddScoped<IUserManagmentService, UserManagmentService>();
builder.Services.AddHostedService<ExpiredHoldCleanupService>();
builder.Services.AddScoped<IEscrowService, EscrowService>();

builder.Services.AddControllersWithViews(options =>
{
    // Add the custom filter globally
    options.Filters.AddService<UserProfilesFilter>();
});

builder.Services.AddDistributedMemoryCache(); // Stores session in memory
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Auto-logout after 30 min
    options.Cookie.HttpOnly = true; // Prevents JavaScript access
    options.Cookie.IsEssential = true; // Required for authentication
});

// ✅ Add Authentication and ensure scheme consistency
//builder.Services.AddAuthentication("SkillSwapAuth")
//    .AddCookie("SkillSwapAuth", options =>
//    {
//        options.LoginPath = "/Home/Login";   // Redirect to login if unauthorized
//        options.AccessDeniedPath = "/Home/AccessDenied"; // Redirect if forbidden
//        options.Cookie.Name = "SkillSwapAuth"; // ✅ Custom cookie name
//        options.ExpireTimeSpan = TimeSpan.FromDays(7);
//    });
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = "SkillSwapAuth";
        options.DefaultSignInScheme = "SkillSwapAuth";
        options.DefaultChallengeScheme = "SkillSwapAuth";
    })
    .AddCookie("SkillSwapAuth", options =>
    {
        options.LoginPath = "/Home/Login";
        options.AccessDeniedPath = "/Home/AccessDenied";
        options.Cookie.Name = "SkillSwapAuth";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.Events.OnRedirectToLogin = context =>
        {
            // Log the redirect URL for debugging.
            Console.WriteLine($"Redirecting to login from {context.Request.Path}");
            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    })
    .AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
    {
        options.SignInScheme = "ExternalScheme";
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"];
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
        options.Events.OnRemoteFailure = ctx =>
        {
            ctx.Response.Redirect("/Home/Login?error=" + Uri.EscapeDataString(ctx.Failure.Message));
            ctx.HandleResponse();
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddSession(); // ✅ Ensure session is enabled

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// ✅ Ensure authentication and session middleware are registered in correct order
app.UseSession();
app.UseAuthentication();  // 🔴 Must be before Authorization
app.UseAuthorization();
app.UseMiddleware<UpdateLastActiveMiddleware>();

app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
