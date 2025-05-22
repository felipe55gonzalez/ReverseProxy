using Microsoft.EntityFrameworkCore;
using ReverseProxyRALI.Data.Entities; // Para tus entidades
using ReverseProxyRALI.Models;
namespace ReverseProxyRALI.Services
{
    public class DbTokenService : ITokenService
    {
        private readonly IDbContextFactory<ProxyRaliDbContext> _dbContextFactory;
        private readonly ILogger<DbTokenService> _logger;

        public DbTokenService(IDbContextFactory<ProxyRaliDbContext> dbContextFactory, ILogger<DbTokenService> logger)
        {
            _dbContextFactory = dbContextFactory;
            _logger = logger;
        }

        public async Task<(bool IsValid, TokenDefinition? TokenDetails)> ValidateTokenAsync(string tokenValue, string requiredEndpointGroup)
        {
            if (string.IsNullOrEmpty(tokenValue) || string.IsNullOrEmpty(requiredEndpointGroup))
            {
                return (false, null);
            }

            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            try
            {
                var apiToken = await dbContext.ApiTokens
                    .Include(t => t.TokenPermissions)
                        .ThenInclude(tp => tp.Group)
                    .FirstOrDefaultAsync(t => t.TokenValue == tokenValue);

                if (apiToken == null)
                {
                    //_logger.LogWarning("Token no encontrado en la base de datos: {TokenValue}", tokenValue);
                    return (false, null);
                }

                if (!apiToken.IsEnabled)
                {
                    //_logger.LogWarning("Token '{TokenValue}' está deshabilitado.", tokenValue);
                    return (false, null);
                }

                if (apiToken.DoesExpire && apiToken.ExpiresAt.HasValue && apiToken.ExpiresAt.Value < DateTime.UtcNow)
                {
                    //_logger.LogWarning("Token '{TokenValue}' ha expirado el {ExpiryDate}.", tokenValue, apiToken.ExpiresAt.Value);
                    return (false, null);
                }

                var permission = apiToken.TokenPermissions
                    .FirstOrDefault(tp => tp.Group != null && tp.Group.GroupName == requiredEndpointGroup);

                if (permission == null)
                {
                    //_logger.LogWarning("Token '{TokenValue}' no tiene permisos asignados para el grupo de endpoints '{RequiredEndpointGroup}'.", tokenValue, requiredEndpointGroup);
                    return (false, null);
                }

                // La validación del método HTTP se hará en el middleware después de obtener los métodos permitidos.
                // Aquí solo validamos que el token tiene *algún* permiso para el grupo.
                // Podrías devolver los AllowedHttpMethods aquí si el ITokenService lo requiriera.

                // Crear un TokenDefinition (modelo) para devolver detalles si es necesario por el middleware
                var tokenDetailsModel = new Models.TokenDefinition(apiToken.TokenValue,
                    apiToken.TokenPermissions.Select(tp => tp.Group?.GroupName ?? string.Empty).ToList())
                {
                    IsActive = apiToken.IsEnabled,
                    ExpiryDate = apiToken.ExpiresAt ?? DateTime.MaxValue // O manejar null apropiadamente
                };

                // Actualizar LastUsedAt (opcional, pero útil)
                apiToken.LastUsedAt = DateTime.Now;
                await dbContext.SaveChangesAsync();


                return (true, tokenDetailsModel);
            }
            catch (Exception ex)
            {
                //_logger.LogError(ex, "Error validando token '{TokenValue}' para el grupo '{RequiredEndpointGroup}'.", tokenValue, requiredEndpointGroup);
                return (false, null);
            }
        }
    }
}
