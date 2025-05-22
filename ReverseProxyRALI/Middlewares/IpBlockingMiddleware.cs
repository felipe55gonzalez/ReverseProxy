using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ReverseProxyRALI.Services;
using System.Threading.Tasks;
using System;

namespace ReverseProxyRALI.Middlewares
{
    public class IpBlockingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IIpBlockingService _ipBlockingService;
        private readonly IAuditLogger _auditLogger;
        private readonly ILogger<IpBlockingMiddleware> _logger;

        public IpBlockingMiddleware(
            RequestDelegate next,
            IIpBlockingService ipBlockingService,
            IAuditLogger auditLogger,
            ILogger<IpBlockingMiddleware> logger)
        {
            _next = next;
            _ipBlockingService = ipBlockingService;
            _auditLogger = auditLogger;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var clientIpAddress = context.Connection.RemoteIpAddress;
            string clientIpString = clientIpAddress?.ToString() ?? "Desconocida";

            _logger.LogInformation("IpBlockingMiddleware: Verificando IP del cliente: {ClientIpString} para la ruta {Path}", clientIpString, context.Request.Path);
            Console.WriteLine($"[INFO][IpBlockingMiddleware] Verificando IP del cliente: {clientIpString} para la ruta {context.Request.Path}");

            if (clientIpAddress != null)
            {
                bool isBlocked = await _ipBlockingService.IsIpBlockedAsync(clientIpAddress);
                _logger.LogInformation("IpBlockingMiddleware: Resultado de IsIpBlockedAsync para {ClientIpString}: {IsBlocked}", clientIpString, isBlocked);
                Console.WriteLine($"[INFO][IpBlockingMiddleware] Resultado de IsIpBlockedAsync para {clientIpString}: {isBlocked}");

                if (isBlocked)
                {
                    _logger.LogWarning("IpBlockingMiddleware: Acceso denegado para IP bloqueada: {ClientIpString}, Ruta: {Path}", clientIpString, context.Request.Path);
                    Console.WriteLine($"[WARN][IpBlockingMiddleware] Acceso denegado para IP bloqueada: {clientIpString}, Ruta: {context.Request.Path}");

                    await _auditLogger.LogEventAsync(
                        entityType: "BlockedIpAccessAttempt",
                        entityId: clientIpString,
                        action: "AccessDenied_IpBlocked",
                        affectedComponent: "IpBlockingMiddleware",
                        clientIpAddress: clientIpString,
                        newValues: new { Path = context.Request.Path.Value, UserAgent = context.Request.Headers["User-Agent"].ToString() }
                    );

                    context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new { message = "Su dirección IP ha sido bloqueada y no tiene permiso para acceder a este recurso." });
                    return;
                }
                else
                {
                    _logger.LogInformation("IpBlockingMiddleware: IP {ClientIpString} no está bloqueada. Continuando con el pipeline.", clientIpString);
                    Console.WriteLine($"[INFO][IpBlockingMiddleware] IP {clientIpString} no está bloqueada. Continuando con el pipeline.");
                }
            }
            else
            {
                _logger.LogWarning("IpBlockingMiddleware: No se pudo determinar la dirección IP del cliente para la verificación de bloqueo. Permitiendo acceso por defecto.");
                Console.WriteLine("[WARN][IpBlockingMiddleware] No se pudo determinar la dirección IP del cliente para la verificación de bloqueo. Permitiendo acceso por defecto.");
            }

            await _next(context);
        }
    }
}
