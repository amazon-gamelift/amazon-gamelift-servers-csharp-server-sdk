/*
* All or portions of this file Copyright (c) Amazon.com, Inc. or its affiliates or
* its licensors.
*
* For complete copyright and license terms please see the LICENSE at the root of this
* distribution (the "License"). All use of this software is governed by the License,
* or, if provided, by the license below or the license accompanying this file. Do not
* remove or modify any license notices. This file is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
*
*/

namespace Aws.GameLift.Server
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Aws.GameLift.Server.Model;
    using Aws.GameLift.Server.Model.Metrics;
    using Aws.GameLift.Server.Model.Metrics.DerivedMetrics;
    using log4net;
    using ITimer = Aws.GameLift.Server.Model.Metrics.ITimer;
    using Timer = System.Threading.Timer;

    /// <summary>
    /// Manages the creation and tracking of various metrics for GameLift servers.
    /// </summary>
#pragma warning disable S1200
    // Suppress SonarQube warning S1200 for this class.
    // S1200: Classes should not be coupled to too many other classes (Single Responsibility Principle)
    // In this case, the complexity is intentional to encapsulate metrics management logic
    // in one location, improving usability for consumers of the SDK.

    public class Metrics : IDisposable
#pragma warning restore S1200
    {
        /// <summary>
        /// The StatsD client used for sending metrics.
        /// </summary>
        private readonly IStatsDClient statsDClient;

        /// <summary>
        /// The Crash Reporter client used for tracking process crash.
        /// </summary>
        private readonly CrashReporterClient crashReporterClient;

        /// <summary>
        /// Dictionary to track all created metrics by name.
        /// </summary>
        // Suppress IDE0028: Collection initialization can be simplified
        // We need to use explicit type initialization for .NET Framework 4.6.2 compatibility
        // The simplified 'new()' syntax is only available in C# 9.0+, which isn't supported by all our target frameworks
#pragma warning disable IDE0028
        private readonly Dictionary<string, IMetric> metrics = new Dictionary<string, IMetric>();
#pragma warning restore IDE0028

        /// <summary>
        /// Lock for thread-safe access to metric collections.
        /// </summary>
        private readonly object metricsLock = new object();

        /// <summary>
        /// Timer for periodic metric flushing.
        /// </summary>
        private readonly Timer flushTimer;

        /// <summary>
        /// Cancellation token source for background operations.
        /// </summary>
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        /// <summary>
        /// Indicates whether the Metrics has been disposed.
        /// </summary>
        private bool disposed;

        /// <summary>
        /// Indicates whether the Metrics is in the process of being disposed.
        /// </summary>
        private bool isDisposing;

        private static readonly ILog Log = LogManager.GetLogger(typeof(Metrics));

        /// <summary>
        /// Maintains the 'up' gauge to indicate if the server is up (1) or down (0).
        /// This gauge is automatically updated during flush operations.
        /// </summary>
        private readonly IGauge serverUpGauge;

        /// <summary>
        /// Initializes a new instance of the <see cref="Metrics"/> class using a builder.
        /// Use MetricsBuilder.Create() to create an instance with fluent configuration.
        /// </summary>
        /// <param name="crashReporterClient">The Crash Reporter client.</param>
        /// <param name="statsDClient">The StatsD client to use for metrics reporting.</param>
        /// <param name="flushIntervalMs">The flush interval in milliseconds.</param>
        internal Metrics(CrashReporterClient crashReporterClient, IStatsDClient statsDClient, int flushIntervalMs)
        {
            if (crashReporterClient == null)
            {
                throw new ArgumentNullException(nameof(crashReporterClient));
            }

            if (statsDClient == null)
            {
                throw new ArgumentNullException(nameof(statsDClient));
            }

            this.crashReporterClient = crashReporterClient;
            this.statsDClient = statsDClient;
            flushTimer = new Timer(FlushAllMetricsCallback, null, flushIntervalMs, flushIntervalMs);
            serverUpGauge = NewGauge("up")
                .Build();
        }

        /// <summary>
        /// Creates a new Metrics builder.
        /// </summary>
        /// <returns>A new MetricsBuilder instance.</returns>
        public static MetricsBuilder Create()
        {
            return new MetricsBuilder();
        }

        /// <summary>
        /// Creates a new counter builder with the specified name.
        /// </summary>
        /// <param name="name">The name of the counter metric.</param>
        /// <returns>A new MetricBuilder instance for creating counters.</returns>
        public MetricBuilder<ICounter> NewCounter(string name)
        {
            return new MetricBuilder<ICounter>(name, this, MetricType.Counter);
        }

        /// <summary>
        /// Creates a new gauge builder with the specified name.
        /// </summary>
        /// <param name="name">The name of the gauge metric.</param>
        /// <returns>A new MetricBuilder instance for creating gauges.</returns>
        public MetricBuilder<IGauge> NewGauge(string name)
        {
            return new MetricBuilder<IGauge>(name, this, MetricType.Gauge);
        }

        /// <summary>
        /// Creates a new timer builder with the specified name.
        /// </summary>
        /// <param name="name">The name of the timer metric.</param>
        /// <returns>A new MetricBuilder instance for creating timers.</returns>
        public MetricBuilder<ITimer> NewTimer(string name)
        {
            return new MetricBuilder<ITimer>(name, this, MetricType.Timer);
        }

        /// <summary>
        /// Internal method to create and register a counter metric.
        /// </summary>
        /// <param name="name">The name of the counter metric.</param>
        /// <param name="tags">Tags to associate with the counter.</param>
        /// <param name="sampleRate">The sample rate for the counter.</param>
        /// <param name="derivedMetrics">A Set of the Derived metric types to be calculated on flush.</param>
        /// <param name="randomSeed">Optional seed for the random number generator.</param>
        /// <returns>An <see cref="ICounter"/> instance for tracking count-based metrics.</returns>
        internal ICounter CreateCounterInternal(string name, IList<string> tags, SampleRate sampleRate, ISet<IDerivedMetric> derivedMetrics, int? randomSeed)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Counter name cannot be null or empty.", nameof(name));
            }

            lock (metricsLock)
            {
                if (metrics.ContainsKey(name))
                {
                    throw new InvalidOperationException($"A metric with the name '{name}' already exists.");
                }

                List<Tag> tagObjects = ConvertToTagObjects(tags);
                Counter counter = new Counter(name, tagObjects, statsDClient, sampleRate, derivedMetrics, randomSeed);
                metrics[name] = counter;
                Log.DebugFormat("Created counter metric '{0}' with {1} tags", name, tagObjects.Count);
                return counter;
            }
        }

        /// <summary>
        /// Internal method to create and register a gauge metric.
        /// </summary>
        /// <param name="name">The name of the gauge metric.</param>
        /// <param name="tags">Tags to associate with the gauge.</param>
        /// <param name="sampleRate">The sample rate for the gauge.</param>
        /// <param name="derivedMetrics">A Set of the Derived metric types to be calculated on flush.</param>
        /// <param name="randomSeed">Optional seed for the random number generator.</param>
        /// <returns>An <see cref="IGauge"/> instance for tracking value metrics that can go up and down.</returns>
        internal IGauge CreateGaugeInternal(string name, IList<string> tags, SampleRate sampleRate, ISet<IDerivedMetric> derivedMetrics, int? randomSeed)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Gauge name cannot be null or empty.", nameof(name));
            }

            lock (metricsLock)
            {
                if (metrics.ContainsKey(name))
                {
                    throw new InvalidOperationException($"A metric with the name '{name}' already exists.");
                }

                List<Tag> tagObjects = ConvertToTagObjects(tags);
                Gauge gauge = new Gauge(name, tagObjects, statsDClient, sampleRate, derivedMetrics, randomSeed);
                metrics[name] = gauge;
                Log.DebugFormat("Created gauge metric '{0}' with {1} tags", name, tagObjects.Count);
                return gauge;
            }
        }

        /// <summary>
        /// Internal method to create and register a timer metric.
        /// </summary>
        /// <param name="name">The name of the timer metric.</param>
        /// <param name="tags">Tags to associate with the timer.</param>
        /// <param name="sampleRate">The sample rate for the timer.</param>
        /// <param name="derivedMetrics">A Set of the Derived metric types to be calculated on flush.</param>
        /// <param name="randomSeed">Optional seed for the random number generator.</param>
        /// <returns>An <see cref="ITimer"/> instance for tracking timing-based metrics.</returns>
        internal ITimer CreateTimerInternal(string name, IList<string> tags, SampleRate sampleRate, ISet<IDerivedMetric> derivedMetrics, int? randomSeed)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Timer name cannot be null or empty.", nameof(name));
            }

            lock (metricsLock)
            {
                if (metrics.ContainsKey(name))
                {
                    throw new InvalidOperationException($"A metric with the name '{name}' already exists.");
                }

                List<Tag> tagObjects = ConvertToTagObjects(tags);
                Model.Metrics.Timer timer = new Model.Metrics.Timer(name, tagObjects, statsDClient, sampleRate, derivedMetrics, randomSeed);
                metrics[name] = timer;
                Log.DebugFormat("Created timer metric '{0}' with {1} tags", name, tagObjects.Count);
                return timer;
            }
        }

        /// <summary>
        /// Deletes a metric by name.
        /// </summary>
        /// <param name="name">The name of the metric to delete.</param>
        /// <returns>True if the metric was found and deleted, false otherwise.</returns>
        public bool DeleteMetric(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Metric name cannot be null or empty.", nameof(name));
            }

            lock (metricsLock)
            {
                bool removed = metrics.Remove(name);
                Log.DebugFormat(removed ? "Deleted metric '{0}'" : "Metric '{0}' failed to delete", name);
                return removed;
            }
        }

        /// <summary>
        /// Adds a global tag to all metrics sent by this Metrics.
        /// </summary>
        /// <param name="tagValue">The tag to add.</param>
        /// <returns>A GenericOutcome indicating the success or failure of the operation.</returns>
        public GenericOutcome AddGlobalTag(string tagValue)
        {
            if (string.IsNullOrWhiteSpace(tagValue))
            {
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.VALIDATION_EXCEPTION, "Tag value cannot be null or empty."));
            }

            try
            {
                statsDClient.AddGlobalTag(new Tag(tagValue));
                return new GenericOutcome();
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to add global tag '{tagValue}': {ex.Message}", ex);
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.METRICS_CONFIGURATION_FAILED, $"Failed to add global tag: {ex.Message}"));
            }
        }

        /// <summary>
        /// Adds multiple global tags to all metrics sent by this Metrics.
        /// </summary>
        /// <param name="tagValues">list of tags to be added to the global tags.</param>
        /// <returns>A GenericOutcome indicating the success or failure of the operation.</returns>
        public GenericOutcome AddGlobalTags(ICollection<string> tagValues)
        {
            if (tagValues == null || tagValues.Count == 0)
            {
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.VALIDATION_EXCEPTION, "Tag values cannot be null or empty."));
            }

            try
            {
                foreach (var tagValue in tagValues)
                {
                    GenericOutcome outcome = AddGlobalTag(tagValue);
                    if (!outcome.Success)
                    {
                        return outcome;
                    }
                }

                return new GenericOutcome();
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to add global tags: {ex.Message}", ex);
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.METRICS_CONFIGURATION_FAILED, $"Failed to add global tags: {ex.Message}"));
            }
        }

        /// <summary>
        /// Removes a global tag from all metrics sent by this Metrics.
        /// </summary>
        /// <param name="key">The key of the tag to remove.</param>
        /// <returns>A GenericOutcome indicating the success or failure of the operation.</returns>
        public GenericOutcome RemoveGlobalTag(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.VALIDATION_EXCEPTION, "Tag key cannot be null or empty."));
            }

            try
            {
                statsDClient.RemoveGlobalTag(key);
                return new GenericOutcome();
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to remove global tag '{key}': {ex.Message}", ex);
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.METRICS_CONFIGURATION_FAILED, ex.Message));
            }
        }

        /// <summary>
        /// Callback method to be invoked when a new game session starts.
        /// Adds a global tag with the game session ID to all metrics.
        /// </summary>
        /// <param name="gameSessionId">The game session ID that has started.</param>
        /// <returns>A GenericOutcome indicating the success or failure of the operation.</returns>
        public GenericOutcome OnGameSessionStart(string gameSessionId)
        {
            if (string.IsNullOrEmpty(gameSessionId))
            {
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.VALIDATION_EXCEPTION, "GameSessionId cannot be null or empty."));
            }

            crashReporterClient.TagGameSession(gameSessionId);
            AddGlobalTag("session_id:" + gameSessionId);
            return new GenericOutcome();
        }

        /// <summary>
        /// Callback method to be invoked when a new game session starts.
        /// Adds a global tag with the game session ID to all metrics.
        /// </summary>
        /// <param name="session">The game session that has started.</param>
        /// <returns>A GenericOutcome indicating the success or failure of the operation.</returns>
        public GenericOutcome OnGameSessionStart(GameSession session)
        {
            if (session == null)
            {
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.VALIDATION_EXCEPTION, "GameSession cannot be null."));
            }

            return OnGameSessionStart(session.GameSessionId);
        }

        /// <summary>
        /// Callback method to be invoked when the process is terminating.
        /// It deregisters the process from crash handler to indicate that its a normal termination.
        /// </summary>
        public void OnProcessTermination()
        {
            crashReporterClient.DeregisterProcess();
        }

        /// <summary>
        /// Converts a list of string tags to a list of Tag objects.
        /// </summary>
        /// <param name="tags">The string tags to convert.</param>
        /// <returns>A list of Tag objects, or an empty list if tags is null.</returns>
        private static List<Tag> ConvertToTagObjects(IList<string> tags)
        {
            // Suppress IDE0028: Collection initialization can be simplified
            // We need to use explicit type initialization for .NET Framework 4.6.2 compatibility
            // The simplified 'new()' syntax is only available in C# 9.0+, which isn't supported by all our target frameworks
#pragma warning disable IDE0028
            return tags?.Select(tag => new Tag(tag)).ToList() ?? new List<Tag>();
#pragma warning restore IDE0028
        }

        /// <summary>
        /// Timer callback method that flushes all metrics.
        /// </summary>
        /// <param name="state">Timer state (not used).</param>
        private void FlushAllMetricsCallback(object state)
        {
            FlushAllMetricsInternal();
        }

        /// <summary>
        /// This method can be called manually to force an immediate flush of all metrics.
        /// </summary>
        /// <returns>A GenericOutcome indicating the success or failure of the operation.</returns>
        public GenericOutcome FlushAllMetrics()
        {
            return FlushAllMetricsInternal();
        }

        /// <summary>
        /// Flushes all metrics, including StatsDClient buffer.
        /// </summary>
        /// <returns>A GenericOutcome indicating the success or failure of the operation.</returns>
        private GenericOutcome FlushAllMetricsInternal()
        {
            if (disposed || cancellationTokenSource.Token.IsCancellationRequested)
            {
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.METRICS_SUBMISSION_FAILED, "Metrics is disposed or cancelled."));
            }

            // Update 'up' gauge based on disposing state if dispose is in progress the server is going down
            serverUpGauge.Set(isDisposing ? 0 : 1);

            IMetric[] metricsToFlush;
            lock (metricsLock)
            {
                if (metrics.Count == 0)
                {
                    return new GenericOutcome();
                }

                metricsToFlush = new IMetric[metrics.Count];
                metrics.Values.CopyTo(metricsToFlush, 0);
            }

            Log.DebugFormat("Flushing {0} metrics", metricsToFlush.Length);

            // When disposing we need a deterministic synchronous flush so that the 'up:0|g' packet is guaranteed
            // to be emitted before resources (like the underlying UDP client) are disposed. Otherwise tests (and
            // shutdown observers) may miss the final state due to the asynchronous task not completing in time.
            if (isDisposing)
            {
                return FlushMetricsInternal(metricsToFlush);
            }

            // Flush metrics outside of lock to avoid blocking metric operations (async during normal operation)
            Task.Run(() => FlushMetricsInternal(metricsToFlush), cancellationTokenSource.Token);

            return new GenericOutcome();
        }

        /// <summary>
        /// Flushes all metrics using the specified array of metrics.
        /// </summary>
        /// <param name="metricsToFlush">The array of metrics to flush.</param>
        /// <returns>A GenericOutcome indicating the success or failure of the operation.</returns>
        private GenericOutcome FlushMetricsInternal(IMetric[] metricsToFlush)
        {
            var errorMessages = new List<string>();

            try
            {
                RunMetricFlush(metricsToFlush, errorMessages);
            }
            catch (Exception ex)
            {
                errorMessages.Add($"Unexpected error during metrics flush: {ex.Message}");
            }

            if (errorMessages.Count > 0)
            {
                Log.Error($"Metrics flush completed with errors: {string.Join("; ", errorMessages)}");
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.METRICS_SUBMISSION_FAILED, string.Join("; ", errorMessages)));
            }

            return new GenericOutcome();
        }

        private void RunMetricFlush(IMetric[] metricsToFlush, ICollection<string> errorMessages)
        {
            foreach (var metric in metricsToFlush)
            {
                if (cancellationTokenSource.Token.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    GenericOutcome outcome = metric.Flush();
                    if (!outcome.Success)
                    {
                        errorMessages.Add($"Failed to flush metric '{metric.Name}': {outcome.Error?.ErrorMessage}");
                    }
                }
                catch (Exception ex)
                {
                    errorMessages.Add($"Failed to flush metric '{metric.Name}': {ex.Message}");
                }
            }

            try
            {
                statsDClient.Flush();
            }
            catch (Exception ex)
            {
                errorMessages.Add($"Failed to flush StatsDClient buffer: {ex.Message}");
            }
        }

        /// <summary>
        /// Disposes of the Metrics resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes of the Metrics resources.
        /// </summary>
        /// <param name="disposing">True if disposing managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                isDisposing = true;
                lock (metricsLock)
                {
                    if (disposed)
                    {
                        return;
                    }
                }

                crashReporterClient.Dispose();

                // Stop the timer first to prevent new flush operations
                flushTimer?.Dispose();

                // Perform final flush (including StatsDClient buffer)
                try
                {
                    GenericOutcome outcome = FlushAllMetricsInternal();
                    if (!outcome.Success)
                    {
                        Log.Error($"Error during final metrics flush: {outcome.Error?.ErrorMessage}");
                    }

                    // Also flush StatsDClient buffer directly
                    statsDClient.Flush();
                }
                catch (Exception ex)
                {
                    // Log the exception but do not rethrow during dispose
                    Log.Error($"Error during final metrics flush: {ex.Message}", ex);
                }

                // Now set disposed flag and cancel operations
                lock (metricsLock)
                {
                    disposed = true;
                }

                // Cancel ongoing metric flushing
                cancellationTokenSource.Cancel();

                // Clear metrics collection
                lock (metricsLock)
                {
                    metrics.Clear();
                }

                // Dispose remaining resources
                statsDClient.Dispose();
                cancellationTokenSource.Dispose();
                Log.Debug("Metrics manager disposed.");
            }
        }
    }
}
