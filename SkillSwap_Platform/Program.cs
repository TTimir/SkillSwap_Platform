using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Middlewares;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Services;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

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

// Register user service
builder.Services.AddScoped<IUserServices, UserServices>();

builder.Services.AddDistributedMemoryCache(); // Stores session in memory
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Auto-logout after 30 min
    options.Cookie.HttpOnly = true; // Prevents JavaScript access
    options.Cookie.IsEssential = true; // Required for authentication
});

// ✅ Add Authentication and ensure scheme consistency
builder.Services.AddAuthentication("SkillSwapAuth")
    .AddCookie("SkillSwapAuth", options =>
    {
        options.LoginPath = "/Home/Login";   // Redirect to login if unauthorized
        options.AccessDeniedPath = "/Home/AccessDenied"; // Redirect if forbidden
        options.Cookie.Name = "SkillSwapAuth"; // ✅ Custom cookie name
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
    });

builder.Services.AddAuthorization();
builder.Services.AddSession(); // ✅ Ensure session is enabled

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
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

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
