using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SegmentChallengeWeb {
    /// <summary>
    /// An <see cref="IHostedService" /> implementation that tracks running background tasks,
    /// cancels them when the hosting application is shutting down, and waits for them to complete.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Queued tasks will be started immediately without any limitation on parallelism.
    /// </para>
    /// </remarks>
    public class BackgroundTaskService : IHostedService {
        private IServiceProvider serviceProvider { get; }
        private IHttpContextAccessor httpContextAccessor { get; }

        private ILogger<BackgroundTaskService> logger { get; }

        private Dictionary<Guid, (IServiceScope scope, Task task)> runningTasks { get; } =
            new Dictionary<Guid, (IServiceScope scope, Task task)>();
        private Object taskStartLock { get; } = new Object();

        private CancellationTokenSource cancellationTokenSource { get; } =
            new CancellationTokenSource();

        public BackgroundTaskService(
            IServiceProvider serviceProvider,
            IHttpContextAccessor httpContextAccessor,
            ILogger<BackgroundTaskService> logger) {

            if (serviceProvider == null) {
                throw new ArgumentNullException(nameof(serviceProvider));
            } else if (httpContextAccessor == null) {
                throw new ArgumentNullException(nameof(httpContextAccessor));
            } else if (logger == null) {
                throw new ArgumentNullException(nameof(logger));
            }

            this.serviceProvider = serviceProvider;
            this.httpContextAccessor = httpContextAccessor;
            this.logger = logger;
        }

        /// <summary>
        /// Starts a background task and returns immediately.
        /// </summary>
        /// <remarks>
        /// The reason the <paramref name="taskFactory" /> takes a
        /// <typeparamref name="TTaskService" /> is because a new dependency injection scope is
        /// created for the background task, so any work should be executed within that injected
        /// <typeparamref name="TTaskService" />. The reason for this is that the
        /// <see cref="Task" /> may continue to execute after the scope associated with the current
        /// request is disposed. If the <see cref="Task" /> implementation uses a closure to access
        /// resources that where part of the original dependency injection scope from the request,
        /// it will likely cause unexpected behavior.
        /// </remarks>
        /// <param name="taskFactory">
        /// A function that takes a service instance and cancellation token, and returns a
        /// <see cref="Task" /> to be executed in the background.
        /// </param>
        /// <typeparam name="TTaskService">
        /// A type of service to be retrieved from the dependency injection provider and passed to
        /// the task factory.
        /// </typeparam>
        public void QueueTask<TTaskService>(
            Func<TTaskService, CancellationToken, Task> taskFactory) {

            this.logger.LogTrace(
                $"Queueing Task {typeof(TTaskService).Name}"
            );

            var context = this.httpContextAccessor.HttpContext;

            lock (taskStartLock) {
                var taskId = Guid.NewGuid();

                var taskTypeName = typeof(TTaskService).Name;

                this.logger.LogTrace(
                    $"Creating Task {taskTypeName} ({taskId})"
                );

                try {
                    var scope = this.serviceProvider.CreateScope();

                    // Inject the HttpContext from the scope that Queued the task
                    var scopedHttpContextAccessor =
                        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
                    scopedHttpContextAccessor.HttpContext = new HttpContextWrapper(context);

                    var service = scope.ServiceProvider.GetRequiredService<TTaskService>();

                    var task = taskFactory(service, this.cancellationTokenSource.Token);
                    if (task.Status == TaskStatus.Created) {
                        this.logger.LogTrace(
                            $"Starting Task {taskTypeName} ({taskId})"
                        );
                        task.Start();
                    }

                    this.runningTasks.Add(
                        taskId,
                        (scope,
                            task.ContinueWith(
                                resultTask => {
                                    if (resultTask.IsCompletedSuccessfully) {
                                        this.logger.LogTrace(
                                            $"Task Completed Successfully: {taskTypeName} ({taskId})"
                                        );
                                    } else if (resultTask.Exception != null) {
                                        var ex = resultTask.Exception.Flatten();
                                        this.logger.LogError(
                                            $"Error in Background Task: {ex.InnerException}",
                                            ex
                                        );
                                    }

                                    lock(this.taskStartLock) {
                                        this.runningTasks.Remove(taskId);
                                    }
                                    scope.Dispose();
                                },
                                this.cancellationTokenSource.Token)));
                } catch (Exception ex) {
                    this.logger.LogError(
                        $"An error occurred attempting to start a background task: {ex}",
                        ex
                    );
                }
            }
        }

        public Task StartAsync(CancellationToken cancellationToken) {
            this.logger.LogTrace("Starting BackgroundTaskService");

            // No-op
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken) {
            List<(IServiceScope scope, Task task)> canceledTasks;

            this.logger.LogTrace("Stopping BackgroundTaskService");

            lock (this.taskStartLock) {
                // Signal cancellation
                this.cancellationTokenSource.Cancel();

                canceledTasks = this.runningTasks.Values.ToList();
            }

            this.logger.LogTrace($"Awaiting {canceledTasks.Count} Canceled Tasks.");

            foreach (var (scope, task) in canceledTasks) {
                if (cancellationToken.IsCancellationRequested) {
                    break;
                }

                await Task.WhenAny(task, Task.Delay(-1, cancellationToken))
                    .ConfigureAwait(false);

                if (task.IsCompleted) {
                    scope.Dispose();
                }
            }
        }

        /// <summary>
        ///   Wait for all queued tasks to completed.
        /// </summary>
        /// <returns></returns>
        public async Task FlushQueue() {
            IList<Task> pendingTasks;

            lock (taskStartLock) {
                pendingTasks = this.runningTasks.Values.Select(item => item.task).ToImmutableList();
            }

            await Task.WhenAll(pendingTasks);
        }

        // Clones an HttpContext
        private sealed class HttpContextWrapper : HttpContext {
            public override void Abort() {
                throw new NotSupportedException();
            }

            public override HttpRequest Request { get; }
            public override HttpResponse Response =>
                throw new NotSupportedException();

            public override IFeatureCollection Features { get; }
            public override ConnectionInfo Connection { get; }
            public override WebSocketManager WebSockets { get; }
            public override ClaimsPrincipal User { get; set; }
            public override IDictionary<Object, Object> Items { get; set; }
            public override IServiceProvider RequestServices { get; set; }
            public override CancellationToken RequestAborted { get; set; }
            public override String TraceIdentifier { get; set; }

            public override ISession Session {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public HttpContextWrapper(HttpContext original) {
                if (original == null) {
                    throw new ArgumentNullException(nameof(original));
                }

                this.Request = original.Request;
                this.Features = original.Features;
                this.Connection = original.Connection;
                this.WebSockets = original.WebSockets;
                this.User = original.User;
                this.Items = new Dictionary<Object, Object>(original.Items);
                this.RequestServices = original.RequestServices;
                this.RequestAborted = original.RequestAborted;
                this.TraceIdentifier = original.TraceIdentifier;
            }
        }
    }
}
