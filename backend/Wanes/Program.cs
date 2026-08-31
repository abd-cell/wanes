using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Wanes;
using Wanes.Areas.Domain.Users;
using Wanes.DataAccess;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Extensions;
using Wanes.Shareds.Middlewares;
using Wanes.Shareds.Json;
using Wanes.Shareds.Security;
using Wanes.Shareds.Security.Token;

var builder = WebApplication.CreateBuilder(args);

// ── Structured logging (Serilog) ──
builder.Host.UseSerilog((context, config) => config
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// ── Configuration models ──
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<Wanes.Shareds.Models.Config.OtpSettings>(builder.Configuration.GetSection("Otp"));
builder.Services.Configure<Wanes.Shareds.Models.Config.FcmSettings>(builder.Configuration.GetSection("Fcm"));
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();

// ── Framework ──
builder.Services.AddControllers(options => options.Filters.Add<ValidateModelAttribute>())
    // Inbound DateTimes are normalised to UTC — see UtcDateTimeConverter for why.
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
        o.JsonSerializerOptions.Converters.Add(new NullableUtcDateTimeConverter());
    });
// our ValidateModel filter owns invalid-model responses (uniform BaseResponse)
builder.Services.Configure<ApiBehaviorOptions>(o => o.SuppressModelStateInvalidFilter = true);
builder.Services.AddHttpContextAccessor();
builder.Services.AddEndpointsApiExplorer();
// Default schemaId uses only the short type name, so two same-named DTOs in
// different namespaces (e.g. Logging.ApiLogRow vs Management.ApiLogRow) collide
// and abort the whole document. Disambiguate by namespace-qualified name.
builder.Services.AddSwaggerGen(c => c.CustomSchemaIds(SwaggerSchemaIds.For));

// ── CORS (web app + CMS clients) ──
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

// ── Database (SQL Server + NetTopologySuite spatial) ──
builder.Services.AddDbContext<DatabaseService>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("Default"),
        sql => sql.UseNetTopologySuite()));

// ── Data access + convention-based DI ──
builder.Services.AddScoped(typeof(Wanes.DataAccess.Repositories.IRepository<>),
    typeof(Wanes.DataAccess.Repositories.Repository<>));
builder.Services.RegisterTypes();

// ── Authentication (JWT; session validated against UserLogin) ──
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
            // Default is five minutes of grace, which would quietly stretch every
            // access token past its stated expiry — and with it the window in which
            // a revoked session keeps working. The same server both issues and
            // validates these, so there are no clocks to reconcile.
            ClockSkew = TimeSpan.Zero,
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async ctx =>
            {
                var sessionKey = ctx.Principal?.FindFirst(AppClaims.SessionKey)?.Value;
                if (string.IsNullOrEmpty(sessionKey)) { ctx.Fail("no session"); return; }

                var db = ctx.HttpContext.RequestServices.GetRequiredService<DatabaseService>();
                var exists = await db.Set<UserLogin>()
                    .AnyAsync(l => l.SessionKey == sessionKey && !l.IsDeleted);
                if (!exists) ctx.Fail("session revoked");
            },
            // Without these the framework answers 401/403 with an empty body and
            // the client has nothing to show the user.
            OnChallenge = async ctx =>
            {
                ctx.HandleResponse();
                await AuthResponseWriter.WriteUnauthorizedAsync(ctx.HttpContext);
            },
            OnForbidden = ctx => AuthResponseWriter.WriteForbiddenAsync(ctx.HttpContext),
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Static HttpContext accessor
AppHttpContext.Configure(app.Services.GetRequiredService<IHttpContextAccessor>());

// ── Pipeline ──
app.UseSerilogRequestLogging();
app.UseMiddleware<ExceptionMiddleware>();
app.UseCors();

// Records one row per /api/ request. Sits ahead of auth so rejected (401/403)
// calls are captured too; the acting user is read after the pipeline unwinds.
app.UseMiddleware<ApiLoggerMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    // Absolute (rooted) definition URL so Swagger UI resolves it correctly even
    // when reached via a proxy or an entry path other than /swagger/index.html.
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Wanes v1"));
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Apply migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DatabaseService>();
    if (db.Database.GetPendingMigrations().Any())
        db.Database.Migrate();
    await Wanes.DataAccess.Seeders.DataSeeder.SeedAsync(db);
}

app.Run();

/// <summary>
/// Builds unique, readable Swagger schema ids. Generic types render as
/// "WrapperOfArg" (recursively); non-generic types are qualified by the trailing
/// namespace segment so identically named DTOs in different features don't clash.
/// </summary>
internal static class SwaggerSchemaIds
{
    public static string For(Type type)
    {
        if (type.IsGenericType)
        {
            var baseName = type.Name[..type.Name.IndexOf('`')];
            var args = string.Join("And", type.GetGenericArguments().Select(For));
            return $"{baseName}Of{args}";
        }

        var ns = type.Namespace;
        var lastSegment = ns?[(ns.LastIndexOf('.') + 1)..];
        // "Models" is the shared leaf folder for most DTOs — step up one level so
        // the id carries the feature name (Management, Logging, …) instead.
        if (lastSegment == "Models" && ns!.LastIndexOf('.') is var i and > 0)
        {
            var parent = ns[..i];
            lastSegment = parent[(parent.LastIndexOf('.') + 1)..];
        }

        return string.IsNullOrEmpty(lastSegment) ? type.Name : $"{lastSegment}{type.Name}";
    }
}
