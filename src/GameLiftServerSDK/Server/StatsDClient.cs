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
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using Aws.GameLift.Server.Model;
    using Aws.GameLift.Server.Model.Metrics;
    using log4net;

    /// <summary>
    /// Represents a client for sending metrics to a StatsD server.
    /// </summary>
    public class StatsDClient : IStatsDClient
    {
        private const string MetricNameNullOrEmptyError = "Metric name cannot be null or empty.";
        private const int MaxPacketSize = 8192; // Maximum UDP packet size for StatsD

        // Metric type constants for StatsD
        private const string CounterMetricType = "c";
        private const string GaugeMetricType = "g";
        private const string TimingMetricType = "ms";

        private static readonly ILog Log = LogManager.GetLogger(typeof(StatsDClient));

        private readonly IUdpClientWrapper udpClient;
        private const string FixedPrefix = "server"; // All metrics share this fixed prefix.
        private readonly int maxPacketSize;

        // Suppress IDE0028: Collection initialization can be simplified
        // We need to use explicit type initialization for .NET Framework 4.6.2 compatibility
        // The simplified 'new()' syntax is only available in C# 9.0+, which isn't supported by all our target frameworks
#pragma warning disable IDE0028
        private readonly Dictionary<string, Tag> globalTags = new Dictionary<string, Tag>();
#pragma warning restore IDE0028
        private readonly object tagsLock = new object();
        private readonly object udpLock = new object();
        private readonly object bufferLock = new object();

        // Buffering fields
        private readonly Queue<string> metricQueue = new Queue<string>();
        private int currentBufferSize;
        private bool disposed;

        /// <summary>
        /// Gets the global tags that will be sent with every metric. Returns a copy of the internal tag dictionary.
        /// </summary>
        public IDictionary<string, Tag> GlobalTags
        {
            get
            {
                lock (tagsLock)
                {
                    return new Dictionary<string, Tag>(globalTags);
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StatsDClient"/> class with the specified configuration.
        /// </summary>
        /// <param name="config">The configuration settings for the StatsDClient.</param>
        public StatsDClient(StatsDClientConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            config.Validate();

            maxPacketSize = config.MaxPacketSize;

            // Initialize global tags from config
            if (config.GlobalTags != null)
            {
                lock (tagsLock)
                {
                    foreach (var tag in config.GlobalTags)
                    {
                        globalTags[tag.Key] = tag;
                    }
                }
            }

            try
            {
                IPAddress ipaddress;
                bool isValidIpAddress = IPAddress.TryParse(config.Host, out ipaddress);

                if (!isValidIpAddress)
                {
                    IPAddress[] addresses = Dns.GetHostAddresses(config.Host);
                    if (addresses.Length == 0)
                    {
                        throw new InvalidOperationException($"No addresses found for host: {config.Host}");
                    }

                    ipaddress = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork) ?? addresses.First();
                    if (ipaddress == null)
                    {
                        throw new InvalidOperationException($"No IPv4 address found for host: {config.Host}");
                    }
                }

                IPEndPoint serverEndPoint = new IPEndPoint(ipaddress, config.Port);

                // Create UDP client
                udpClient = new UdpClientWrapper();
                udpClient.Connect(serverEndPoint);

                Log.InfoFormat(
                    "StatsDClient initialized successfully. Server: {0}:{1}, FixedPrefix: 'server', MaxPacketSize: {2}",
                    serverEndPoint.Address,
                    serverEndPoint.Port,
                    maxPacketSize);
            }
            catch (SocketException ex)
            {
                throw new InvalidOperationException($"Failed to resolve hostname '{config.Host}': {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to initialize StatsDClient for {config.Host}:{config.Port}: {ex.Message}", ex);
                throw new InvalidOperationException($"Cannot initialize StatsDClient for {config.Host}:{config.Port}. See inner exception for details.", ex);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StatsDClient"/> class with a custom UDP client for testing.
        /// </summary>
        /// <param name="udpClient">The UDP client wrapper for testing.</param>
        /// <param name="serverEndPoint">The server endpoint.</param>
        public StatsDClient(IUdpClientWrapper udpClient, IPEndPoint serverEndPoint)
        {
            this.udpClient = udpClient ?? throw new ArgumentNullException(nameof(udpClient));
            maxPacketSize = MaxPacketSize;
            Log.Debug("StatsDClient initialized with custom UDP client for testing purposes (fixed prefix 'server').");
        }

        /// <summary>
        /// Validates the metric name. Throws exceptions if validation fails.
        /// </summary>
        /// <param name="metric">The name of the metric.</param>
        private void ValidateParameters(string metric)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(StatsDClient));
            }

            if (string.IsNullOrEmpty(metric))
            {
                throw new ArgumentException(MetricNameNullOrEmptyError, nameof(metric));
            }
        }

        /// <summary>
        /// Converts Tag collection to string array for StatsD compatibility.
        /// </summary>
        /// <param name="tags">The tags to convert.</param>
        /// <returns>Array of tag strings.</returns>
        private static string[] ConvertTagsToStringArray(IList<Tag> tags)
        {
            return tags?.Select(tag => tag?.ToString()).Where(value => value != null).ToArray();
        }

        /// <summary>
        /// Appends global tags to the provided list, ensuring that tag keys are unique.
        /// If a tag key exists in both the global and provided tags, the global tag value is used.
        /// </summary>
        /// <param name="tags">The List to add tags to.</param>
        /// <returns>List of tags with global tags appended. The return will be a New List.</returns>
        private IList<Tag> AppendGlobalTags(IEnumerable<Tag> tags)
        {
            var tagDict = new Dictionary<string, Tag>();
            if (tags != null)
            {
                foreach (var tag in tags)
                {
                    tagDict[tag.Key] = tag;
                }
            }

            // Add global tags (they take precedence) so override any existing keys
            foreach (var tag in GlobalTags.Values)
            {
                tagDict[tag.Key] = tag;
            }

            return tagDict.Values.ToList();
        }

        /// <summary>
        /// Builds a StatsD metric packet string.
        /// </summary>
        /// <param name="metricName">The name of the metric.</param>
        /// <param name="value">The metric value.</param>
        /// <param name="metricType">The metric type (c for counter, g for gauge, ms for timer).</param>
        /// <param name="tags">Tags to include with the metric.</param>
        /// <param name="sampleRate">The sample rate for the metric (only used for counters).</param>
        /// <returns>A formatted StatsD packet string.</returns>
        private string BuildMetricPacket(string metricName, object value, string metricType, IList<Tag> tags, SampleRate sampleRate)
        {
            var fullMetricName = $"{FixedPrefix}_{metricName}";
            var packet = $"{fullMetricName}:{value}|{metricType}";

            // Add sample rate for counters
            if (metricType == CounterMetricType && sampleRate != null && sampleRate.ToDouble() < 1.0)
            {
                packet += $"|@{sampleRate.ToDouble():0.##}";
            }

            // Add tags if present
            if (tags != null && tags.Count > 0)
            {
                var tagStrings = ConvertTagsToStringArray(tags);
                if (tagStrings.Length > 0)
                {
                    packet += $"|#{string.Join(",", tagStrings)}";
                }
            }

            return packet;
        }

        /// <summary>
        /// Adds a metric packet to the buffer and flushes if necessary.
        /// </summary>
        /// <param name="packet">The metric packet to buffer.</param>
        private void BufferPacket(string packet)
        {
            if (disposed)
            {
                return;
            }

            lock (bufferLock)
            {
                var packetBytes = Encoding.UTF8.GetByteCount(packet);

                // Calculate size if we add this packet (including newline separator)
                var newlineBytes = metricQueue.Count > 0 ? 1 : 0; // Add newline separator if queue not empty
                var totalSize = currentBufferSize + newlineBytes + packetBytes;

                // If adding this packet would exceed max size, flush buffer first
                if (totalSize > maxPacketSize && metricQueue.Count > 0)
                {
                    SendBufferedPackets();
                    currentBufferSize = 0;
                    totalSize = packetBytes;
                }

                // Add packet to buffer
                metricQueue.Enqueue(packet);
                currentBufferSize = totalSize;
            }
        }

        /// <summary>
        /// Sends all buffered packets as a single UDP message.
        /// </summary>
        private void SendBufferedPackets()
        {
            if (disposed || metricQueue.Count == 0)
            {
                return;
            }

            try
            {
                lock (udpLock)
                {
                    // Combine all queued packets with newline separators
                    var combinedPacket = string.Join("\n", metricQueue);
                    var bytes = Encoding.UTF8.GetBytes(combinedPacket);

                    udpClient.Send(bytes, bytes.Length);

                    // Clear the buffer
                    metricQueue.Clear();
                    currentBufferSize = 0;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to send buffered StatsD packets. Error: {ex.Message}", ex);
                // Clear buffer on error to prevent infinite retry
                metricQueue.Clear();
                currentBufferSize = 0;
                // Don't rethrow - metrics should be non-blocking
            }
        }

        /// <summary>
        /// Flushes any buffered packets immediately.
        /// This method can be called to force sending of any queued metrics.
        /// </summary>
        public void Flush()
        {
            lock (bufferLock)
            {
                SendBufferedPackets();
            }
        }

        /// <summary>
        /// Increments a counter metric by the given value.
        /// </summary>
        /// <param name="metric">The name of the metric to increment.</param>
        /// <param name="value">The value to increment by.</param>
        /// <param name="tags">Tags to associate with the metric.</param>
        /// <param name="sampleRate">The sample rate for the metric.</param>
        public void Increment(string metric, int value, IList<Tag> tags, SampleRate sampleRate)
        {
            ValidateParameters(metric);
            var finalTags = AppendGlobalTags(tags);
            Log.DebugFormat("Incrementing counter metric '{0}' by {1} with {2} tags", metric, value, finalTags.Count);

            try
            {
                var packet = BuildMetricPacket(metric, value, CounterMetricType, finalTags, sampleRate);
                BufferPacket(packet);
            }
            catch (Exception e)
            {
                Log.Error($"Failed to send increment metric '{metric}': {e.Message}", e);
                throw;
            }
        }

        /// <summary>
        /// Decrements a counter metric by the given value.
        /// </summary>
        /// <param name="metric">The name of the metric to decrement.</param>
        /// <param name="value">The value to decrement by.</param>
        /// <param name="tags">Tags to associate with the metric.</param>
        /// <param name="sampleRate">The sample rate for the metric.</param>
        public void Decrement(string metric, int value, IList<Tag> tags, SampleRate sampleRate)
        {
            ValidateParameters(metric);
            var finalTags = AppendGlobalTags(tags);
            Log.DebugFormat("Decrementing counter metric '{0}' by {1} with {2} tags", metric, value, finalTags.Count);

            try
            {
                var packet = BuildMetricPacket(metric, -value, CounterMetricType, finalTags, sampleRate);
                BufferPacket(packet);
            }
            catch (Exception e)
            {
                Log.Error($"Failed to send decrement metric '{metric}': {e.Message}", e);
                throw;
            }
        }

        /// <summary>
        /// Sets a gauge value for the specified metric.
        /// </summary>
        /// <param name="metric">The name of the metric to set.</param>
        /// <param name="value">The value to set for the gauge.</param>
        /// <param name="tags">Tags to associate with the metric.</param>
        /// <param name="sampleRate">The sample rate for the metric.</param>
        public void Gauge(string metric, double value, IList<Tag> tags, SampleRate sampleRate)
        {
            ValidateParameters(metric);
            var finalTags = AppendGlobalTags(tags);
            Log.DebugFormat("Setting gauge metric '{0}' to {1} with {2} tags", metric, value, finalTags.Count);

            try
            {
                var packet = BuildMetricPacket(metric, value, GaugeMetricType, finalTags, sampleRate);
                BufferPacket(packet);
            }
            catch (Exception e)
            {
                Log.Error($"Failed to send gauge metric '{metric}': {e.Message}", e);
                throw;
            }
        }

        /// <summary>
        /// Records timing information for the specified metric.
        /// </summary>
        /// <param name="metric">The name of the metric to record timing for.</param>
        /// <param name="value">The timing value in milliseconds.</param>
        /// <param name="tags">Tags to associate with the metric.</param>
        /// <param name="sampleRate">The sample rate for the metric.</param>
        public void Timing(string metric, double value, IList<Tag> tags, SampleRate sampleRate)
        {
            ValidateParameters(metric);
            var finalTags = AppendGlobalTags(tags);
            Log.DebugFormat("Recording timing metric '{0}' with value {1}ms and {2} tags", metric, value, finalTags.Count);

            try
            {
                var packet = BuildMetricPacket(metric, value, TimingMetricType, finalTags, sampleRate);
                BufferPacket(packet);
            }
            catch (Exception e)
            {
                Log.Error($"Failed to send timing metric '{metric}': {e.Message}", e);
                throw;
            }
        }

        /// <summary>
        /// Add to the global tags list sent with every metric.
        /// </summary>
        /// <param name="tag">The tag to be added.</param>
        public void AddGlobalTag(Tag tag)
        {
            if (tag == null)
            {
                Log.Error("Ignoring attempt to add null global tag.");
                return;
            }

            lock (tagsLock)
            {
                globalTags[tag.Key] = tag;  // Simple dictionary assignment - replaces if key exists
            }
        }

        /// <summary>
        /// Add to the global tags list sent with every metric.
        /// </summary>
        /// <param name="tag">The tag to be added.</param>
        public void AddGlobalTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                Log.Error("Attempted to add null or empty global tag string, ignoring silently");
                return;
            }

            // Let Tag constructor validation throw ArgumentException for invalid formats
            // This includes cases like ":value" (empty key) which should fail fast
            AddGlobalTag(new Tag(tag));
        }

        /// <summary>
        /// Remove a tag from the global tags list sent with every metric.
        /// </summary>
        /// <param name="tag">The tag to be removed.</param>
        public void RemoveGlobalTag(Tag tag)
        {
            lock (tagsLock)
            {
                globalTags.Remove(tag.Key);
            }
        }

        /// <summary>
        /// Remove a tag from the global tags list sent with every metric.
        /// </summary>
        /// <param name="tag">The tag key to be removed.</param>
        public void RemoveGlobalTag(string tag)
        {
            lock (tagsLock)
            {
                globalTags.Remove(tag);
            }
        }

        /// <summary>
        /// Disposes of the StatsDClient resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected implementation of Dispose pattern.
        /// </summary>
        /// <param name="disposing">True if called from Dispose(), false if called from finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    try
                    {
                        // Flush any remaining buffered packets before disposing
                        Flush();
                        udpClient?.Close();
                        udpClient?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Error disposing UDP client: {ex.Message}", ex);
                    }
                }

                disposed = true;
            }
        }
    }
}
