using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using EduLog.Data;
using EduLog.Models;
using EduLog.Services;
using Microsoft.Extensions.Options;
using Polly;
using Resend;

// Find the project root directory by walking up from bin folder
string contentRootPath = AppContext.BaseDirectory;

// Keep going up until we find a directory with a Views folder
for (int i = 0; i < 10; i++) // Safety limit to prevent infinite loops
{
    if (Directory.Exists(Path.Combine(contentRootPath, "Views")))
    {
        break;
    }

    var parentDir = Directory.GetParent(contentRootPath);
    if (parentDir == null)
        break;

    contentRootPath = parentDir.FullName;
}

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = contentRootPath
});

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();

// ������ �������� ���� �����
builder.Services.AddDbContext<EduLogContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ������������ Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
})
.AddEntityFrameworkStores<EduLogContext>()
.AddDefaultTokenProviders();

// Multi-tenant services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantService, TenantService>();
builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, CustomClaimsPrincipalFactory>();
builder.Services.Configure<SchedulerApiOptions>(builder.Configuration.GetSection(SchedulerApiOptions.SectionName));
builder.Services.AddHttpClient<ISchedulerService, SchedulerService>((serviceProvider, client) =>
{
    var schedulerOptions = serviceProvider.GetRequiredService<IOptions<SchedulerApiOptions>>().Value;
    client.BaseAddress = new Uri(schedulerOptions.BaseUrl ?? "http://localhost:5001");
    client.Timeout = TimeSpan.FromSeconds(Math.Max(1, schedulerOptions.TimeoutSeconds));
})
.AddPolicyHandler((serviceProvider, _) =>
{
    var schedulerOptions = serviceProvider.GetRequiredService<IOptions<SchedulerApiOptions>>().Value;
    var retryCount = Math.Max(1, schedulerOptions.RetryCount);

    return Policy<HttpResponseMessage>
        .Handle<HttpRequestException>()
        .OrResult(response => !response.IsSuccessStatusCode)
        .WaitAndRetryAsync(
            retryCount,
            attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));
});
// Mailtrap configuration
builder.Services.AddScoped<IEmailService, MailtrapEmailService>();
builder.Services.AddHttpClient();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

var app = builder.Build();

app.Logger.LogInformation("Content root path: {ContentRootPath}", app.Environment.ContentRootPath);
app.Logger.LogInformation("Views folder exists: {ViewsExists}", Directory.Exists(Path.Combine(app.Environment.ContentRootPath, "Views")));
app.Logger.LogInformation("Home folder exists: {HomeExists}", Directory.Exists(Path.Combine(app.Environment.ContentRootPath, "Views", "Home")));
app.Logger.LogInformation("Index.cshtml exists: {IndexExists}", File.Exists(Path.Combine(app.Environment.ContentRootPath, "Views", "Home", "Index.cshtml")));

// Seed roles
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<EduLogContext>();
    await dbContext.Database.MigrateAsync();

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    string[] roles = { "Admin", "Teacher" };
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
