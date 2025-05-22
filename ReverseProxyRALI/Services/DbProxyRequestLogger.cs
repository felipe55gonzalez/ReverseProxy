// Services/DbProxyRequestLogger.cs
using Microsoft.EntityFrameworkCore;
using ReverseProxyRALI.Data;
using ReverseProxyRALI.Data.Entities;
using System.Diagnostics; // Para Stopwatch
using System.Text.Json;
using Microsoft.Extensions.Primitives; // Para StringValues

namespace ReverseProxyRALI.Services
{
    public class DbProxyRequestLogger : IProxyRequestLogger
    {
        private readonly IDbContextFactory<ProxyRaliDbContext> _dbContextFactory;
        private readonly ILogger<DbProxyRequestLogger> _logger;
        private readonly IEndpointCategorizer _endpointCategorizer; // Para obtener el grupo

        public DbProxyRequestLogger(
            IDbContextFactory<ProxyRaliDbContext> dbContextFactory,
            ILogger<DbProxyRequestLogger> logger,
            IEndpointCategorizer endpointCategorizer)
        {
            _dbContextFactory = dbContextFactory;
            _logger = logger;
            _endpointCategorizer = endpointCategorizer;
        }

        public async Task LogRequestAsync(HttpContext context, Func<Task> next)
        {
            var request = context.Request;
            var requestTime = DateTime.UtcNow;
            var stopwatch = Stopwatch.StartNew();

            // Habilitar buffering para leer el cuerpo múltiples veces
            request.EnableBuffering();
            string requestBodyPreview = "N/A";
            long? requestBodySizeBytes = request.ContentLength;

            if (request.ContentLength > 0 && request.Body.CanRead)
            {
                using (var reader = new StreamReader(request.Body, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true))
                {
                    var fullBody = await reader.ReadToEndAsync();
                    requestBodyPreview = fullBody.Length > 500 ? fullBody.Substring(0, 500) + "..." : fullBody; // Limitar tamaño de preview
                }
                request.Body.Position = 0; // Rebobinar para el siguiente middleware/YARP
            }

            // Capturar información antes de llamar a next()
            string clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "N/A";
            string method = request.Method;
            string path = request.Path.Value ?? string.Empty;
            string queryString = request.QueryString.HasValue ? request.QueryString.Value : null;
            string requestHeaders = SerializeHeaders(request.Headers);
            string userAgent = request.Headers["User-Agent"].FirstOrDefault();

            // Intentar obtener el TokenId y si fue válido (esto podría necesitar acceso a los resultados del middleware de token)
            // Por ahora, lo dejaremos simple y lo mejoraremos si es necesario.
            // Podríamos almacenar el resultado de la validación del token en HttpContext.Items.
            int? tokenIdUsed = null;
            bool? wasTokenValid = null;
            if (context.Items.TryGetValue("TokenValidationResult_TokenId", out var tokenIdObj) && tokenIdObj is int tId)
            {
                tokenIdUsed = tId;
            }
            if (context.Items.TryGetValue("TokenValidationResult_IsValid", out var isValidObj) && isValidObj is bool isValid)
            {
                wasTokenValid = isValid;
            }


            // Para ResponseBodyPreview y ResponseSizeBytes, necesitamos interceptar la respuesta
            var originalResponseBodyStream = context.Response.Body;
            using var responseBodyMemoryStream = new MemoryStream();
            context.Response.Body = responseBodyMemoryStream;

            string? proxyError = null;
            string? backendTarget = null; // YARP podría poner esto en HttpContext.Features

            try
            {
                await next.Invoke(); // Ejecutar el resto del pipeline (incluyendo YARP y el backend)
            }
            catch (Exception ex)
            {
                // Si una excepción ocurre en el pipeline DESPUÉS de este middleware
                proxyError = $"Excepción en el pipeline: {ex.Message}";
                // Asegurarse de que la respuesta original se restaure si hay un error antes de escribir la respuesta
                context.Response.Body = originalResponseBodyStream;
                throw; // Relanzar para que el manejador de excepciones global lo capture
            }
            finally // Asegurar que el logging ocurra incluso si hay una excepción no manejada más adelante
            {
                stopwatch.Stop();
                var durationMs = (int)stopwatch.ElapsedMilliseconds;

                // Leer el cuerpo de la respuesta
                responseBodyMemoryStream.Position = 0;
                string responseBodyPreview = "N/A";
                long? responseBodySizeBytes = responseBodyMemoryStream.Length > 0 ? responseBodyMemoryStream.Length : null;

                if (responseBodySizeBytes > 0)
                {
                    using (var reader = new StreamReader(responseBodyMemoryStream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true, bufferSize: 1024))
                    {
                        // Leer solo una parte para la vista previa
                        char[] buffer = new char[500];
                        int charsRead = await reader.ReadAsync(buffer, 0, buffer.Length);
                        responseBodyPreview = new string(buffer, 0, charsRead);
                        if (responseBodySizeBytes > 500) responseBodyPreview += "...";
                    }
                    responseBodyMemoryStream.Position = 0; // Rebobinar para copiar al stream original
                }

                // Copiar el contenido del MemoryStream al stream de respuesta original
                if (context.Response.HasStarted == false || responseBodyMemoryStream.Length > 0) // Evitar error si ya se envió la respuesta
                {
                    try
                    {
                        await responseBodyMemoryStream.CopyToAsync(originalResponseBodyStream);
                    }
                    catch (ObjectDisposedException odEx)
                    {
                        //_logger.LogWarning(odEx, "Stream de respuesta original ya fue dispuesto. No se pudo copiar el cuerpo de la respuesta para logging.");
                    }
                    catch (Exception ex)
                    {
                        //_logger.LogError(ex, "Error copiando el cuerpo de la respuesta al stream original.");
                    }
                }
                context.Response.Body = originalResponseBodyStream; // Restaurar el stream original


                // Obtener información después de que la solicitud ha pasado por YARP
                var endpointGroupCategorization = _endpointCategorizer.GetEndpointGroupForPath(path);
                string endpointGroupAccessed = endpointGroupCategorization?.GroupName ?? "Unknown";

                // YARP guarda el destino en HttpContext.Features
                var reverseProxyFeature = context.Features.Get<Yarp.ReverseProxy.Model.IReverseProxyFeature>();
                backendTarget = reverseProxyFeature?.ProxiedDestination?.Model?.Config?.Address;


                await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
                var logEntry = new RequestLog
                {
                    RequestId = context.TraceIdentifier, // Usar TraceIdentifier como RequestId
                    TimestampUtc = requestTime,
                    ClientIpAddress = clientIp,
                    HttpMethod = method,
                    RequestPath = path,
                    QueryString = queryString,
                    RequestHeaders = requestHeaders,
                    RequestBodyPreview = requestBodyPreview,
                    RequestSizeBytes = requestBodySizeBytes,
                    TokenIdUsed = tokenIdUsed,
                    WasTokenValid = wasTokenValid,
                    EndpointGroupAccessed = endpointGroupAccessed,
                    BackendTargetUrl = backendTarget,
                    ResponseStatusCode = context.Response.StatusCode,
                    ResponseHeaders = SerializeHeaders(context.Response.Headers),
                    ResponseBodyPreview = responseBodyPreview,
                    ResponseSizeBytes = responseBodySizeBytes,
                    DurationMs = durationMs,
                    ProxyProcessingError = proxyError, // Se llenará si hay una excepción capturada
                    UserAgent = userAgent,
                    // GeoCountry y GeoCity se llenarán más tarde
                };

                try
                {
                    dbContext.RequestLogs.Add(logEntry);
                    await dbContext.SaveChangesAsync();
                }
                catch (Exception dbEx)
                {
                    //_logger.LogError(dbEx, "Error al guardar RequestLog en la base de datos para RequestId: {RequestId}", logEntry.RequestId);
                }
            }
        }

        private string SerializeHeaders(IHeaderDictionary headers)
        {
            if (headers == null || !headers.Any()) return null;
            try
            {
                // Filtrar encabezados sensibles si es necesario
                var filteredHeaders = headers
                    // .Where(h => !h.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase)) // Ejemplo de filtro
                    .ToDictionary(h => h.Key, h => h.Value.ToString());
                return JsonSerializer.Serialize(filteredHeaders);
            }
            catch (Exception ex)
            {
                //_logger.LogError(ex, "Error serializando encabezados.");
                return "{\"error\":\"Error serializando encabezados\"}";
            }
        }
    }
}