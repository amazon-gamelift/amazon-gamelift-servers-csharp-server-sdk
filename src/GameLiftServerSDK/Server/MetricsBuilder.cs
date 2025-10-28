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
    using System.Diagnostics;
    using System.Net;
    using Aws.GameLift.Server.Model;
    using log4net;

    /// <summary>
    /// Builder for creating Metrics instances with fluent configuration.
    /// </summary>
    public class MetricsBuilder
    {
        private const int MaxFlushIntervalMs = 10000; // 10 seconds - must not exceed OTEL collector setting
        private const int DefaultFlushIntervalMs = 10000; // 10 seconds default
        private const int DefaultCrashReporterPortNumber = 8126; // Default Crash Reporter port
        private const int DefaultStatsdDPortNumber = 8125; // Default StatsD port
        private const int DefaultPacketSize = 512; // Default StatsD packet size

        private static readonly ILog Log = LogManager.GetLogger(typeof(MetricsBuilder));

        // Suppress IDE0028: Collection initialization can be simplified
        // We need to use explicit type initialization for .NET Framework 4.6.2 compatibility
        // The simplified 'new()' syntax is only available in C# 9.0+, which isn't supported by all our target frameworks
#pragma warning disable IDE0028
        private readonly List<string> globalTags = new List<string>();
#pragma warning restore IDE0028
        private string crashReporterHost;
        private int crashReporterPort = -1;
        private string statsdHost;
        private int statsdPort = -1;
        private int flushIntervalMs = -1;
        private int maxPacketSize = -1;
        private IStatsDClient customStatsDClient;

        /// <summary>
        /// Sets the Crash Reporter server host.
        /// </summary>
        /// <param name="host">The Crash Reporter server host.</param>
        /// <returns>This builder instance for method chaining.</returns>
        public MetricsBuilder SetCrashReporterHost(string host)
        {
            if (string.IsNullOrEmpty(host))
            {
                throw new ArgumentException("Crash Reporter Host cannot be null or empty.", nameof(host));
            }

            this.crashReporterHost = host;
            return this;
        }

        /// <summary>
        /// Sets the Crash Reporter server port.
        /// </summary>
        /// <param name="port">The Crash Reporter server port.</param>
        /// <returns>This builder instance for method chaining.</returns>
        public MetricsBuilder SetCrashReporterPort(int port)
        {
            if (port <= 0 || port > IPEndPoint.MaxPort)
            {
                throw new ArgumentOutOfRangeException(nameof(port), "Crash Reporter Port must be between 1 and 65535.");
            }

            this.crashReporterPort = port;
            return this;
        }

        /// <summary>
        /// Sets the StatsD server host.
        /// </summary>
        /// <param name="host">The StatsD server host.</param>
        /// <returns>This builder instance for method chaining.</returns>
        public MetricsBuilder SetStatsdHost(string host)
        {
            if (string.IsNullOrEmpty(host))
            {
                throw new ArgumentException("StatsD Host cannot be null or empty.", nameof(host));
            }

            this.statsdHost = host;
            return this;
        }

        /// <summary>
        /// Sets the StatsD server port.
        /// </summary>
        /// <param name="port">The StatsD server port.</param>
        /// <returns>This builder instance for method chaining.</returns>
        public MetricsBuilder SetStatsdPort(int port)
        {
            if (port <= 0 || port > IPEndPoint.MaxPort)
            {
                throw new ArgumentOutOfRangeException(nameof(port), "StatsD Port must be between 1 and 65535.");
            }

            this.statsdPort = port;
            return this;
        }

        /// <summary>
        /// Sets the flush interval in milliseconds.
        /// </summary>
        /// <param name="proposedFlushInterval">The flush interval in milliseconds.</param>
        /// <returns>This builder instance for method chaining.</returns>
        public MetricsBuilder SetFlushInterval(int proposedFlushInterval)
        {
            flushIntervalMs = ClampFlushInterval(proposedFlushInterval);
            return this;
        }

        /// <summary>
        /// Sets the maximum UDP packet size for StatsD metrics.
        /// </summary>
        /// <param name="maxPacketSize">The maximum packet size in bytes.</param>
        /// <returns>This builder instance for method chaining.</returns>
        public MetricsBuilder SetMaxPacketSize(int maxPacketSize)
        {
            if (maxPacketSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPacketSize), "MaxPacketSize must be greater than 0.");
            }

            this.maxPacketSize = maxPacketSize;
            return this;
        }

        /// <summary>
        /// Sets a pre-built StatsD client. This allows for complete control over StatsDClient configuration.
        /// </summary>
        /// <param name="statsDClient">The pre-built StatsD client.</param>
        /// <returns>This builder instance for method chaining.</returns>
        public MetricsBuilder SetStatsDClient(IStatsDClient statsDClient)
        {
            if (statsDClient == null)
            {
                throw new ArgumentNullException(nameof(statsDClient));
            }

            customStatsDClient = statsDClient;
            return this;
        }

        /// <summary>
        /// Adds a global tag that will be applied to all metrics created by this Metrics.
        /// </summary>
        /// <param name="tag">The global tag to add.</param>
        /// <returns>This builder instance for method chaining.</returns>
        public MetricsBuilder AddGlobalTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                throw new ArgumentException("Global tag cannot be null or empty.", nameof(tag));
            }

            globalTags.Add(tag);
            return this;
        }

        /// <summary>
        /// Adds multiple global tags that will be applied to all metrics created by this Metrics.
        /// </summary>
        /// <param name="tags">The global tags to add.</param>
        /// <returns>This builder instance for method chaining.</returns>
        public MetricsBuilder AddGlobalTags(params string[] tags)
        {
            if (tags == null)
            {
                throw new ArgumentNullException(nameof(tags));
            }

            foreach (string tag in tags)
            {
                AddGlobalTag(tag);
            }

            return this;
        }

        /// <summary>
        /// Adds multiple global tags that will be applied to all metrics created by this Metrics.
        /// </summary>
        /// <param name="tags">The collection of global tags to add.</param>
        /// <returns>This builder instance for method chaining.</returns>
        public MetricsBuilder AddGlobalTags(IEnumerable<string> tags)
        {
            if (tags == null)
            {
                throw new ArgumentNullException(nameof(tags));
            }

            foreach (string tag in tags)
            {
                AddGlobalTag(tag);
            }

            return this;
        }

        /// <summary>
        /// Gets the effective flush interval, considering builder settings, environment variables, and defaults.
        /// </summary>
        /// <returns>The effective flush interval in milliseconds.</returns>
        internal int GetEffectiveFlushInterval()
        {
            if (flushIntervalMs > 0)
            {
                return flushIntervalMs;
            }

            int parsedInterval = ParseFlushIntervalFromEnvironment();
            if (parsedInterval > 0)
            {
                return ClampFlushInterval(parsedInterval);
            }

            return DefaultFlushIntervalMs;
        }

        /// <summary>
        /// Parses the flush interval from the environment variable.
        /// </summary>
        /// <returns>The parsed flush interval in milliseconds, or -1 if not found or invalid.</returns>
        private static int ParseFlushIntervalFromEnvironment()
        {
            string flushIntervalEnv = System.Environment.GetEnvironmentVariable("GAMELIFT_FLUSH_INTERVAL_MS");
            if (!string.IsNullOrEmpty(flushIntervalEnv) && int.TryParse(flushIntervalEnv, out int parsedInterval))
            {
                return parsedInterval;
            }

            return -1;
        }

        /// <summary>
        /// Clamps the flush interval to allowed bounds without mutating builder state.
        /// </summary>
        /// <param name="interval">The interval to clamp.</param>
        /// <returns>The clamped interval.</returns>
        private static int ClampFlushInterval(int interval)
        {
            if (interval <= 0)
            {
                Log.Warn($"Flush interval {interval}ms is not positive. Defaulting to {DefaultFlushIntervalMs}ms.");
                return DefaultFlushIntervalMs;
            }
            else if (interval > MaxFlushIntervalMs)
            {
                Log.Warn($"Flush interval {interval}ms exceeds the maximum allowed value of {MaxFlushIntervalMs}ms (OTEL collector limit). " +
                        $"Defaulting to {MaxFlushIntervalMs}ms.");
                return MaxFlushIntervalMs;
            }
            else
            {
                return interval;
            }
        }

        /// <summary>
        /// Builds the Metrics instance with the configured settings.
        /// </summary>
        /// <returns>A new Metrics instance.</returns>
        public Metrics Build()
        {
            string finalCrashReporterHost = DetermineCrashReporterHost();
            int finalCrashReporterPort = DetermineCrashReporterPort();
            CrashReporterClient crashReporterClient = new CrashReporterClient(finalCrashReporterHost, finalCrashReporterPort);
            crashReporterClient.RegisterProcess();

            IStatsDClient statsDClient;
            int interval = GetEffectiveFlushInterval();

            if (customStatsDClient != null)
            {
                // Use the provided pre-built client
                statsDClient = customStatsDClient;
            }
            else
            {
                // Create a new StatsDClient using configuration
                string finalStatsdHost = DetermineStatsdHost();
                int finalStatsdPort = DetermineStatsdPort();
                maxPacketSize = DetermineMaxPacketSize();
                string processPid = Process.GetCurrentProcess().Id.ToString();
                string gameliftProcessId = System.Environment.GetEnvironmentVariable("GAMELIFT_SDK_PROCESS_ID");

                // Build StatsDClientConfig using the builder pattern
                var configBuilder = StatsDClientConfig.ForHost(finalStatsdHost)
                    .WithPort(finalStatsdPort);

                // Add optional configurations if specified
                if (maxPacketSize > 0)
                {
                    configBuilder.WithMaxPacketSize(maxPacketSize);
                }

                // Add default tags
                globalTags.Add($"process_pid:{processPid}");
                if (!string.IsNullOrEmpty(gameliftProcessId))
                {
                    globalTags.Add($"gamelift_process_id:{gameliftProcessId}");
                }

                // Add global tags from builder to config
                foreach (var tag in globalTags)
                {
                    configBuilder.WithGlobalTag(tag);
                }

                var config = configBuilder.Build();
                statsDClient = new StatsDClient(config);
            }

            var metrics = new Metrics(crashReporterClient, statsDClient, interval);
            return metrics;
        }

        /// <summary>
        /// Determines the effective Crash Reporter server host, using the builder value, environment variable, or default.
        /// </summary>
        /// <returns>The resolved Crash Reporter server host.</returns>
        private string DetermineCrashReporterHost()
        {
            return crashReporterHost ?? System.Environment.GetEnvironmentVariable("GAMELIFT_CRASH_REPORTER_HOST") ?? "localhost";
        }

        /// <summary>
        /// Determines the effective Crash Reporter server port, using the builder value, environment variable, or default.
        /// </summary>
        /// <returns>The resolved Crash Reporter server port.</returns>
        private int DetermineCrashReporterPort()
        {
            if (crashReporterPort > 0)
            {
                return crashReporterPort;
            }

            string envPort = System.Environment.GetEnvironmentVariable("GAMELIFT_CRASH_REPORTER_PORT");
            return int.TryParse(envPort, out int parsedPort) ? parsedPort : DefaultCrashReporterPortNumber;
        }

        /// <summary>
        /// Determines the effective StatsD server host, using the builder value, environment variable, or default.
        /// </summary>
        /// <returns>The resolved StatsD server host.</returns>
        private string DetermineStatsdHost()
        {
            return statsdHost ?? System.Environment.GetEnvironmentVariable("GAMELIFT_STATSD_HOST") ?? "localhost";
        }

        /// <summary>
        /// Determines the effective StatsD server port, using the builder value, environment variable, or default.
        /// </summary>
        /// <returns>The resolved StatsD server port.</returns>
        private int DetermineStatsdPort()
        {
            if (statsdPort > 0)
            {
                return statsdPort;
            }

            string envPort = System.Environment.GetEnvironmentVariable("GAMELIFT_STATSD_PORT");
            return int.TryParse(envPort, out int parsedPort) ? parsedPort : DefaultStatsdDPortNumber;
        }

        /// <summary>
        /// Determines the maximum UDP packet size for StatsD metrics, using the builder value, environment variable, or default.
        /// </summary>
        /// <returns>The resolved maximum packet size in bytes.</returns>
        private int DetermineMaxPacketSize()
        {
            if (maxPacketSize > 0)
            {
                return maxPacketSize;
            }

            string envPacketSize = System.Environment.GetEnvironmentVariable("GAMELIFT_MAX_PACKET_SIZE");
            return int.TryParse(envPacketSize, out int parsedSize) ? parsedSize : DefaultPacketSize;
        }
    }
}
