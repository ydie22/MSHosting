using System;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Health.V1;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ProtoBuf.Grpc;
using Version = Grpc.Health.V1.Version;

namespace ConsoleApp.Grpc.HealthCheck
{
    /// <summary>
    ///     This class is an implementation of the <see cref="IHealth" /> service contract.
    ///     It internally delegates to the collection of the <see cref="IHealthCheck" />
    ///     objects passed as parameter in its constructor.
    /// </summary>
    /// <typeparam name="TService">
    ///     The type of the service contract this <see cref="IHealth" />
    ///     endpoint relates to.
    /// </typeparam>
    public class HealthCheckService<TService> : IHealth where TService : class
    {
        private readonly HealthCheckService _inner;

        /// <summary>
        /// </summary>
        /// <param name="inner"></param>
        public HealthCheckService(HealthCheckService inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public async Task<HealthCheckResponse> Check(HealthCheckRequest request, CallContext context)
        {
            var report = await _inner.CheckHealthAsync(context.CancellationToken);
            var isEverythingHealthy = report.Status != HealthStatus.Unhealthy;

            var response = new HealthCheckResponse
            {
                Status = isEverythingHealthy
                    ? HealthCheckResponse.ServingStatus.Serving
                    : HealthCheckResponse.ServingStatus.NotServing
            };
            response.Diagnostics.AddRange(report.Entries.Select(e => new CheckInfo
            {
                CheckName = e.Key, IsSuccessful = e.Value.Status != HealthStatus.Unhealthy,
                DiagnosticInfo = e.Value.Exception.ToString()
            }));
            response.ServiceVersion = GetServiceVersion();
            return response;
        }

        public Task<PingResponse> Ping(PingRequest request, CallContext context)
        {
            return Task.FromResult(new PingResponse {ServiceVersion = GetServiceVersion()});
        }

        private static Version GetServiceVersion()
        {
            var serviceAssembly = typeof(TService).Assembly;
            var assemblyVersion = serviceAssembly.GetName().Version;
            var version = new Version
            {
                Major = assemblyVersion.Major,
                Minor = assemblyVersion.Minor,
                Patch = assemblyVersion.Build
            };
            return version;
        }
    }
}