using Microsoft.EntityFrameworkCore;
using ReverseProxyRALI.Data; // Para ProxyRaliDbContext
using ReverseProxyRALI.Data.Entities; // Para EndpointGroup
using ReverseProxyRALI.Models; // Para EndpointCategorizationResult
using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Extensions.Logging; // Asegúrate que este using esté
using System.Linq; // Asegúrate que este using esté

namespace ReverseProxyRALI.Services
{
    public class PathBasedEndpointCategorizer : IEndpointCategorizer
    {
        private readonly IDbContextFactory<ProxyRaliDbContext> _dbContextFactory;
        private readonly ILogger<PathBasedEndpointCategorizer> _logger;

        // Modificar el record interno para incluir RequiresToken
        private record EndpointGroupPattern(string PathPattern, string GroupName, int MatchOrder, bool RequiresToken);

        private List<EndpointGroupPattern> _cachedPatterns = new List<EndpointGroupPattern>();
        private DateTime _lastCacheRefresh = DateTime.MinValue;
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5); // O el valor que prefieras
        private readonly object _cacheLock = new object();

        public PathBasedEndpointCategorizer(IDbContextFactory<ProxyRaliDbContext> dbContextFactory, ILogger<PathBasedEndpointCategorizer> logger)
        {
            _logger = logger;
            _dbContextFactory = dbContextFactory;

            //_logger.LogDebug("PathBasedEndpointCategorizer constructor called.");
            Console.WriteLine("[DEBUG] Constructor de PathBasedEndpointCategorizer llamado.");
            //_logger.LogDebug("Calling RefreshPatternsCacheAsync from constructor.");
            Console.WriteLine("[DEBUG] Llamando a RefreshPatternsCacheAsync desde el constructor.");
            RefreshPatternsCacheAsync(CancellationToken.None).GetAwaiter().GetResult();
            //_logger.LogDebug("PathBasedEndpointCategorizer constructor finished.");
            Console.WriteLine("[DEBUG] Constructor de PathBasedEndpointCategorizer finalizado.");
        }

        private async Task RefreshPatternsCacheAsync(CancellationToken cancellationToken)
        {
            //_logger.LogDebug("RefreshPatternsCacheAsync called.");
            Console.WriteLine("[DEBUG] RefreshPatternsCacheAsync llamado.");
            //_logger.LogInformation("Refrescando caché de patrones de EndpointGroup desde la base de datos...");
            Console.WriteLine("[INFO] Refrescando caché de patrones de EndpointGroup desde la base de datos...");
            try
            {
                //_logger.LogDebug("Creating DbContext for refreshing cache.");
                Console.WriteLine("[DEBUG] Creando DbContext para refrescar la caché.");
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                //_logger.LogDebug("Fetching patterns from database.");
                Console.WriteLine("[DEBUG] Obteniendo patrones de la base de datos.");
                var patternsFromDb = await dbContext.EndpointGroups
                    .Where(eg => !string.IsNullOrEmpty(eg.PathPattern) && eg.PathPattern != "/api/default_placeholder/*" && eg.PathPattern != "/api/temp_default/*") // Ignorar placeholders
                    .OrderBy(eg => eg.MatchOrder)
                    .ThenByDescending(eg => eg.PathPattern.Length)
                    // Incluir ReqToken en la selección
                    .Select(eg => new EndpointGroupPattern(eg.PathPattern, eg.GroupName, eg.MatchOrder, eg.ReqToken))
                    .ToListAsync(cancellationToken);

                //_logger.LogDebug("Fetched {Count} patterns from database.", patternsFromDb.Count);
                Console.WriteLine($"[DEBUG] Obtenidos {patternsFromDb.Count} patrones de la base de datos.");

                lock (_cacheLock)
                {
                    //_logger.LogDebug("Acquired cacheLock to update cachedPatterns.");
                    Console.WriteLine("[DEBUG] Bloqueo de caché adquirido para actualizar cachedPatterns.");
                    _cachedPatterns = patternsFromDb;
                    _lastCacheRefresh = DateTime.UtcNow;
                    //_logger.LogDebug("Released cacheLock after updating cachedPatterns.");
                    Console.WriteLine("[DEBUG] Bloqueo de caché liberado después de actualizar cachedPatterns.");
                }
                //_logger.LogInformation("Caché de patrones de EndpointGroup refrescada. {Count} patrones cargados.", _cachedPatterns.Count);
                Console.WriteLine($"[INFO] Caché de patrones de EndpointGroup refrescada. {_cachedPatterns.Count} patrones cargados.");
            }
            catch (Exception ex)
            {
                //_logger.LogError(ex, "Error al refrescar la caché de patrones de EndpointGroup.");
                Console.WriteLine($"[ERROR] Error al refrescar la caché de patrones de EndpointGroup: {ex.Message}");
            }
            //_logger.LogDebug("RefreshPatternsCacheAsync finished.");
            Console.WriteLine("[DEBUG] RefreshPatternsCacheAsync finalizado.");
        }

        // Cambiar el tipo de retorno
        public EndpointCategorizationResult? GetEndpointGroupForPath(string requestPath)
        {
            //_logger.LogDebug("GetEndpointGroupForPath called with requestPath: {RequestPath}", requestPath);
            Console.WriteLine($"[DEBUG] GetEndpointGroupForPath llamado con requestPath: {requestPath}");

            if (string.IsNullOrEmpty(requestPath))
            {
                //_logger.LogDebug("RequestPath is null or empty. Returning default public group.");
                Console.WriteLine("[DEBUG] RequestPath es nulo o vacío. Retornando 'Public_Group_EmptyPath'.");
                return new EndpointCategorizationResult("Public_Group_EmptyPath", false, string.Empty);
            }

            //_logger.LogDebug("Current UTC time: {UtcNow}, Last cache refresh: {LastCacheRefresh}, Cache duration: {CacheDuration}", DateTime.UtcNow, _lastCacheRefresh, _cacheDuration);
            Console.WriteLine($"[DEBUG] Hora UTC actual: {DateTime.UtcNow}, Última actualización de caché: {_lastCacheRefresh}, Duración de caché: {_cacheDuration}");
            if (DateTime.UtcNow - _lastCacheRefresh > _cacheDuration)
            {
                //_logger.LogDebug("Cache duration expired. Attempting to refresh cache.");
                Console.WriteLine("[DEBUG] Duración de la caché expirada. Intentando refrescar la caché.");
                lock (_cacheLock)
                {
                    //_logger.LogDebug("Acquired cacheLock for checking cache duration again.");
                    Console.WriteLine("[DEBUG] Bloqueo de caché adquirido para verificar nuevamente la duración de la caché.");
                    if (DateTime.UtcNow - _lastCacheRefresh > _cacheDuration)
                    {
                        //_logger.LogDebug("Cache duration still expired. Calling RefreshPatternsCacheAsync.");
                        Console.WriteLine("[DEBUG] Duración de la caché aún expirada. Llamando a RefreshPatternsCacheAsync.");
                        RefreshPatternsCacheAsync(CancellationToken.None).GetAwaiter().GetResult();
                    }
                    else
                    {
                        //_logger.LogDebug("Cache was refreshed by another thread. No need to refresh again.");
                        Console.WriteLine("[DEBUG] La caché fue refrescada por otro hilo. No es necesario refrescar de nuevo.");
                    }
                    //_logger.LogDebug("Released cacheLock after checking cache duration.");
                    Console.WriteLine("[DEBUG] Bloqueo de caché liberado después de verificar la duración de la caché.");
                }
            }
            else
            {
                //_logger.LogDebug("Cache is still valid. Not refreshing.");
                Console.WriteLine("[DEBUG] La caché sigue siendo válida. No se refresca.");
            }

            List<EndpointGroupPattern> currentPatterns;
            lock (_cacheLock)
            {
                //_logger.LogDebug("Acquired cacheLock to copy cachedPatterns.");
                Console.WriteLine("[DEBUG] Bloqueo de caché adquirido para copiar cachedPatterns.");
                currentPatterns = new List<EndpointGroupPattern>(_cachedPatterns);
                //_logger.LogDebug("Released cacheLock after copying cachedPatterns. Copied {Count} patterns.", currentPatterns.Count);
                Console.WriteLine($"[DEBUG] Bloqueo de caché liberado después de copiar cachedPatterns. Copiados {currentPatterns.Count} patrones.");
            }

            //_logger.LogDebug("Iterating through {Count} current patterns to find a match for requestPath: {RequestPath}", currentPatterns.Count, requestPath);
            Console.WriteLine($"[DEBUG] Iterando a través de {currentPatterns.Count} patrones actuales para encontrar una coincidencia para requestPath: {requestPath}");
            foreach (var patternInfo in currentPatterns)
            {
                //_logger.LogDebug("Checking pattern: PathPattern='{PathPattern}', GroupName='{GroupName}', MatchOrder={MatchOrder}, ReqToken={ReqToken}",patternInfo.PathPattern, patternInfo.GroupName, patternInfo.MatchOrder, patternInfo.RequiresToken);
                Console.WriteLine($"[DEBUG] Verificando patrón: PathPattern='{patternInfo.PathPattern}', GroupName='{patternInfo.GroupName}', MatchOrder={patternInfo.MatchOrder}, ReqToken={patternInfo.RequiresToken}");

                if (PathMatches(requestPath, patternInfo.PathPattern))
                {
                    //_logger.LogDebug("Ruta '{RequestPath}' coincidió con el patrón '{PathPattern}' para el grupo '{GroupName}'. ReqToken: {RequiresToken}",requestPath, patternInfo.PathPattern, patternInfo.GroupName, patternInfo.RequiresToken);
                    Console.WriteLine($"[DEBUG] Ruta '{requestPath}' coincidió con el patrón '{patternInfo.PathPattern}' para el grupo '{patternInfo.GroupName}'. ReqToken: {patternInfo.RequiresToken}");
                    return new EndpointCategorizationResult(patternInfo.GroupName, patternInfo.RequiresToken, patternInfo.PathPattern);
                }
                else
                {
                    //_logger.LogDebug("Path '{RequestPath}' did not match pattern '{PathPattern}'.", requestPath, patternInfo.PathPattern);
                    Console.WriteLine($"[DEBUG] Ruta '{requestPath}' no coincidió con el patrón '{patternInfo.PathPattern}'.");
                }
            }

            //_logger.LogDebug("Ningún patrón específico de la base de datos coincidió con la ruta '{RequestPath}'. Usando 'Public_Group_NoMatch' por defecto.", requestPath);
            Console.WriteLine($"[DEBUG] Ningún patrón específico de la base de datos coincidió con la ruta '{requestPath}'. Usando 'Public_Group_NoMatch' por defecto.");
            return new EndpointCategorizationResult("Public_Group_NoMatch", false, string.Empty);
        }

        private bool PathMatches(string requestPath, string pattern)
        {
            //_logger.LogDebug("PathMatches called with requestPath: '{RequestPath}', pattern: '{Pattern}'", requestPath, pattern);
            Console.WriteLine($"[DEBUG] PathMatches llamado con requestPath: '{requestPath}', pattern: '{pattern}'");
            bool matchFound;

            if (pattern.EndsWith("/{**remainder}"))
            {
                string prefixPattern = pattern.Substring(0, pattern.Length - "/{**remainder}".Length);
                //_logger.LogDebug("El patrón termina con '/{{**remainder}}'. Patrón de Prefijo: '{PrefixPattern}'", prefixPattern);
                Console.WriteLine($"[DEBUG] El patrón termina con '/{{**remainder}}'. Patrón de Prefijo: '{prefixPattern}'");

                if (prefixPattern.Contains("{") && prefixPattern.Contains("}"))
                {
                    string regexPrefixString = ConvertPathPatternToRegexPrefix(prefixPattern);
                    Console.WriteLine($"[DEBUG] Prefijo convertido a Regex: '{regexPrefixString}'");

                    if (Regex.IsMatch(requestPath, $"^{regexPrefixString}$", RegexOptions.IgnoreCase))
                    {
                        matchFound = true;
                        //_logger.LogDebug("Coincidencia exacta del prefijo con Regex. RequestPath: '{RequestPath}', Regex: '^{RegexPrefixString}$'", requestPath, regexPrefixString);
                        Console.WriteLine($"[DEBUG] Coincidencia exacta del prefijo con Regex. RequestPath: '{requestPath}', Regex: '^{regexPrefixString}$'");
                    }
                    else if (Regex.IsMatch(requestPath, $"^{regexPrefixString}\\/.*$", RegexOptions.IgnoreCase))
                    {
                        matchFound = true;
                        //_logger.LogDebug("Coincidencia del prefijo con Regex más '/...'. RequestPath: '{RequestPath}', Regex: '^{RegexPrefixString}\\/.*$'", requestPath, regexPrefixString);
                        Console.WriteLine($"[DEBUG] Coincidencia del prefijo con Regex más '/...'. RequestPath: '{requestPath}', Regex: '^{regexPrefixString}\\/.*$'");
                    }
                    else
                    {
                        matchFound = false;
                        //_logger.LogDebug("No hubo coincidencia con Regex para el prefijo. RequestPath: '{RequestPath}', Regex evaluados: '^{RegexPrefixString}$' y '^{RegexPrefixString}\\/.*$'", requestPath, regexPrefixString);
                        Console.WriteLine($"[DEBUG] No hubo coincidencia con Regex para el prefijo. RequestPath: '{requestPath}', Regex evaluados: '^{regexPrefixString}$' y '^{regexPrefixString}\\/.*$'");
                    }
                }
                else
                {
                    matchFound = requestPath.Equals(prefixPattern, StringComparison.OrdinalIgnoreCase) ||
                                 requestPath.StartsWith(prefixPattern + "/", StringComparison.OrdinalIgnoreCase);
                    //_logger.LogDebug("Coincidencia del prefijo literal. RequestPath: '{RequestPath}', PrefixPattern: '{PrefixPattern}', Equals: {EqualsResult}, StartsWith: {StartsWithResult}", requestPath, prefixPattern, requestPath.Equals(prefixPattern, StringComparison.OrdinalIgnoreCase), requestPath.StartsWith(prefixPattern + "/", StringComparison.OrdinalIgnoreCase));
                    Console.WriteLine($"[DEBUG] Coincidencia del prefijo literal. RequestPath: '{requestPath}', PrefixPattern: '{prefixPattern}', Equals: {requestPath.Equals(prefixPattern, StringComparison.OrdinalIgnoreCase)}, StartsWith: {requestPath.StartsWith(prefixPattern + "/", StringComparison.OrdinalIgnoreCase)}");
                }

                //_logger.LogDebug("Resultado de PathMatches para el patrón '/{{**remainder}}': {MatchFound}", matchFound);
                Console.WriteLine($"[DEBUG] Resultado de PathMatches para el patrón '/{{**remainder}}': {matchFound}");
                return matchFound;
            }
            else if (pattern.EndsWith("/*"))
            {
                string prefixPattern = pattern.Substring(0, pattern.Length - "/*".Length);
                //_logger.LogDebug("El patrón termina con '/*'. Patrón de Prefijo: '{PrefixPattern}'", prefixPattern);
                Console.WriteLine($"[DEBUG] El patrón termina con '/*'. Patrón de Prefijo: '{prefixPattern}'");

                if (prefixPattern.Contains("{") && prefixPattern.Contains("}"))
                {
                    string regexPrefixString = ConvertPathPatternToRegexPrefix(prefixPattern);
                    Console.WriteLine($"[DEBUG] Prefijo '/*' convertido a Regex: '{regexPrefixString}'");
                    matchFound = Regex.IsMatch(requestPath, $"^{regexPrefixString}\\/[^/]+(?:\\/.*)?$", RegexOptions.IgnoreCase);
                    //_logger.LogDebug("Resultado de PathMatches para el patrón '/*' con Regex: {MatchFound}. RequestPath: '{RequestPath}', Regex: '^{RegexPrefixString}\\/[^/]+(?:\\/.*)?$'", matchFound, requestPath, regexPrefixString);
                    Console.WriteLine($"[DEBUG] Resultado de PathMatches para el patrón '/*' con Regex: {matchFound}. RequestPath: '{requestPath}', Regex: '^{regexPrefixString}\\/[^/]+(?:\\/.*)?$'");
                }
                else
                {
                    if (requestPath.StartsWith(prefixPattern + "/", StringComparison.OrdinalIgnoreCase))
                    {
                        string remainder = requestPath.Substring(prefixPattern.Length + 1);
                        matchFound = remainder.Length > 0 && !remainder.Contains("/");
                        //_logger.LogDebug("Coincidencia del prefijo literal para '/*'. RequestPath: '{RequestPath}', PrefixPattern: '{PrefixPattern}', Remainder: '{Remainder}', Match: {MatchFound}", requestPath, prefixPattern, remainder, matchFound);
                        Console.WriteLine($"[DEBUG] Coincidencia del prefijo literal para '/*'. RequestPath: '{requestPath}', PrefixPattern: '{prefixPattern}', Remainder: '{remainder}', Match: {matchFound}");
                    }
                    else
                    {
                        matchFound = false;
                        //_logger.LogDebug("El prefijo literal para '/*' no coincidió o no fue seguido por '/'. RequestPath: '{RequestPath}', PrefixPattern: '{PrefixPattern}'", requestPath, prefixPattern);
                        Console.WriteLine($"[DEBUG] El prefijo literal para '/*' no coincidió o no fue seguido por '/'. RequestPath: '{requestPath}', PrefixPattern: '{prefixPattern}'");
                    }
                }
                return matchFound;
            }
            else
            {
                if (pattern.Contains("{") && pattern.Contains("}"))
                {
                    string regexPattern = ConvertPathPatternToRegexPrefix(pattern);
                    Console.WriteLine($"[DEBUG] Patrón exacto convertido a Regex: '{regexPattern}'");
                    matchFound = Regex.IsMatch(requestPath, $"^{regexPattern}$", RegexOptions.IgnoreCase);
                    //_logger.LogDebug("Resultado de PathMatches para patrón exacto con Regex: {MatchFound}. RequestPath: '{RequestPath}', Regex: '^{RegexPattern}$'", matchFound, requestPath, regexPattern);
                    Console.WriteLine($"[DEBUG] Resultado de PathMatches para patrón exacto con Regex: {matchFound}. RequestPath: '{requestPath}', Regex: '^{regexPattern}$'");
                }
                else
                {
                    matchFound = requestPath.Equals(pattern, StringComparison.OrdinalIgnoreCase);
                    //_logger.LogDebug("El patrón es una coincidencia exacta literal. Resultado: {MatchFound}", matchFound);
                    Console.WriteLine($"[DEBUG] El patrón es una coincidencia exacta literal. Resultado: {matchFound}");
                }
                return matchFound;
            }
        }

        private string ConvertPathPatternToRegexPrefix(string pathPattern)
        {
            Console.WriteLine($"[DEBUG] ConvertPathPatternToRegexPrefix llamado con pathPattern: '{pathPattern}'");
            var regexBuilder = new StringBuilder();
            var segments = pathPattern.Split('/');
            bool firstSegmentProcessed = false;

            for (int i = 0; i < segments.Length; i++)
            {
                string segment = segments[i];

                if (i == 0 && string.IsNullOrEmpty(segment) && pathPattern.StartsWith("/"))
                {
                    regexBuilder.Append("\\/");
                    firstSegmentProcessed = true;
                    continue;
                }

                if (firstSegmentProcessed || (i > 0 && pathPattern.StartsWith("/")))
                {
                    if (!(i == 1 && string.IsNullOrEmpty(segments[0]) && pathPattern.StartsWith("/")))
                    {
                        regexBuilder.Append("\\/");
                    }
                }
                else if (i > 0)
                {
                    regexBuilder.Append("\\/");
                }

                if (segment.StartsWith("{") && segment.EndsWith("}"))
                {
                    regexBuilder.Append("([^/]+)");
                }
                else
                {
                    regexBuilder.Append(Regex.Escape(segment));
                }
                if (!string.IsNullOrEmpty(segment) || (i == 0 && string.IsNullOrEmpty(segment) && pathPattern.StartsWith("/")))
                {
                    firstSegmentProcessed = true;
                }
            }
            string resultRegex = regexBuilder.ToString();
            Console.WriteLine($"[DEBUG] ConvertPathPatternToRegexPrefix resultado para '{pathPattern}': '{resultRegex}'");
            return resultRegex;
        }
    }
}