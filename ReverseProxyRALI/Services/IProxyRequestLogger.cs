// Services/IProxyRequestLogger.cs
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http; // Para HttpContext

namespace ReverseProxyRALI.Services
{
    public interface IProxyRequestLogger
    {
        Task LogRequestAsync(HttpContext context, Func<Task> next);
    }
}