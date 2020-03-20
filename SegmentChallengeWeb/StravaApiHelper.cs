using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SegmentChallengeWeb {
    public class StravaApiHelper {
        private static readonly DateTime
            Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static readonly Queue<DateTime> apiRequestHistory = new Queue<DateTime>();
        private static readonly SemaphoreSlim apiRequestMutex = new SemaphoreSlim(1);

        private readonly ILogger<StravaApiHelper> logger;

        public StravaApiHelper(ILogger<StravaApiHelper> logger) {
            this.logger = logger;
        }

        public async Task<TResult> MakeThrottledApiRequest<TResult>(
            Func<Task<TResult>> apiRequest,
            CancellationToken cancellationToken) {
            await apiRequestMutex.WaitAsync(cancellationToken);
            try {
                // While there have been too many requests in the last 15 minutes, wait just long enough
                while (apiRequestHistory.Count >= 500 &&
                    apiRequestHistory.TryPeek(out var time) &&
                    time > DateTime.UtcNow) {
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

                apiRequestHistory.Enqueue(DateTime.UtcNow);
            } finally {
                apiRequestMutex.Release();
            }

            return await apiRequest();
        }

        public static DateTime DateTimeFromUnixTime(Int64 unixTime) {
            return Epoch.AddSeconds(unixTime);
        }
    }
}
