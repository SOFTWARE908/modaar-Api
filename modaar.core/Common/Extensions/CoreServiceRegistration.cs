using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using modaar.api.Common.Auth;
using modaar.api.Common.Localization;
using modaar.api.Common.Realtime;
using modaar.api.Features.Authentication.Services;
using modaar.api.Features.Contracts.Services;
using modaar.api.Features.Dashboards.Services;
using modaar.api.Features.Maintenance.Services;
using modaar.api.Features.Payments.Services;
using modaar.api.Features.Properties.Services;
using modaar.api.Features.Users.Services;
using modaar.api.Persistence;

namespace modaar.api.Common.Extensions;

// Everything a host needs to run the business rules: the DbContext, every service, validators,
// notifications and push. Each web host (the API, the admin panel) calls this and adds its own
// auth, pipeline and background workers on top.
//
// The host must also register an IRealtimeNotifier (the API's pushes over its SignalR hub).
public static class CoreServiceRegistration
{
    public static IServiceCollection AddModaarCore(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ModaarDb")
            ?? throw new InvalidOperationException("ConnectionStrings:ModaarDb is not configured.");
        services.AddDbContext<ModaarDbContext>(options => options.UseSqlServer(connectionString));

        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();
        services.AddScoped<IRequestLanguage, RequestLanguage>();

        // Sign-in: OTP, passwords and issuing tokens (validating them is the host's job).
        services.Configure<OtpSettings>(configuration.GetSection(OtpSettings.SectionName));
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<IOtpHasher, Sha256OtpHasher>();
        services.AddSingleton<IOtpCodeGenerator, OtpCodeGenerator>();
        services.AddSingleton<IOtpDeliverySender, LoggingOtpDeliverySender>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IDeviceService, DeviceService>();

        // Users, brokers, mandates, announcements.
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<ISecuritySettingsService, SecuritySettingsService>();
        services.AddScoped<IBrokerService, Features.Users.Services.BrokerService>();
        services.AddScoped<IOwnerSubscriptionService, OwnerSubscriptionService>();
        services.AddScoped<IBrokerClientService, BrokerClientService>();
        services.AddScoped<IBrokerServiceCategoryService, BrokerServiceCategoryService>();
        services.AddScoped<Features.Agreements.Services.IManagementAgreementService, Features.Agreements.Services.ManagementAgreementService>();
        services.AddScoped<Features.Announcements.Services.IAnnouncementService, Features.Announcements.Services.AnnouncementService>();

        // Maintenance.
        services.AddScoped<IMaintenanceService, MaintenanceService>();
        services.AddScoped<IMaintenanceAttachmentService, MaintenanceAttachmentService>();
        services.AddScoped<IMaintenanceActionService, MaintenanceActionService>();
        services.AddScoped<ITechnicianService, TechnicianService>();
        services.AddScoped<IOwnerTechnicianService, OwnerTechnicianService>();
        services.AddScoped<IMaintenanceFeedbackService, MaintenanceFeedbackService>();
        services.AddScoped<IMaintenanceServiceTypeService, MaintenanceServiceTypeService>();

        // Properties, buildings, leases.
        services.AddScoped<IContractService, ContractService>();
        services.AddScoped<IPropertyService, PropertyService>();
        services.AddScoped<IPropertyHandoverService, PropertyHandoverService>();
        services.AddScoped<Features.Buildings.Services.IBuildingService, Features.Buildings.Services.BuildingService>();
        services.AddScoped<Features.Buildings.Services.IBuildingRuleService, Features.Buildings.Services.BuildingRuleService>();

        // Payments and wallets. With a Moyasar secret key the real gateway is used; without one, a
        // simulator, so the app can build the flow before the account exists. Test → live is
        // swapping the keys in config, nothing else.
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IReceiptShareService, ReceiptShareService>();
        services.AddScoped<IPayService, PayService>();
        services.AddScoped<Features.Wallets.Services.IWalletService, Features.Wallets.Services.WalletService>();
        services.Configure<Features.Payments.Gateway.PaymentSettings>(
            configuration.GetSection(Features.Payments.Gateway.PaymentSettings.SectionName));
        var moyasar = configuration.GetSection(Features.Payments.Gateway.PaymentSettings.SectionName)
            .Get<Features.Payments.Gateway.PaymentSettings>()?.Moyasar;
        if (moyasar?.IsConfigured == true)
            services.AddHttpClient<Features.Payments.Gateway.IPaymentGatewayClient, Features.Payments.Gateway.MoyasarClient>();
        else
            services.AddSingleton<Features.Payments.Gateway.IPaymentGatewayClient, Features.Payments.Gateway.SimulatedMoyasarClient>();

        services.AddScoped<IDashboardService, DashboardService>();

        // Notifications and chat. Scoped: they use the request's DbContext.
        services.AddScoped<Features.Notifications.Services.INotificationService, Features.Notifications.Services.NotificationService>();
        services.AddSingleton<PresenceTracker>();
        services.AddScoped<Features.Chat.Services.ChatAccess>();
        services.AddScoped<Features.Chat.Services.IChatService, Features.Chat.Services.ChatService>();

        // Push: the queue lives for the whole app; the sender is Firebase when a credentials file is
        // configured, otherwise a no-op. The host runs the PushDispatcher that drains the queue.
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

        // Once-a-day work. Registration order is run order: expiry before renewal reminders. The
        // host decides whether to run them (DailyJobsRunner); only one host should.
        services.Configure<Jobs.JobsSettings>(configuration.GetSection(Jobs.JobsSettings.SectionName));
        services.AddScoped<Jobs.IDailyJob, Features.Contracts.Jobs.ContractExpiryJob>();
        services.AddScoped<Jobs.IDailyJob, Features.Agreements.Jobs.AgreementUnitExitJob>();
        services.AddScoped<Jobs.IDailyJob, Features.Agreements.Jobs.AgreementExitReminderJob>();
        services.AddScoped<Jobs.IDailyJob, Features.Contracts.Jobs.ContractRenewalReminderJob>();
        services.AddScoped<Jobs.IDailyJob, Features.Payments.Jobs.RentReminderJob>();

        // The DTO validators live next to their DTOs, in this assembly.
        services.AddValidatorsFromAssembly(typeof(CoreServiceRegistration).Assembly);

        return services;
    }
}
