using modaar.api.Common.Errors;
using modaar.api.Common.Extensions;
using Serilog;

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

    builder.Services.AddControllers();
    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddModaarPersistence(builder.Configuration);
    builder.Services.AddModaarAuth(builder.Configuration);
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

    app.UseHttpsRedirection();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

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
