using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ReGranBill.Server.Data;
using ReGranBill.Server.Middleware;
using ReGranBill.Server.Services;

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

LoadDotEnv();

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

var connectionString = configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Missing required connection string: ConnectionStrings:DefaultConnection");
}

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// JWT Authentication
var jwtSettings = configuration.GetSection("JwtSettings");
var secretKeyValue = jwtSettings["SecretKey"];
if (string.IsNullOrWhiteSpace(secretKeyValue))
{
    throw new InvalidOperationException("Missing required JWT setting: JwtSettings:SecretKey");
}

var secretKey = Encoding.UTF8.GetBytes(secretKeyValue);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(secretKey)
    };
});

builder.Services.AddAuthorization();

// Services DI
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IDeliveryChallanService, DeliveryChallanService>();
builder.Services.AddScoped<IJournalVoucherService, JournalVoucherService>();
builder.Services.AddScoped<ICashVoucherService, CashVoucherService>();
builder.Services.AddScoped<IVoucherEditorService, VoucherEditorService>();
builder.Services.AddScoped<IPurchaseVoucherService, PurchaseVoucherService>();
builder.Services.AddScoped<ISaleReturnService, SaleReturnService>();
builder.Services.AddScoped<IPurchaseReturnService, PurchaseReturnService>();
builder.Services.AddScoped<IStatementService, StatementService>();
builder.Services.AddScoped<ICustomerLedgerService, CustomerLedgerService>();
builder.Services.AddScoped<IMasterReportService, MasterReportService>();
builder.Services.AddScoped<IAccountClosingReportService, AccountClosingReportService>();
builder.Services.AddScoped<ICompanySettingsService, CompanySettingsService>();
builder.Services.AddScoped<ISalePurchaseReportService, SalePurchaseReportService>();
builder.Services.AddScoped<IProductStockReportService, ProductStockReportService>();
builder.Services.AddScoped<IPdfService, PdfService>();
builder.Services.AddScoped<IVoucherNumberService, VoucherNumberService>();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// CORS for Angular dev server
var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ??
[
    "https://localhost:50559",
    "http://localhost:50559",
    "https://127.0.0.1:50559",
    "http://127.0.0.1:50559"
];

builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCors", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();
var webRootPath = app.Environment.WebRootPath;
var hasWebRoot = !string.IsNullOrWhiteSpace(webRootPath) && Directory.Exists(webRootPath);

app.UseMiddleware<ExceptionHandlingMiddleware>();

// Seed database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await SeedData.InitializeAsync(db);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors("DevCors");
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

if (!app.Environment.IsDevelopment() && hasWebRoot)
{
    app.UseDefaultFiles();
    app.MapStaticAssets();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

if (!app.Environment.IsDevelopment() && hasWebRoot)
{
    app.MapFallbackToFile("/index.html");
}

app.Run();

static void LoadDotEnv()
{
    var probePaths = new[]
    {
        Path.Combine(Directory.GetCurrentDirectory(), ".env"),
        Path.Combine(Directory.GetCurrentDirectory(), "..", ".env")
    };

    foreach (var envPath in probePaths)
    {
        if (!File.Exists(envPath))
        {
            continue;
        }

        foreach (var rawLine in File.ReadAllLines(envPath))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var equalsIndex = line.IndexOf('=');
            if (equalsIndex <= 0)
            {
                continue;
            }

            var key = line[..equalsIndex].Trim();
            var value = line[(equalsIndex + 1)..].Trim().Trim('"');

            if (key.Length > 0 && string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }

        return;
    }
}
