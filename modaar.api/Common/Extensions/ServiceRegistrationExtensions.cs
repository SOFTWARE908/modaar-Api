using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using modaar.api.Common.Auth;
using modaar.api.Common.Validation;
using modaar.api.Features.Authentication.Services;
using modaar.api.Persistence;
using JsonWebTokens = Microsoft.IdentityModel.JsonWebTokens;

namespace modaar.api.Common.Extensions;

public static class ServiceRegistrationExtensions
{
    public static IServiceCollection AddModaarPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ModaarDb")
            ?? throw new InvalidOperationException("ConnectionStrings:ModaarDb is not configured.");

        services.AddDbContext<ModaarDbContext>(options => options.UseSqlServer(connectionString));
        return services;
    }

    public static IServiceCollection AddModaarAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.Configure<OtpSettings>(configuration.GetSection(OtpSettings.SectionName));

        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<IOtpHasher, Sha256OtpHasher>();
        services.AddSingleton<IOtpCodeGenerator, OtpCodeGenerator>();
        services.AddSingleton<IOtpDeliverySender, LoggingOtpDeliverySender>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();

        var jwtSection = configuration.GetSection(JwtSettings.SectionName);
        services.Configure<JwtSettings>(jwtSection);

        var jwtSettings = jwtSection.Get<JwtSettings>()
            ?? throw new InvalidOperationException("JwtSettings section is missing from configuration.");

        if (string.IsNullOrWhiteSpace(jwtSettings.SigningKey))
            throw new InvalidOperationException("JwtSettings:SigningKey is not configured.");

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SigningKey));

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                // Don't rewrite "sub" → ClaimTypes.NameIdentifier; we want the raw JWT claim names.
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = signingKey,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = JsonWebTokens.JwtRegisteredClaimNames.Sub
                };
            });

        services.AddAuthorization();
        return services;
    }

    public static IServiceCollection AddModaarValidation(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(ServiceRegistrationExtensions).Assembly);
        services.Configure<MvcOptions>(options => options.Filters.Add<ValidationActionFilter>());
        return services;
    }

    public static IServiceCollection AddModaarSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "Modaar API", Version = "v1" });

            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Description = "Paste your JWT here. Format: Bearer {token}",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            });

            c.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", doc, null!)] = new List<string>()
            });
        });
        return services;
    }
}
