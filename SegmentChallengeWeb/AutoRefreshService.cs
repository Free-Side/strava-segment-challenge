using System;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SegmentChallengeWeb.Models;
using SegmentChallengeWeb.Persistence;

namespace SegmentChallengeWeb {
    public class AutoRefreshService : IHostedService {
        private readonly Func<DbConnection> dbConnectionFactory;
        private readonly BackgroundTaskService taskService;
        private readonly ILogger<AutoRefreshService> logger;
        private Timer autoRefreshTimer;

        public AutoRefreshService(
            Func<DbConnection> dbConnectionFactory,
            BackgroundTaskService taskService,
            ILogger<AutoRefreshService> logger) {

            this.dbConnectionFactory = dbConnectionFactory;
            this.taskService = taskService;
            this.logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken) {
            this.logger.LogDebug("AutoRefreshService started!");
            // TODO: Configuration
            this.autoRefreshTimer =
                new Timer(_ => this.RefreshAllChallenges(), null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30));
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken) {
            await this.autoRefreshTimer.DisposeAsync();
            this.autoRefreshTimer = null;
        }

        private void RefreshAllChallenges() {
            try {
                var startRefreshTask = this.RefreshAllChallengesAsync(CancellationToken.None);
                startRefreshTask.ConfigureAwait(continueOnCapturedContext: false).GetAwaiter().GetResult();
            } catch (Exception err) {
                this.logger.LogError(-1, err, err.Message);
            }
        }

        private async Task RefreshAllChallengesAsync(CancellationToken cancellationToken) {
            logger.LogDebug("Refreshing all active challenges");
            await using var connection = this.dbConnectionFactory();
            await connection.OpenAsync(cancellationToken);

            await using var dbContext = new SegmentChallengeDbContext(connection);
            var updatesTable = dbContext.Set<Update>();

            var pendingUpdates = await
                updatesTable
                    .Where(u => u.EndTime == null && u.AthleteId == null)
                    .CountAsync(cancellationToken);

            if (pendingUpdates > 0) {
                // Ruh-roh
                this.logger.LogWarning(
                    "Unable to start auto refresh process because there is an unfinished update already running."
                );
                return;
            }

            this.taskService.QueueTask<EffortRefresher>(
                (service, taskCancellationToken) => service.RefreshAllChallenges(taskCancellationToken)
            );
        }
    }
}
