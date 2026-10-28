using Microsoft.AspNetCore.Authentication.Cookies;
using RaceDay.Web.Filters;

var builder = WebApplication.CreateBuilder(args);

var apiBaseUrl = builder.Configuration["Api:BaseUrl"]
    ?? throw new InvalidOperationException("Api:BaseUrl is not configured.");
var allowInvalidCertificate = builder.Configuration.GetValue<bool>("Api:AllowInvalidCertificate");

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<ApiExceptionFilter>();                          // friendly handling of API problems
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());   // CSRF protection on every POST
});

// ---- Session: the API's login cookie is kept server-side, never sent to the browser ----
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".RaceDay.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromMinutes(100);
});

// ---- Authentication: the MVC cookie holds user id, name and role ----
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "RaceDayWeb";
        options.Cookie.HttpOnly = true;
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(100);
        options.SlidingExpiration = false;
    });
builder.Services.AddAuthorization();

// ---- Typed HttpClient for the Part 2 API (this app has no database access) ----
builder.Services
    .AddHttpClient<ApiClient>(client =>
    {
        client.BaseAddress = new Uri(apiBaseUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .ConfigurePrimaryHttpMessageHandler(() =>
    {
        var handler = new HttpClientHandler { UseCookies = false }; // we forward the API cookie ourselves
        if (allowInvalidCertificate)
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        return handler;
    });

var app = builder.Build();

app.UseExceptionHandler("/Home/Error");                  // never show raw exceptions
app.UseStatusCodePagesWithReExecute("/Home/Status/{0}");

app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
