using Microsoft.Extensions.FileProviders;
using modaar.api.Common.Errors;
using modaar.api.Common.Extensions;
using modaar.api.Common.Realtime;
using modaar.api.Common.Responses;
using modaar.api.Features.App;
using Serilog;
using System.Text.Json;
using System.Text.Json.Serialization;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.AddControllers(options => options.Filters.Add<ResponseEnvelopeFilter>())
            .AddJsonOptions(options =>
            {
                // The client expects "rented", "paintRepair", "apartment". Without this every enum goes
                // over as PascalCase and nothing matches.
                options.JsonSerializerOptions.Converters.Add(
                    new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

                // Validation error keys come from C# property names; align them with the JSON the client
                // actually sent, so "annualRent" is the key rather than "AnnualRent".
                options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
            }); ;
    builder.Services.Configure<AppVersionOptions>(
          builder.Configuration.GetSection(AppVersionOptions.SectionName));
    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddModaarPersistence(builder.Configuration);
    builder.Services.AddModaarAuth(builder.Configuration);
    builder.Services.AddModaarRealtime(builder.Configuration);
    builder.Services.AddModaarValidation();
    builder.Services.AddModaarSwagger();

    var app = builder.Build();

    app.UseExceptionHandler();
    app.UseSerilogRequestLogging();

    //if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }
    // wwwroot holds dev tools only (dev/realtime-test.html). Not served in production.
    if (app.Environment.IsDevelopment())
        app.UseStaticFiles();

    var uploadsRoot = builder.Configuration["Uploads:Root"] ?? @"E:\modaar-uploads";
    Directory.CreateDirectory(uploadsRoot);

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(uploadsRoot),
        RequestPath = "/uploads"
    });
    app.UseHttpsRedirection();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseMiddleware<AuthChallengeEnvelopeMiddleware>();

    app.MapControllers();
    app.MapHub<AppHub>(RealtimeRoutes.AppHub);

    app.Run();
}
// HostAbortedException is the expected exit signal during design-time tooling (dotnet ef migrations / dbcontext scaffold).
catch (Exception ex) when (ex is not Microsoft.Extensions.Hosting.HostAbortedException)
{
    Log.Fatal(ex, "Modaar API terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
