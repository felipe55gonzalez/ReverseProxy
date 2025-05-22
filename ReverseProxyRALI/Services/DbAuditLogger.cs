// Services/DbAuditLogger.cs
using Microsoft.EntityFrameworkCore;
using ReverseProxyRALI.Data; // Para ProxyRaliDbContext
using ReverseProxyRALI.Data.Entities; // Para AuditLog
using System.Text.Json; // Para serializar a JSON

namespace ReverseProxyRALI.Services
{
    public class DbAuditLogger : IAuditLogger
    {
        private readonly IDbContextFactory<ProxyRaliDbContext> _dbContextFactory;
        private readonly ILogger<DbAuditLogger> _logger;

        public DbAuditLogger(IDbContextFactory<ProxyRaliDbContext> dbContextFactory, ILogger<DbAuditLogger> logger)
        {
            _dbContextFactory = dbContextFactory;
            _logger = logger;
        }

        public async Task LogEventAsync(
            string entityType,
            string entityId,
            string action,
            string? userId = null,
            string? affectedComponent = null,
            object? oldValues = null,
            object? newValues = null,
            string? clientIpAddress = null)
        {
            try
            {
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

                var auditLogEntry = new AuditLog
                {
                    TimestampUtc = DateTime.Now,
                    UserId = userId,
                    EntityType = entityType,
                    EntityId = entityId,
                    Action = action,
                    OldValues = oldValues != null ? JsonSerializer.Serialize(oldValues, new JsonSerializerOptions { WriteIndented = false }) : null,
                    NewValues = newValues != null ? JsonSerializer.Serialize(newValues, new JsonSerializerOptions { WriteIndented = false }) : null,
                    AffectedComponent = affectedComponent,
                    IpAddress = clientIpAddress
                };

                dbContext.AuditLogs.Add(auditLogEntry);
                await dbContext.SaveChangesAsync();
                //_logger.LogInformation("Evento de auditoría registrado: {Action} en {EntityType} ID {EntityId}", action, entityType, entityId);
            }
            catch (Exception ex)
            {
               // _logger.LogError(ex, "Error al registrar evento de auditoría para {EntityType} ID {EntityId}, Acción {Action}", entityType, entityId, action);
                // Decidir si relanzar la excepción o solo loguearla.
                // Por ahora, solo la logueamos para no interrumpir el flujo principal.
            }
        }
    }
}