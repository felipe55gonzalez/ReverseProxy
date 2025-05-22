using System.Net;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using ReverseProxyRALI.Data;
using ReverseProxyRALI.Data.Entities;
using ReverseProxyRALI.Middlewares;
using ReverseProxyRALI.Services;
using System.Text;
using Microsoft.Extensions.Caching.Memory;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("ProxyDB"); 
if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("La cadena de conexión 'ProxyDB' no fue encontrada en appsettings.json.");
}

builder.Services.AddDbContextFactory<ProxyRaliDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddCors(options =>
{
    options.AddPolicy("ProxyCorsPolicy", policyBuilder =>
    {
        var tempServices = builder.Services.BuildServiceProvider();
        using (var scope = tempServices.CreateScope())
        {
            var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ProxyRaliDbContext>>();
            using var dbContext = dbContextFactory.CreateDbContext();
            try
            {
                var allowedOrigins = dbContext.AllowedCorsOrigins
                                            .Where(o => o.IsEnabled)
                                            .Select(o => o.OriginUrl)
                                            .ToArray();

                if (allowedOrigins.Any())
                {
                    policyBuilder.WithOrigins(allowedOrigins)
                                 .AllowAnyMethod()
                                 .AllowAnyHeader()
                                 .AllowCredentials();
                    Console.WriteLine($"CORS Policy 'ProxyCorsPolicy' configurada con los siguientes orígenes desde la BD: {string.Join(", ", allowedOrigins)}");
                }
                else
                {
                    policyBuilder.WithOrigins("http://localhost:INVALID_ORIGIN_BY_DEFAULT")
                                 .AllowAnyMethod().AllowAnyHeader().AllowCredentials();
                    Console.WriteLine("ADVERTENCIA: No se encontraron orígenes CORS habilitados en la BD. Política CORS restrictiva.");
                }
            }
            catch (Exception ex)
            {
                policyBuilder.WithOrigins("http://localhost:INVALID_ORIGIN_ON_DB_ERROR")
                             .AllowAnyMethod().AllowAnyHeader().AllowCredentials();
                Console.WriteLine($"ERROR al configurar CORS desde la BD: {ex.Message}. Política CORS restrictiva.");
            }
        }
    });
});

builder.Services.AddSingleton<IYarpConfigService, DbYarpConfigService>();
builder.Services.AddSingleton<Yarp.ReverseProxy.Configuration.IProxyConfigProvider, DbProxyConfigProvider>();
builder.Services.AddReverseProxy();

builder.Services.AddHostedService<HourlyTrafficAggregatorService>(); 
builder.Services.AddScoped<ITokenService, DbTokenService>();
builder.Services.AddSingleton<IEndpointCategorizer, PathBasedEndpointCategorizer>();
builder.Services.AddSingleton<IAuditLogger, DbAuditLogger>();
builder.Services.AddScoped<IProxyRequestLogger, DbProxyRequestLogger>();
builder.Services.AddSingleton<IIpBlockingService, DbIpBlockingService>();
builder.Services.AddMemoryCache();


builder.Services.AddLogging(loggingBuilder => loggingBuilder.AddConsole().SetMinimumLevel(LogLevel.Debug)); 


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";
            var exceptionHandlerFeature = context.Features.Get<IExceptionHandlerFeature>();
            var errorResponse = new { message = "Ocurrió un error interno en el proxy.", details = exceptionHandlerFeature?.Error.Message };
            await context.Response.WriteAsJsonAsync(errorResponse);
        });
    });
}

app.UseCors("ProxyCorsPolicy");

app.UseMiddleware<IpBlockingMiddleware>();

app.UseStatusCodePages(async statusCodeContext =>
{
    var context = statusCodeContext.HttpContext;
    var response = context.Response;
    if (response.StatusCode == (int)HttpStatusCode.BadGateway ||
        response.StatusCode == (int)HttpStatusCode.ServiceUnavailable)
    {
        response.ContentType = "application/json";
        var errorResponse = new
        {
            message = "El servicio API de backend no está disponible o no responde.",
            status = response.StatusCode,
            requestedPath = context.Request.Path.Value
        };
        await context.Response.WriteAsJsonAsync(errorResponse);
    }
});

app.UseMiddleware<ProxyLoggingMiddleware>();
app.UseMiddleware<ProxyTokenValidationMiddleware>();

app.MapReverseProxy();

app.Run();
