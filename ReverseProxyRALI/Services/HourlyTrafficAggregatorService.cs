using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ReverseProxyRALI.Data;
using ReverseProxyRALI.Data.Entities;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ReverseProxyRALI.Services
{
    public class HourlyTrafficAggregatorService : IHostedService, IDisposable
    {
        private readonly ILogger<HourlyTrafficAggregatorService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private Timer? _timer;
        private readonly TimeSpan _aggregationInterval = TimeSpan.FromHours(1); // Ejecutar cada hora
        //private readonly TimeSpan _aggregationInterval = TimeSpan.FromMinutes(1); // Para pruebas

        public HourlyTrafficAggregatorService(
            ILogger<HourlyTrafficAggregatorService> logger,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            Console.WriteLine("[DEBUG][HourlyTrafficAggregatorService] Constructor llamado.");
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            //_logger.LogInformation("Servicio de Agregación de Tráfico Horario Iniciándose.");
            Console.WriteLine("[INFO][HourlyTrafficAggregatorService] Servicio de Agregación de Tráfico Horario Iniciándose.");

            var now = DateTime.UtcNow;
            // Para pruebas con intervalo de 1 minuto, ajustamos la primera ejecución para que sea más pronto.
            // Por ejemplo, al próximo minuto en punto.
            var nextRunTime = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, DateTimeKind.Local).AddMinutes(1);
            var dueTime = nextRunTime - now;

            if (dueTime <= TimeSpan.Zero)
            {
                // Si ya pasó el minuto en punto, o es negativo, programar para el siguiente intervalo desde ahora.
                dueTime = _aggregationInterval;
                nextRunTime = now.Add(_aggregationInterval); // Ajustar nextRunTime para el log
            }

            // Si el intervalo es de 1 hora, la lógica original de dueTime es mejor:
            // var nextRunTimeOriginal = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc).AddHours(1);
            // var dueTimeOriginal = nextRunTimeOriginal - now;
            // if (dueTimeOriginal <= TimeSpan.Zero) 
            // {
            //     dueTimeOriginal = dueTimeOriginal.Add(_aggregationInterval);
            // }
            // //_logger.LogInformation("Primera ejecución de agregación programada para: {NextRunTimeUtc} (en {DueTime})", nextRunTimeOriginal, dueTimeOriginal);
            // Console.WriteLine($"[INFO][HourlyTrafficAggregatorService] Primera ejecución de agregación programada para: {nextRunTimeOriginal} UTC (en {dueTimeOriginal})");
            // _timer = new Timer(DoWork, null, dueTimeOriginal, _aggregationInterval);


            //_logger.LogInformation("Primera ejecución de agregación programada para: {NextRunTimeUtc} (en {DueTime})", nextRunTime, dueTime);
            Console.WriteLine($"[INFO][HourlyTrafficAggregatorService] Primera ejecución de agregación programada para: {nextRunTime} UTC (en {dueTime})");
            _timer = new Timer(DoWork, null, dueTime, _aggregationInterval);

            Console.WriteLine($"[DEBUG][HourlyTrafficAggregatorService] Timer configurado. DueTime: {dueTime}, Interval: {_aggregationInterval}");

            return Task.CompletedTask;
        }

        private async void DoWork(object? state)
        {
            //_logger.LogInformation("Servicio de Agregación de Tráfico Horario ejecutando trabajo a las: {Time}", DateTime.UtcNow);
            Console.WriteLine($"[INFO][HourlyTrafficAggregatorService] DoWork ejecutando trabajo a las: {DateTime.UtcNow} UTC");

            using (var scope = _scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ProxyRaliDbContext>();
                Console.WriteLine("[DEBUG][HourlyTrafficAggregatorService] DbContext obtenido del scope.");

                try
                {
                    var now = DateTime.UtcNow;
                    DateTime processingHourEnd;
                    DateTime processingHourStart;

                    if (_aggregationInterval == TimeSpan.FromHours(1))
                    {
                        // Lógica para intervalo horario
                        processingHourEnd = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc);
                        processingHourStart = processingHourEnd.AddHours(-1);
                    }
                    else if (_aggregationInterval == TimeSpan.FromMinutes(1))
                    {
                        // Lógica para intervalo de 1 minuto (procesar el minuto anterior completo)
                        processingHourEnd = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, DateTimeKind.Utc);
                        processingHourStart = processingHourEnd.AddMinutes(-1);
                    }
                    else
                    {
                        // Lógica por defecto o para otros intervalos (ajustar según necesidad)
                        processingHourEnd = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc);
                        processingHourStart = processingHourEnd.Add(-_aggregationInterval); // Procesar el intervalo anterior
                        //_logger.LogWarning("Intervalo de agregación no estándar. Procesando desde {ProcessingHourStart} hasta {ProcessingHourEnd}", processingHourStart, processingHourEnd);
                        Console.WriteLine($"[WARN][HourlyTrafficAggregatorService] Intervalo de agregación no estándar. Procesando desde {processingHourStart} hasta {processingHourEnd}");
                    }


                    //_logger.LogInformation("Procesando logs para el intervalo: {ProcessingHourStart} a {ProcessingHourEnd}", processingHourStart, processingHourEnd);
                    Console.WriteLine($"[INFO][HourlyTrafficAggregatorService] Procesando logs para el intervalo: {processingHourStart} UTC a {processingHourEnd} UTC");

                    bool alreadyProcessed = await dbContext.HourlyTrafficSummaries
                                                .AnyAsync(s => s.HourUtc == processingHourStart);
                    Console.WriteLine($"[DEBUG][HourlyTrafficAggregatorService] ¿Intervalo {processingHourStart} ya procesado?: {alreadyProcessed}");

                    if (alreadyProcessed && !IsDevelopmentEnvironment())
                    {
                        //_logger.LogInformation("El intervalo {ProcessingHourStart} ya ha sido procesado. Saltando.", processingHourStart);
                        Console.WriteLine($"[INFO][HourlyTrafficAggregatorService] El intervalo {processingHourStart} ya ha sido procesado. Saltando.");
                        return;
                    }
                    if (alreadyProcessed && IsDevelopmentEnvironment())
                    {
                        //_logger.LogInformation("Modo desarrollo: Eliminando datos antiguos para {ProcessingHourStart} antes de reprocesar.", processingHourStart);
                        Console.WriteLine($"[INFO][HourlyTrafficAggregatorService] Modo desarrollo: Eliminando datos antiguos para {processingHourStart} antes de reprocesar.");
                        await dbContext.HourlyTrafficSummaries.Where(s => s.HourUtc == processingHourStart).ExecuteDeleteAsync();
                        Console.WriteLine($"[DEBUG][HourlyTrafficAggregatorService] Datos antiguos para {processingHourStart} eliminados.");
                    }

                    Console.WriteLine($"[DEBUG][HourlyTrafficAggregatorService] Consultando RequestLogs para el intervalo.");
                    var hourlyAggregates = await dbContext.RequestLogs
                        .Where(rl => rl.TimestampUtc >= processingHourStart && rl.TimestampUtc < processingHourEnd && rl.EndpointGroupAccessed != null)
                        .GroupBy(rl => new { Hour = processingHourStart, rl.EndpointGroupAccessed, rl.HttpMethod })
                        .Select(g => new HourlyTrafficSummary
                        {
                            HourUtc = DateTime.Now,
                            EndpointGroupId = dbContext.EndpointGroups
                                                .Where(eg => eg.GroupName == g.Key.EndpointGroupAccessed)
                                                .Select(eg => eg.GroupId)
                                                .FirstOrDefault(),
                            HttpMethod = g.Key.HttpMethod,
                            RequestCount = g.Count(),
                            ErrorCount4xx = g.Count(rl => rl.ResponseStatusCode >= 400 && rl.ResponseStatusCode < 500),
                            ErrorCount5xx = g.Count(rl => rl.ResponseStatusCode >= 500),
                            AverageDurationMs = (decimal?)g.Average(rl => rl.DurationMs),
                            TotalRequestBytes = g.Sum(rl => rl.RequestSizeBytes),
                            TotalResponseBytes = g.Sum(rl => rl.ResponseSizeBytes),
                            UniqueClientIps = g.Select(rl => rl.ClientIpAddress).Distinct().Count()
                        })
                        .Where(s => s.EndpointGroupId != 0)
                        .ToListAsync();
                    Console.WriteLine($"[DEBUG][HourlyTrafficAggregatorService] Agregados preliminares obtenidos: {hourlyAggregates.Count} registros.");

                    if (hourlyAggregates.Any())
                    {
                        Console.WriteLine($"[DEBUG][HourlyTrafficAggregatorService] Calculando P95 para {hourlyAggregates.Count} agregados.");
                        foreach (var aggregate in hourlyAggregates)
                        {
                            var groupNameForP95 = await dbContext.EndpointGroups
                                .Where(eg => eg.GroupId == aggregate.EndpointGroupId)
                                .Select(eg => eg.GroupName)
                                .FirstOrDefaultAsync();

                            if (string.IsNullOrEmpty(groupNameForP95))
                            {
                                //_logger.LogWarning("No se pudo encontrar GroupName para GroupId {GroupId} al calcular P95. Saltando P95 para este agregado.", aggregate.EndpointGroupId);
                                Console.WriteLine($"[WARN][HourlyTrafficAggregatorService] No se pudo encontrar GroupName para GroupId {aggregate.EndpointGroupId} al calcular P95. Saltando P95 para este agregado.");
                                continue;
                            }

                            var durations = await dbContext.RequestLogs
                                .Where(rl => rl.TimestampUtc >= processingHourStart && rl.TimestampUtc < processingHourEnd &&
                                             rl.EndpointGroupAccessed == groupNameForP95 &&
                                             rl.HttpMethod == aggregate.HttpMethod)
                                .Select(rl => rl.DurationMs)
                                .OrderBy(d => d)
                                .ToListAsync();
                            if (durations.Any())
                            {
                                int indexP95 = (int)Math.Ceiling(0.95 * durations.Count) - 1;
                                if (indexP95 >= 0 && indexP95 < durations.Count)
                                {
                                    aggregate.P95durationMs = durations[indexP95]; // Corregido: P95DurationMs
                                }
                            }
                            Console.WriteLine($"[DEBUG][HourlyTrafficAggregatorService] P95 calculado para GrupoId {aggregate.EndpointGroupId}, Método {aggregate.HttpMethod}: {aggregate.P95durationMs}");
                        }

                        dbContext.HourlyTrafficSummaries.AddRange(hourlyAggregates);
                        await dbContext.SaveChangesAsync();
                        //_logger.LogInformation("Agregados {Count} registros a HourlyTrafficSummary para la hora {ProcessingHourStart}.", hourlyAggregates.Count, processingHourStart);
                        Console.WriteLine($"[INFO][HourlyTrafficAggregatorService] Agregados {hourlyAggregates.Count} registros a HourlyTrafficSummary para la hora {processingHourStart}.");
                    }
                    else
                    {
                        //_logger.LogInformation("No hay datos para agregar en HourlyTrafficSummary para la hora {ProcessingHourStart}.", processingHourStart);
                        Console.WriteLine($"[INFO][HourlyTrafficAggregatorService] No hay datos para agregar en HourlyTrafficSummary para la hora {processingHourStart}.");
                    }
                }
                catch (Exception ex)
                {
                    //_logger.LogError(ex, "Error durante la ejecución del Servicio de Agregación de Tráfico Horario.");
                    Console.WriteLine($"[ERROR][HourlyTrafficAggregatorService] Error durante la ejecución de DoWork: {ex.ToString()}");
                }
            }
            Console.WriteLine($"[DEBUG][HourlyTrafficAggregatorService] DoWork finalizado para el estado: {state}");
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            //_logger.LogInformation("Servicio de Agregación de Tráfico Horario Deteniéndose.");
            Console.WriteLine("[INFO][HourlyTrafficAggregatorService] Servicio de Agregación de Tráfico Horario Deteniéndose.");
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
            GC.SuppressFinalize(this);
            Console.WriteLine("[DEBUG][HourlyTrafficAggregatorService] Dispose llamado.");
        }

        private bool IsDevelopmentEnvironment()
        {
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            bool isDev = string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase);
            Console.WriteLine($"[DEBUG][HourlyTrafficAggregatorService] IsDevelopmentEnvironment: {isDev}");
            return isDev;
        }
    }
}