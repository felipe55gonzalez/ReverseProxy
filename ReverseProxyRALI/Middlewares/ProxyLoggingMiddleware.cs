
using ReverseProxyRALI.Services; 

namespace ReverseProxyRALI.Middlewares
{
    public class ProxyLoggingMiddleware
    {
        private readonly RequestDelegate _next;
 

        public ProxyLoggingMiddleware(RequestDelegate next) 
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, IProxyRequestLogger requestLogger)
        {
            await requestLogger.LogRequestAsync(context, () => _next(context));
        }
    }
}