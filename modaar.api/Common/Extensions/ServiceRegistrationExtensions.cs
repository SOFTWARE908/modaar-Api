using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using modaar.api.Common.Auth;
using modaar.api.Common.Realtime;
using modaar.api.Common.Validation;
using System.Text;
using JsonWebTokens = Microsoft.IdentityModel.JsonWebTokens;

namespace modaar.api.Common.Extensions;

// The API host's own wiring. The business side (DbContext, services, validators) is AddModaarCore
// in modaar.core.
public static class ServiceRegistrationExtensions
{
    // Validating the app's JWTs. Issuing them is AuthService/JwtTokenService in core.
    public static IServiceCollection AddModaarAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
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

                // A WebSocket can't carry an Authorization header, so the SignalR client sends the
                // JWT as ?access_token=. Accept it from there for the hub only — everywhere else a
                // token in the URL would end up in logs and browser history.
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var token = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(token) &&
                            context.HttpContext.Request.Path.StartsWithSegments(RealtimeRoutes.AppHub))
                        {
                            context.Token = token;
                        }
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();

        return services;
    }

    // The SignalR hub and the background workers. Only the API runs these: the hub is where the
    // apps connect, and the daily jobs and push queue must run in one process.
    public static IServiceCollection AddModaarRealtime(this IServiceCollection services)
    {
        services
            .AddSignalR()
            // Same JSON shape as the REST API — camelCase enums — so a DTO looks identical whether
            // it arrives over HTTP or over the hub.
            .AddJsonProtocol(options =>
                options.PayloadSerializerOptions.Converters.Add(
                    new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase)));

        services.AddSingleton<Microsoft.AspNetCore.SignalR.IUserIdProvider, SubClaimUserIdProvider>();
        services.AddSingleton<IRealtimeNotifier, RealtimeNotifier>();

        services.AddHostedService<Push.PushDispatcher>();
        services.AddHostedService<Jobs.DailyJobsRunner>();

        return services;
    }

    // Core's validators come with AddModaarCore; this adds the ones written in the API itself (next
    // to a controller) and the filter that runs them all.
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
