using System.Reflection;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ---- Database (SQL Server via EF Core, Code-First) ----
builder.Services.AddDbContext<ApplicationDBContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ---- Session-based authentication (cookie holds the user id + role) ----
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "RaceDayAuth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromHours(2);
        options.SlidingExpiration = true;
        // This is an API: answer with status codes instead of redirecting to a login page.
        options.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
        options.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
    });
builder.Services.AddAuthorization();

// ---- Image storage: Azure Blob if configured, otherwise local wwwroot/uploads ----
var azureConnection = builder.Configuration["AzureBlob:ConnectionString"];
if (!string.IsNullOrWhiteSpace(azureConnection))
{
    var container = builder.Configuration["AzureBlob:ContainerName"] ?? "raceday-images";
    builder.Services.AddSingleton<IImageStorage>(new AzureBlobImageStorage(azureConnection, container));
}
else
{
    builder.Services.AddSingleton<IImageStorage, LocalImageStorage>();
}

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "RaceDay API",
        Version = "v1",
        Description = "RESTful API for the RaceDay event system. Log in with POST /api/auth/login first - " +
                      "Swagger then sends the session cookie automatically. Roles: Organiser, Participant."
    });
    var xmlFile = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xmlFile)) options.IncludeXmlComments(xmlFile);
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseAuthentication();   // must run before authorization
app.UseAuthorization();
app.MapControllers();

app.Run();

// Makes Program visible to the test project (WebApplicationFactory<Program>).
public partial class Program { }
