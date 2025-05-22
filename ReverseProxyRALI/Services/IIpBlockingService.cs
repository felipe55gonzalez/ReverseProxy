// Services/IIpBlockingService.cs
using System.Net; // Para IPAddress
using System.Threading.Tasks;

namespace ReverseProxyRALI.Services
{
    public interface IIpBlockingService
    {
        Task<bool> IsIpBlockedAsync(IPAddress ipAddress);
        // Podrías añadir métodos para bloquear/desbloquear IPs aquí si el proxy lo gestionara,
        // pero usualmente esto se haría a través de una interfaz de administración separada.
    }
}