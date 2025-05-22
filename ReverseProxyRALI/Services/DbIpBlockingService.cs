using Microsoft.EntityFrameworkCore;
using ReverseProxyRALI.Data;
using ReverseProxyRALI.Data.Entities; // Asegúrate que BlockedIp esté aquí
using System.Net;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace ReverseProxyRALI.Services
{
    public class DbIpBlockingService : IIpBlockingService
    {
        private readonly IDbContextFactory<ProxyRaliDbContext> _dbContextFactory;
        private readonly ILogger<DbIpBlockingService> _logger;
        private readonly IMemoryCache _memoryCache;
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(1); // Reducido para pruebas, puedes ajustarlo

        public DbIpBlockingService(
            IDbContextFactory<ProxyRaliDbContext> dbContextFactory,
            ILogger<DbIpBlockingService> logger,
            IMemoryCache memoryCache)
        {
            _dbContextFactory = dbContextFactory;
            _logger = logger;
            _memoryCache = memoryCache;
        }

        public async Task<bool> IsIpBlockedAsync(IPAddress? ipAddress)
        {
            if (ipAddress == null)
            {
                //_logger.LogWarning("Se intentó verificar una IP nula para bloqueo.");
                Console.WriteLine("[WARN][DbIpBlockingService] Se intentó verificar una IP nula para bloqueo.");
                return false;
            }

            string ipString = ipAddress.ToString();
            // Para IPv6 loopback, normalizar a la forma compacta "::1"
            if (ipAddress.Equals(IPAddress.IPv6Loopback))
            {
                ipString = "::1";
            }

            string cacheKey = $"BlockedIp_{ipString}";

            ////_logger.LogDebug("Verificando IP para bloqueo: {IpAddressString} (CacheKey: {CacheKey})", ipString, cacheKey);
            Console.WriteLine($"[DEBUG][DbIpBlockingService] Verificando IP para bloqueo: {ipString} (CacheKey: {cacheKey})");

            if (_memoryCache.TryGetValue(cacheKey, out bool isBlockedCached))
            {
                //_logger.LogInformation("Resultado de bloqueo para IP '{IpAddressString}' obtenido de la caché: {IsBlockedCached}", ipString, isBlockedCached);
                Console.WriteLine($"[INFO][DbIpBlockingService] Resultado de bloqueo para IP '{ipString}' obtenido de la caché: {isBlockedCached}");
                return isBlockedCached;
            }

            //_logger.LogInformation("IP '{IpAddressString}' no encontrada en caché. Consultando base de datos.", ipString);
            Console.WriteLine($"[INFO][DbIpBlockingService] IP '{ipString}' no encontrada en caché. Consultando base de datos.");
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            try
            {
                var blockedEntry = await dbContext.BlockedIps
                    .FirstOrDefaultAsync(b => b.IpAddress == ipString);

                bool isCurrentlyBlocked = false;
                if (blockedEntry != null)
                {
                    //_logger.LogInformation("Entrada de bloqueo encontrada para IP '{IpAddressString}': BlockedUntil={BlockedUntilDate}, Reason='{Reason}'",ipString, blockedEntry.BlockedUntil?.ToString() ?? "Permanente", blockedEntry.Reason ?? "N/A");
                    Console.WriteLine($"[INFO][DbIpBlockingService] Entrada de bloqueo encontrada para IP '{ipString}': BlockedUntil={blockedEntry.BlockedUntil?.ToString() ?? "Permanente"}, Reason='{blockedEntry.Reason ?? "N/A"}'");

                    isCurrentlyBlocked = (blockedEntry.BlockedUntil == null || blockedEntry.BlockedUntil > DateTime.UtcNow);
                }
                else
                {
                    //_logger.LogInformation("No se encontró entrada de bloqueo en la BD para IP '{IpAddressString}'.", ipString);
                    Console.WriteLine($"[INFO][DbIpBlockingService] No se encontró entrada de bloqueo en la BD para IP '{ipString}'.");
                }

                if (isCurrentlyBlocked)
                {
                    //_logger.LogWarning("IP '{IpAddressString}' está BLOQUEADA. Razón: {Reason}, Bloqueada hasta: {BlockedUntil}",ipString, blockedEntry?.Reason ?? "N/A", blockedEntry?.BlockedUntil?.ToString() ?? "Permanente");
                    Console.WriteLine($"[WARN][DbIpBlockingService] IP '{ipString}' está BLOQUEADA. Razón: {blockedEntry?.Reason ?? "N/A"}, Bloqueada hasta: {blockedEntry?.BlockedUntil?.ToString() ?? "Permanente"}");
                }
                else
                {
                    //_logger.LogInformation("IP '{IpAddressString}' no está bloqueada o el bloqueo ha expirado.", ipString);
                    Console.WriteLine($"[INFO][DbIpBlockingService] IP '{ipString}' no está bloqueada o el bloqueo ha expirado.");
                }

                _memoryCache.Set(cacheKey, isCurrentlyBlocked, _cacheDuration);
                //_logger.LogInformation("Resultado de bloqueo para IP '{IpAddressString}' guardado en caché: {IsCurrentlyBlocked}", ipString, isCurrentlyBlocked);
                Console.WriteLine($"[INFO][DbIpBlockingService] Resultado de bloqueo para IP '{ipString}' guardado en caché: {isCurrentlyBlocked}");

                return isCurrentlyBlocked;
            }
            catch (Exception ex)
            {
                //_logger.LogError(ex, "Error al verificar si la IP '{IpAddressString}' está bloqueada en la BD.", ipString);
                Console.WriteLine($"[ERROR][DbIpBlockingService] Error al verificar si la IP '{ipString}' está bloqueada en la BD: {ex.Message}");
                return false;
            }
        }
    }
}
