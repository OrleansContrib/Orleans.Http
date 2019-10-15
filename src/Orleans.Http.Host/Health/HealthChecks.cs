using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Orleans.Concurrency;
using Orleans.Configuration;
using Orleans.Runtime;

namespace Orleans.Http.Host.Health
{
    public static class HealthChecks
    {
        public interface ILocalHealthCheckGrain : IGrainWithGuidKey
        {
            Task Ping();
        }

        [StatelessWorker]
        [CollectionAgeLimit(Minutes = 2)]
        // ReSharper disable once UnusedMember.Global
        public class LocalHealthCheckGrain : Grain, ILocalHealthCheckGrain
        {
            public Task Ping() => Task.CompletedTask;
        }

        public class GrainHealthCheck : IHealthCheck
        {
            private readonly IClusterClient client;

            public GrainHealthCheck(IClusterClient client)
            {
                this.client = client;
            }

            public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new CancellationToken())
            {
                try
                {
                    var localGrain = this.client.GetGrain<ILocalHealthCheckGrain>(Guid.Empty);
                    await localGrain.Ping();
                }
                catch (Exception exception)
                {
                    return HealthCheckResult.Unhealthy("Unable to ping local health check grain", exception);
                }

                return HealthCheckResult.Healthy();
            }
        }

        public class SiloHealthCheck : IHealthCheck
        {
            private DateTime lastCheckTime;
            private readonly List<IHealthCheckParticipant> participants;

            public SiloHealthCheck(IEnumerable<IHealthCheckParticipant> participants)
            {
                // TODO: put IHealthCheckParticipants into DI container
                this.participants = participants.ToList();
            }

            public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new CancellationToken())
            {
                var lastChecked = this.lastCheckTime;
                this.lastCheckTime = DateTime.UtcNow;
                foreach (var participant in this.participants)
                {
                    try
                    {
                        if (!participant.CheckHealth(lastChecked))
                        {
                            return Task.FromResult(HealthCheckResult.Degraded());
                        }
                    }
                    catch (Exception exception)
                    {
                        return Task.FromResult(
                            HealthCheckResult.Unhealthy(
                                $"Exception while checking health of component {participant.GetType()}: {exception.Message}",
                                exception));
                    }
                }

                return Task.FromResult(HealthCheckResult.Healthy());
            }
        }
    }
}
