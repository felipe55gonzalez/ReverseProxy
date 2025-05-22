using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;

namespace ReverseProxyRALI.Services // O ReverseProxyRALI.Yarp
{
    public class DbProxyConfigProvider : IProxyConfigProvider
    {
        private readonly IYarpConfigService _configService;
        private DateTime _lastLoadTime = DateTime.MinValue;
        private IProxyConfig? _currentConfig;
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5); // Cache por 5 minutos

        public DbProxyConfigProvider(IYarpConfigService configService)
        {
            _configService = configService;
        }

        public IProxyConfig GetConfig()
        {
            // Implementación simple de cacheo para no ir a la BD en cada solicitud
            // Para una recarga dinámica real sin reinicio, se necesita IProxyConfigManager
            if (_currentConfig == null || DateTime.UtcNow - _lastLoadTime > _cacheExpiration)
            {
                var (routes, clusters) = _configService.GetConfigAsync().GetAwaiter().GetResult();
                _currentConfig = new InMemoryConfig(routes, clusters);
                _lastLoadTime = DateTime.UtcNow;
            }
            return _currentConfig ?? new InMemoryConfig(new List<RouteConfig>(), new List<ClusterConfig>());
        }

        // Clase interna para IProxyConfig
        private class InMemoryConfig : IProxyConfig
        {
            public IReadOnlyList<RouteConfig> Routes { get; }
            public IReadOnlyList<ClusterConfig> Clusters { get; }
            public string RevisionId { get; } = Guid.NewGuid().ToString(); // Nuevo ID para cada recarga
            public IChangeToken ChangeToken { get; } = new CancellationChangeToken(CancellationToken.None); // No soporta recarga dinámica en esta implementación simple

            public InMemoryConfig(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters)
            {
                Routes = routes;
                Clusters = clusters;
            }
        }
    }
}
