using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using modaar.api.Common.Auth;
using modaar.api.Common.Localization;
using modaar.api.Common.Realtime;
using modaar.api.Common.Validation;
using modaar.api.Features.Authentication.Services;
using modaar.api.Features.Contracts.Services;
using modaar.api.Features.Dashboards.Services;
using modaar.api.Features.Maintenance.Services;
using modaar.api.Features.Payments.Services;
using modaar.api.Features.Properties.Services;
using modaar.api.Features.Users.Entities;
using modaar.api.Features.Users.Services;
using modaar.api.Persistence;
using System.Text;
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
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<ISecuritySettingsService, SecuritySettingsService>();
        services.AddScoped<IBrokerService, Features.Users.Services.BrokerService>();
        services.AddScoped<IOwnerSubscriptionService, OwnerSubscriptionService>();


        services.AddHttpContextAccessor();
        services.AddScoped<IMaintenanceService, MaintenanceService>();
        services.AddScoped<IMaintenanceAttachmentService, MaintenanceAttachmentService>();

        services.AddScoped<IMaintenanceActionService, MaintenanceActionService>();
        services.AddScoped<ITechnicianService, TechnicianService>();

        services.AddScoped<IMaintenanceFeedbackService, MaintenanceFeedbackService>();

        services.AddScoped<IMaintenanceServiceTypeService, MaintenanceServiceTypeService>();


        services.AddScoped<IContractService, ContractService>();
        //////////////Properties//////////////////////
        services.AddScoped<IPropertyService, PropertyService>();

        services.AddScoped<IPropertyHandoverService, PropertyHandoverService>();

        ///////////////////////////payments/////////////////////
        services.AddScoped<IPaymentService, PaymentService>();

        // Paying installments. With a Moyasar secret key the real gateway is used; without one, a
        // simulator, so the app can build the flow before the account exists. Test → live is
        // swapping the keys in config, nothing else.
        services.Configure<Features.Payments.Gateway.PaymentSettings>(
            configuration.GetSection(Features.Payments.Gateway.PaymentSettings.SectionName));
        var moyasar = configuration.GetSection(Features.Payments.Gateway.PaymentSettings.SectionName)
            .Get<Features.Payments.Gateway.PaymentSettings>()?.Moyasar;
        if (moyasar?.IsConfigured == true)
            services.AddHttpClient<Features.Payments.Gateway.IPaymentGatewayClient, Features.Payments.Gateway.MoyasarClient>();
        else
            services.AddSingleton<Features.Payments.Gateway.IPaymentGatewayClient, Features.Payments.Gateway.SimulatedMoyasarClient>();
        services.AddScoped<Features.Wallets.Services.IWalletService, Features.Wallets.Services.WalletService>();
        services.AddScoped<Features.Buildings.Services.IBuildingService, Features.Buildings.Services.BuildingService>();
        services.AddScoped<Features.Payments.Services.IPayService, Features.Payments.Services.PayService>();


        services.AddScoped<IDashboardService, DashboardService>();


        services.AddScoped<IRequestLanguage, RequestLanguage>();

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

    public static IServiceCollection AddModaarRealtime(this IServiceCollection services, IConfiguration configuration)
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

        // Scoped: it uses the request's DbContext.
        services.AddScoped<Features.Notifications.Services.INotificationService,
                           Features.Notifications.Services.NotificationService>();

        // Push: the queue and its background worker are singletons living for the whole app; the
        // sender is Firebase when a credentials file is configured, otherwise a no-op.
        services.AddSingleton<Push.PushQueue>();
        services.AddSingleton<Push.IPushSender>(sp =>
        {
            var settings = configuration.GetSection(Push.FirebaseSettings.SectionName).Get<Push.FirebaseSettings>();
            var path = settings?.CredentialsPath;

            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                return new Push.FirebasePushSender(path, sp.GetRequiredService<ILogger<Push.FirebasePushSender>>());

            if (!string.IsNullOrWhiteSpace(path))
                sp.GetRequiredService<ILogger<Push.NullPushSender>>()
                    .LogWarning("Firebase credentials file not found at {Path}; push is off.", path);

            return new Push.NullPushSender(sp.GetRequiredService<ILogger<Push.NullPushSender>>());
        });
        services.AddHostedService<Push.PushDispatcher>();

        // Once-a-day work. Registration order is run order: expiry before renewal reminders.
        services.Configure<Jobs.JobsSettings>(configuration.GetSection(Jobs.JobsSettings.SectionName));
        services.AddScoped<Jobs.IDailyJob, Features.Contracts.Jobs.ContractExpiryJob>();
        services.AddScoped<Jobs.IDailyJob, Features.Contracts.Jobs.ContractRenewalReminderJob>();
        services.AddScoped<Jobs.IDailyJob, Features.Payments.Jobs.RentReminderJob>();
        services.AddHostedService<Jobs.DailyJobsRunner>();
        services.AddScoped<Features.Authentication.Services.IDeviceService, Features.Authentication.Services.DeviceService>();

        services.AddScoped<Features.Chat.Services.ChatAccess>();
        services.AddScoped<Features.Chat.Services.IChatService, Features.Chat.Services.ChatService>();

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
