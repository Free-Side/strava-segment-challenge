using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SegmentChallengeWeb.Configuration;

namespace SegmentChallengeWeb {
    public class StravaApiHelper {
        private static readonly DateTime
            Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static readonly Queue<DateTime> apiRequestHistory = new Queue<DateTime>();
        private static readonly SemaphoreSlim apiRequestMutex = new SemaphoreSlim(1);
        private static DateTime lastApiRequest;

        private readonly ILogger<StravaApiHelper> logger;
        private readonly IOptions<StravaConfiguration> configuration;

        public StravaApiHelper(
            IOptions<StravaConfiguration> configuration,
            ILogger<StravaApiHelper> logger) {

            this.configuration = configuration;
            this.logger = logger;
        }

        public async Task<TResult> MakeThrottledApiRequest<TResult>(
            Func<Task<TResult>> apiRequest,
            CancellationToken cancellationToken) {

            TResult result;

            await apiRequestMutex.WaitAsync(cancellationToken);
            try {
                var secondsSinceLastCall = DateTime.UtcNow.Subtract(lastApiRequest).TotalSeconds;
                if (secondsSinceLastCall < 0.5) {
                    // Don't make more than two calls per second
                    await Task.Delay(TimeSpan.FromSeconds(0.5 - secondsSinceLastCall), cancellationToken);
                }

                // While there have been too many requests in the last 15 minutes, wait just long enough
                while (apiRequestHistory.Count >= this.configuration.Value.MaximumRequestsPer15Minutes &&
                    apiRequestHistory.TryPeek(out var time)) {
                    var delay = TimeSpan.FromMinutes(15).Subtract(DateTime.UtcNow.Subtract(time));
                    if (delay > TimeSpan.Zero) {
                        this.logger.LogInformation(
                            "Throttling Strava Api Access. Sleeping for {Delay}",
                            delay
                        );

                        await Task.Delay(delay, cancellationToken);
                    }

                    // We should now be able to clear out some of the older requests.
                    while (apiRequestHistory.TryPeek(out time) &&
                        time < DateTime.UtcNow.AddMinutes(-15)) {
                        apiRequestHistory.Dequeue();
                    }

                    if (cancellationToken.IsCancellationRequested) {
                        throw new TaskCanceledException();
                    }
                }

                // Only one API request can happen at a time.
                result = await apiRequest();

                // Record the time at which the request finished;
                apiRequestHistory.Enqueue(lastApiRequest = DateTime.UtcNow);
            } finally {
                apiRequestMutex.Release();
            }

            return result;
        }

        public static DateTime DateTimeFromUnixTime(Int64 unixTime) {
            return Epoch.AddSeconds(unixTime);
        }
    }
}
