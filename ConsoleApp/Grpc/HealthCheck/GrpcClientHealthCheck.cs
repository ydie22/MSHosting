using System;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Health.V1;
using Grpc.Net.Client;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ProtoBuf.Grpc.Client;
using Version = System.Version;

namespace ConsoleApp.Grpc.HealthCheck
{
    /// <summary>
    /// </summary>
    /// <typeparam name="TService">
    ///     The contract type of the service for which the <see cref="IHealth" /> endpoint
    ///     is provided.
    /// </typeparam>
    public class GrpcClientHealthCheck<TService> : IHealthCheck where TService : class
    {
        private readonly GrpcChannel _channel;

        public GrpcClientHealthCheck(GrpcChannel channel)
        {
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
            CancellationToken cancellationToken = new CancellationToken())
        {
            try
            {
                var healthClient = _channel.CreateGrpcService<IHealth>();
                var pingResponse = await healthClient.Ping(new PingRequest(), cancellationToken);
                if (pingResponse.ServiceVersion != null)
                {
                    var requiredContractVersion = typeof(TService).Assembly.GetName().Version;
                    var pingedImplementedVersion = new Version(pingResponse.ServiceVersion.Major,
                        pingResponse.ServiceVersion.Minor, pingResponse.ServiceVersion.Patch, 0);
                    if (requiredContractVersion > pingedImplementedVersion)
                        throw new InvalidMessageContractException(
                            $"The minimum required contract version {requiredContractVersion} was not found at the remote endpoint. Found contract version {pingedImplementedVersion}.");
                    if (pingedImplementedVersion.Major > requiredContractVersion.Major)
                        throw new InvalidMessageContractException(
                            $"An incompatible version of the service contract was found at the remote endpoint. Found contract version {pingedImplementedVersion}, required contract version {requiredContractVersion}.");
                }
            }
            catch (InvalidMessageContractException contractException)
            {
                return HealthCheckResult.Unhealthy("Invalid dependency contract version detected", contractException);
            }
            catch (Exception exception)
            {
                return HealthCheckResult.Unhealthy("Exception while pinging dependency", exception);
            }

            return HealthCheckResult.Healthy("GRPC dependency pinged successfully");
        }
    }
}