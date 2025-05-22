using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ReverseProxyRALI.Data;
using ReverseProxyRALI.Services;
using ReverseProxyRALI.Models;
using System.Linq;
using System.Threading.Tasks;
using ReverseProxyRALI.Data.Entities;

namespace ReverseProxyRALI.Middlewares
{
    public class ProxyTokenValidationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ProxyTokenValidationMiddleware> _logger;
        private readonly IAuditLogger _auditLogger;

        public ProxyTokenValidationMiddleware(RequestDelegate next, ILogger<ProxyTokenValidationMiddleware> logger, IAuditLogger auditLogger)
        {
            _next = next;
            _logger = logger;
            _auditLogger = auditLogger;
        }

        public async Task InvokeAsync(HttpContext context,
                                      ITokenService tokenService,
                                      IEndpointCategorizer endpointCategorizer,
                                      IDbContextFactory<ProxyRaliDbContext> dbContextFactory)
        {
            if (string.Equals(context.Request.Method, "OPTIONS", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            string requestedPath = context.Request.Path.Value ?? string.Empty;
            EndpointCategorizationResult? categorizationResult = endpointCategorizer.GetEndpointGroupForPath(requestedPath);

            if (categorizationResult == null)
            {
                _logger.LogWarning("No se pudo categorizar la ruta: {RequestedPath}. Tratando como público por defecto y permitiendo acceso.", requestedPath);
                await _next(context);
                return;
            }

            _logger.LogInformation("Ruta '{RequestedPath}' categorizada como Grupo: '{GroupName}', RequiereToken: {RequiresToken}, PatrónCoincidente: '{PathPattern}'",
                requestedPath, categorizationResult.GroupName, categorizationResult.RequiresToken, categorizationResult.MatchedPathPattern);

            if (!categorizationResult.RequiresToken)
            {
                _logger.LogInformation("El grupo '{GroupName}' para la ruta '{RequestedPath}' no requiere validación de token. Saltando validación.", categorizationResult.GroupName, requestedPath);
                await _next(context);
                return;
            }

            _logger.LogInformation("El grupo '{GroupName}' para la ruta '{RequestedPath}' requiere validación de token.", categorizationResult.GroupName, requestedPath);
            var tokenHeader = context.Request.Headers["Authorization"].FirstOrDefault();

            if (string.IsNullOrEmpty(tokenHeader) || !tokenHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("[PROXY TOKEN] Error: Token faltante o malformado para '{RequestedPath}'. Recibido: '{TokenHeader}'", requestedPath, tokenHeader ?? "AUSENTE");
                await _auditLogger.LogEventAsync(
                    entityType: "TokenValidationAttempt",
                    entityId: "N/A_TokenMissingOrMalformed",
                    action: $"TokenMissingOrMalformed_ForGroup:{categorizationResult.GroupName}",
                    affectedComponent: "ProxyTokenValidationMiddleware",
                    clientIpAddress: context.Connection.RemoteIpAddress?.ToString(),
                    newValues: new { Path = requestedPath, ReceivedHeader = tokenHeader ?? "AUSENTE", EndpointGroup = categorizationResult.GroupName }
                );
                await WriteJsonResponse(context, HttpStatusCode.Unauthorized, "Proxy: Se requiere un token de autorización (Bearer) o tiene un formato incorrecto.");
                return;
            }

            string actualToken = tokenHeader.Substring("Bearer ".Length).Trim();
            var (isValid, tokenDetails) = await tokenService.ValidateTokenAsync(actualToken, categorizationResult.GroupName);

            if (!isValid)
            {
                _logger.LogWarning("[PROXY TOKEN] Error: Token '{ActualToken}' no es válido o no tiene permiso para el grupo '{EndpointGroup}' en la ruta '{RequestedPath}'.", actualToken, categorizationResult.GroupName, requestedPath);
                await _auditLogger.LogEventAsync(
                    entityType: "TokenValidationAttempt",
                    entityId: actualToken,
                    action: $"TokenInvalidOrNoPermission_ForGroup:{categorizationResult.GroupName}",
                    affectedComponent: "ProxyTokenValidationMiddleware",
                    clientIpAddress: context.Connection.RemoteIpAddress?.ToString(),
                    newValues: new { Path = requestedPath, Token = actualToken, EndpointGroup = categorizationResult.GroupName }
                );
                await WriteJsonResponse(context, HttpStatusCode.Forbidden, "Proxy: Token inválido o sin permisos suficientes para este recurso.");
                return;
            }

            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var permission = await dbContext.TokenPermissions
                .Include(tp => tp.Token)
                .Include(tp => tp.Group)
                .FirstOrDefaultAsync(tp => tp.Token != null && tp.Token.TokenValue == actualToken &&
                                           tp.Group != null && tp.Group.GroupName == categorizationResult.GroupName);

            if (permission == null || string.IsNullOrEmpty(permission.AllowedHttpMethods))
            {
                _logger.LogError("[PROXY TOKEN] Error CRÍTICO: No se encontraron detalles de permiso (AllowedHttpMethods) para Token '{ActualToken}' y Grupo '{EndpointGroup}' a pesar de que ValidateTokenAsync retornó true. Esto indica una inconsistencia.", actualToken, categorizationResult.GroupName);
                await _auditLogger.LogEventAsync(
                    entityType: "TokenPermissionError",
                    entityId: actualToken,
                    action: $"MissingHttpMethodPermissions_ForGroup:{categorizationResult.GroupName}",
                    affectedComponent: "ProxyTokenValidationMiddleware",
                    clientIpAddress: context.Connection.RemoteIpAddress?.ToString(),
                    newValues: new { Path = requestedPath, Token = actualToken, EndpointGroup = categorizationResult.GroupName }
                );
                await WriteJsonResponse(context, HttpStatusCode.Forbidden, "Proxy: Error interno de configuración de permisos (métodos HTTP no definidos para el token y grupo).");
                return;
            }

            var allowedMethods = permission.AllowedHttpMethods.Split(',').Select(m => m.Trim().ToUpperInvariant()).ToList();
            string requestMethodUpper = context.Request.Method.ToUpperInvariant();

            if (!allowedMethods.Contains(requestMethodUpper))
            {
                _logger.LogWarning("[PROXY TOKEN] Error: Método HTTP '{RequestMethod}' no permitido para Token '{ActualToken}' en Grupo '{EndpointGroup}'. Permitidos: {AllowedMethods}",
                    context.Request.Method, actualToken, categorizationResult.GroupName, permission.AllowedHttpMethods);
                await _auditLogger.LogEventAsync(
                    entityType: "TokenValidationAttempt",
                    entityId: actualToken,
                    action: $"HttpMethodNotAllowed_ForGroup:{categorizationResult.GroupName}_Method:{requestMethodUpper}",
                    affectedComponent: "ProxyTokenValidationMiddleware",
                    clientIpAddress: context.Connection.RemoteIpAddress?.ToString(),
                    newValues: new { Path = requestedPath, Token = actualToken, EndpointGroup = categorizationResult.GroupName, RequestedMethod = requestMethodUpper, AllowedMethods = permission.AllowedHttpMethods }
                );
                await WriteJsonResponse(context, HttpStatusCode.MethodNotAllowed, $"Proxy: Método HTTP '{context.Request.Method}' no permitido para este recurso con el token proporcionado.");
                return;
            }

            _logger.LogInformation("[PROXY TOKEN] Info: Token '{ActualToken}' y método '{RequestMethod}' validados para el grupo '{EndpointGroup}' en la ruta '{RequestedPath}'.",
                actualToken, context.Request.Method, categorizationResult.GroupName, requestedPath);

            await _auditLogger.LogEventAsync(
                entityType: "TokenValidationSuccess",
                entityId: actualToken,
                action: $"TokenValidated_ForGroup:{categorizationResult.GroupName}_Method:{requestMethodUpper}",
                affectedComponent: "ProxyTokenValidationMiddleware",
                clientIpAddress: context.Connection.RemoteIpAddress?.ToString(),
                newValues: new { Path = requestedPath, Token = actualToken, EndpointGroup = categorizationResult.GroupName, Method = requestMethodUpper }
            );

            context.Items["TokenValidationResult_IsValid"] = true;
            context.Items["TokenValidationResult_TokenValue"] = actualToken; 
            if (permission.TokenId > 0) 
            {
                context.Items["TokenValidationResult_TokenId"] = permission.TokenId; 
            }


            await _next(context);
        }

        private static async Task WriteJsonResponse(HttpContext context, HttpStatusCode statusCode, string message)
        {
            context.Response.StatusCode = (int)statusCode;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { message });
        }
        private static async Task WriteJsonResponse(HttpContext context, HttpStatusCode statusCode, object payload)
        {
            context.Response.StatusCode = (int)statusCode;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(payload);
        }
    }
}