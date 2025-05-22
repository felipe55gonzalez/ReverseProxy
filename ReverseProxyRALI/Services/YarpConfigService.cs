using Yarp.ReverseProxy.Configuration;

namespace ReverseProxyRALI.Services
{
    public interface IYarpConfigService
    {
        Task<(IReadOnlyList<RouteConfig> Routes, IReadOnlyList<ClusterConfig> Clusters)> GetConfigAsync();
    }
}
