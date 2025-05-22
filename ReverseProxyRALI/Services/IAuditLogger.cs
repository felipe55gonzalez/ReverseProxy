// Services/IAuditLogger.cs
using System.Threading.Tasks;

namespace ReverseProxyRALI.Services
{
    public interface IAuditLogger
    {
        Task LogEventAsync(
            string entityType,
            string entityId,
            string action,
            string? userId = null,
            string? affectedComponent = null,
            object? oldValues = null,
            object? newValues = null,
            string? clientIpAddress = null
        );
    }
}