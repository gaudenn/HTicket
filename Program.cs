using HTicket.Models;
using HTicket.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Mscc.GenerativeAI;

var builder = WebApplication.CreateBuilder(args);

// --- 1. ĐĂNG KÝ SERVICES (Cấu hình hệ thống) ---

// Cấu hình MVC
builder.Services.AddControllersWithViews();

// Cấu hình SQL Server (Giữ nguyên của bạn)
builder.Services.AddDbContext<TestDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Cấu hình Cookie Authentication (Giữ nguyên của bạn)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options => {
        options.LoginPath = "/Account/Login";
    });

// Đăng ký Service dự báo cũ (Giữ nguyên - Singleton)
builder.Services.AddSingleton<IDemandPredictionService, DemandPredictionService>();

builder.Services.AddSingleton(sp => {

    var apiKey = builder.Configuration["GeminiSettings:ApiKey"];
    return new GoogleAI(apiKey, "v1");
});

builder.Services.AddScoped<IChatbotService, ChatbotService>();

builder.Services.AddHttpClient<IChatbotService, ChatbotService>();

var app = builder.Build();


if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication(); // Xác thực danh tính
app.UseAuthorization();  // Phân quyền

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();