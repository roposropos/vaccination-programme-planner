using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using VaxPlan.Api.Data;
using VaxPlan.Api.Domain;
using VaxPlan.Api.Fhir;
using VaxPlan.Api.Infrastructure;
using VaxPlan.Api.Services;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(config.GetConnectionString("Default")));

builder.Services.Configure<JwtOptions>(config.GetSection("Jwt"));
var jwt = config.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (jwt.Key.Length < 32)
{
    throw new InvalidOperationException("Ustaw Jwt:Key (co najmniej 32 znaki) w konfiguracji lub zmiennej środowiskowej Jwt__Key.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = AppClaims.Name,
            RoleClaimType = AppClaims.Role
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<CalendarService>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<FhirMapper>();
builder.Services.AddSingleton<INotificationSender, LoggingNotificationSender>();
builder.Services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
builder.Services.AddScoped<ImmunizationService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<ReminderService>();
builder.Services.AddScoped<FhirPatientService>();
builder.Services.AddScoped<DataSeeder>();

builder.Services.Configure<ReminderOptions>(config.GetSection("Reminders"));
builder.Services.AddHostedService<ReminderBackgroundService>();

builder.Services.Configure<FhirServerOptions>(config.GetSection("FhirServer"));
builder.Services.AddHttpClient<FhirServerClient>(client =>
{
    var baseUrl = config.GetSection("FhirServer").Get<FhirServerOptions>()?.BaseUrl ?? new FhirServerOptions().BaseUrl;
    client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddCors(options => options.AddPolicy("web", policy => policy
    .WithOrigins(config.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:4200"])
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "VaxPlan API",
        Version = "v1",
        Description = "Planowanie i monitorowanie programu szczepień w przychodni POZ (projekt studencki, dane syntetyczne)."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Wklej token z POST /api/auth/login"
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });

    var xmlFile = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xmlFile))
    {
        options.IncludeXmlComments(xmlFile);
    }
});

var app = builder.Build();

await InitializeDatabaseAsync(app);

app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI(o => o.DocumentTitle = "VaxPlan API");
app.UseCors("web");
app.UseAuthentication();
// Before authorization, so refused requests (401/403) end up in the access log too.
app.UseMiddleware<AuditMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

app.Run();

// Postgres in Docker can need a few seconds after the container starts, hence the retries.
static async Task InitializeDatabaseAsync(WebApplication app)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    for (var attempt = 1; ; attempt++)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();

            if (app.Configuration.GetValue("Seed:Enabled", true))
            {
                var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
                await seeder.SeedAsync(app.Configuration.GetValue("Seed:Patients", 80));
            }

            return;
        }
        catch (Exception ex) when (attempt < 10)
        {
            logger.LogWarning(ex, "Baza danych niedostępna (próba {Attempt}/10) – ponawiam za 3 s.", attempt);
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }
}

public partial class Program
{
}
