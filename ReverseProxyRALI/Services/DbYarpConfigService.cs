using Microsoft.EntityFrameworkCore;
using ReverseProxyRALI.Data; // Asume que aquí está tu DbContext
using ReverseProxyRALI.Data.Entities; // Asume que aquí están tus entidades
using Yarp.ReverseProxy.Configuration;
using System.Text.Json; // Para deserializar MetadataJson si es necesario
using Yarp.ReverseProxy.Health;
using Yarp.ReverseProxy.LoadBalancing; 

namespace ReverseProxyRALI.Services
{
    public class DbYarpConfigService : IYarpConfigService
    {
        private readonly IDbContextFactory<ProxyRaliDbContext> _dbContextFactory;
        private readonly ILogger<DbYarpConfigService> _logger;

        public DbYarpConfigService(IDbContextFactory<ProxyRaliDbContext> dbContextFactory, ILogger<DbYarpConfigService> logger)
        {
            _dbContextFactory = dbContextFactory;
            _logger = logger;
        }

        public async Task<(IReadOnlyList<RouteConfig> Routes, IReadOnlyList<ClusterConfig> Clusters)> GetConfigAsync()
        {
            //_logger.LogInformation("Cargando configuración de YARP desde la base de datos...");
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            var routes = new List<RouteConfig>();
            var clusters = new List<ClusterConfig>();

            try
            {
                var dbEndpointGroups = await dbContext.EndpointGroups
                    .Include(eg => eg.EndpointGroupDestinations)
                        .ThenInclude(egd => egd.Destination) // Asegúrate de incluir el Destino
                    .Where(eg => eg.EndpointGroupDestinations.Any(egd => egd.Destination.IsEnabled && egd.IsEnabledInGroup))
                    .ToListAsync();

                foreach (var group in dbEndpointGroups)
                {
                    var clusterId = group.GroupName; // Usar GroupName como ClusterId

                    var destinations = group.EndpointGroupDestinations
                        .Where(egd => egd.Destination.IsEnabled && egd.IsEnabledInGroup)
                        .ToDictionary(
                            egd => $"dest_{group.GroupName}_{egd.Destination.DestinationId}", // Nombre único para el destino dentro del clúster
                            egd => new DestinationConfig
                            {
                                Address = egd.Destination.Address,
                                Health = null, // Dejar null para que use la configuración de HealthCheck del clúster si existe
                                // Metadata = !string.IsNullOrEmpty(egd.Destination.MetadataJson) ? JsonSerializer.Deserialize<Dictionary<string, string>>(egd.Destination.MetadataJson) : null
                            }
                        );

                    if (!destinations.Any())
                    {
                        //_logger.LogWarning("EndpointGroup '{GroupName}' no tiene destinos habilitados y será omitido.", group.GroupName);
                        continue;
                    }

                    string? clusterHealthCheckPath = group.EndpointGroupDestinations
                                                       .Select(egd => egd.Destination.HealthCheckPath)
                                                       .FirstOrDefault(hcp => !string.IsNullOrEmpty(hcp));

                    HealthCheckConfig? healthCheckConfig = null;
                    if (!string.IsNullOrEmpty(clusterHealthCheckPath))
                    {
                        healthCheckConfig = new HealthCheckConfig
                        {
                            Active = new ActiveHealthCheckConfig
                            {
                                Enabled = true,
                                Path = clusterHealthCheckPath,
                                Policy = HealthCheckConstants.ActivePolicy.ConsecutiveFailures,
                                Interval = TimeSpan.FromSeconds(30),
                                Timeout = TimeSpan.FromSeconds(10)
                            }
                        };
                    }

                    var clusterConfig = new ClusterConfig
                    {
                        ClusterId = clusterId,
                        Destinations = destinations,
                        HealthCheck = healthCheckConfig,
                        LoadBalancingPolicy = LoadBalancingPolicies.PowerOfTwoChoices
                    };
                    clusters.Add(clusterConfig);

                    // Crear una Ruta para este Cluster API (ej. /api/GroupName/*)
                    // Solo crear rutas con prefijo /api para los grupos que no sean el "Default_Api_Group"
                    // o el grupo destinado a servir la UI de Swagger desde la raíz.
                    if (group.GroupName != "Default_Api_Group") // Evitar crear /api/Default_Api_Group/... si Default_Api_Group sirve la raíz
                    {
                        routes.Add(new RouteConfig
                        {
                            RouteId = $"route_api_for_{clusterId}",
                            ClusterId = clusterId,
                            Match = new RouteMatch
                            {
                                Path = $"/api/{group.GroupName}/{{**remainder}}"
                            },
                            Order = 0, // Rutas API específicas tienen mayor prioridad
                            Transforms = new List<IReadOnlyDictionary<string, string>>
                            {
                                new Dictionary<string, string> { { "RequestHeaderOriginalHost", "true" } },
                                new Dictionary<string, string> { { "X-Forwarded", "Append" } },
                                // Considerar si "Authorization" debe eliminarse aquí o globalmente
                                // new Dictionary<string, string> { { "RequestHeaderRemove", "Authorization" } }
                            }
                        });
                    }
                }

                // --- AÑADIR RUTAS ESPECÍFICAS PARA SWAGGER UI ---
                var mainApiClusterId = "Default_Api_Group"; // El GroupName que apunta a http://localhost:7586

                // Verificar si el clúster principal para Swagger/Root existe
                if (clusters.Any(c => c.ClusterId == mainApiClusterId))
                {
                    // Ruta para Swagger UI si está bajo /swagger/
                    routes.Add(new RouteConfig
                    {
                        RouteId = $"route_swagger_ui_path",
                        ClusterId = mainApiClusterId,
                        Match = new RouteMatch { Path = "/swagger/{**remainder}" },
                        Order = 100 // Prioridad media, después de rutas API específicas
                    });

                    // Ruta para la raíz (/) y /index.html, potencialmente para Swagger UI
                    // si RoutePrefix está vacío en el backend.
                    // Esta ruta es amplia, por lo que su orden es importante.
                    routes.Add(new RouteConfig
                    {
                        RouteId = $"route_root_path",
                        ClusterId = mainApiClusterId,
                        Match = new RouteMatch { Path = "/{**remainder}" }, // Captura todo lo demás
                        Order = 900 // Menor prioridad, se evalúa después de otras rutas más específicas
                    });
                    //_logger.LogInformation("Añadidas rutas para Swagger UI y Root apuntando al clúster '{MainApiClusterId}'.", mainApiClusterId);
                }
                else
                {
                    //_logger.LogWarning("El clúster '{MainApiClusterId}' no fue encontrado o no tiene destinos. Las rutas para Swagger UI y Root no serán añadidas.", mainApiClusterId);
                }

                //_logger.LogInformation("Configuración de YARP cargada desde la BD: {RouteCount} rutas, {ClusterCount} clusters.", routes.Count, clusters.Count);
            }
            catch (Exception ex)
            {
                //_logger.LogError(ex, "Error al cargar la configuración de YARP desde la base de datos.");
                return (new List<RouteConfig>(), new List<ClusterConfig>());
            }

            return (routes, clusters);
        }
    }
}
