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

namespace Aws.GameLift.Server.Model
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Aws.GameLift.Server.Model.Metrics;

    /// <summary>
    /// Configuration settings for the StatsDClient.
    /// </summary>
    public class StatsDClientConfig
    {
        private const int DefaultPort = 8125;
        private const int MaxPortNumber = 65535;

        /// <summary>
        /// Gets or sets the StatsD server host.
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// Gets or sets the StatsD server port. Default is 8125.
        /// </summary>
        public int Port { get; set; } = DefaultPort;

        /// <summary>
        /// Gets or sets the maximum UDP packet size. Default is 8192 bytes.
        /// </summary>
        public int MaxPacketSize { get; set; } = 8192;

        /// <summary>
        /// Gets or sets the initial global tags to be sent with every metric.
        /// </summary>
        // Suppress IDE0028: Collection initialization can be simplified
        // We need to use explicit type initialization for .NET Framework 4.6.2 compatibility
        // The simplified 'new()' syntax is only available in C# 9.0+, which isn't supported by all our target frameworks
#pragma warning disable IDE0028
        public IList<Tag> GlobalTags { get; set; } = new List<Tag>();
#pragma warning restore IDE0028

        /// <summary>
        /// Initializes a new instance of the <see cref="StatsDClientConfig"/> class.
        /// Use the Builder to create configured instances.
        /// </summary>
        private StatsDClientConfig()
        {
        }

        /// <summary>
        /// Creates a new builder for configuring StatsDClientConfig.
        /// </summary>
        /// <param name="host">The StatsD server host.</param>
        /// <returns>A new StatsDClientConfigBuilder instance.</returns>
        public static StatsDClientConfigBuilder ForHost(string host)
        {
            return new StatsDClientConfigBuilder(host);
        }

        /// <summary>
        /// Validates the configuration settings.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when Host is null or empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when Port is invalid.</exception>
        public void Validate()
        {
            if (string.IsNullOrEmpty(Host))
            {
                throw new ArgumentException("Host cannot be null or empty.");
            }

            if (Port <= 0 || Port > MaxPortNumber)
            {
                throw new ArgumentOutOfRangeException($"Port must be between 1 and {MaxPortNumber}.");
            }

            if (MaxPacketSize <= 0)
            {
                throw new ArgumentException("MaxPacketSize must be greater than 0.");
            }
        }

        /// <summary>
        /// Builder class for creating StatsDClientConfig instances.
        /// </summary>
        public class StatsDClientConfigBuilder
        {
            private readonly StatsDClientConfig config;

            internal StatsDClientConfigBuilder(string host)
            {
                config = new StatsDClientConfig
                {
                    Host = host,
                };
            }

            /// <summary>
            /// Sets the port for the StatsD server.
            /// </summary>
            /// <param name="port">The port number.</param>
            /// <returns>This builder instance for chaining.</returns>
            public StatsDClientConfigBuilder WithPort(int port)
            {
                config.Port = port;
                return this;
            }

            /// <summary>
            /// Sets the maximum UDP packet size.
            /// </summary>
            /// <param name="maxPacketSize">The maximum packet size in bytes.</param>
            /// <returns>This builder instance for chaining.</returns>
            public StatsDClientConfigBuilder WithMaxPacketSize(int maxPacketSize)
            {
                config.MaxPacketSize = maxPacketSize;
                return this;
            }

            /// <summary>
            /// Adds a global tag that will be sent with every metric.
            /// </summary>
            /// <param name="tag">The tag to add.</param>
            /// <returns>This builder instance for chaining.</returns>
            public StatsDClientConfigBuilder WithGlobalTag(Tag tag)
            {
                if (tag != null)
                {
                    config.GlobalTags.Add(tag);
                }

                return this;
            }

            /// <summary>
            /// Adds a global tag that will be sent with every metric.
            /// </summary>
            /// <param name="tag">The tag value to add.</param>
            /// <returns>This builder instance for chaining.</returns>
            public StatsDClientConfigBuilder WithGlobalTag(string tag)
            {
                if (!string.IsNullOrEmpty(tag))
                {
                    config.GlobalTags.Add(new Tag(tag));
                }

                return this;
            }

            /// <summary>
            /// Adds multiple global tags that will be sent with every metric.
            /// </summary>
            /// <param name="tags">The tags to add.</param>
            /// <returns>This builder instance for chaining.</returns>
            public StatsDClientConfigBuilder WithGlobalTags(IEnumerable<Tag> tags)
            {
                if (tags != null)
                {
                    foreach (var tag in tags.Where(t => t != null))
                    {
                        config.GlobalTags.Add(tag);
                    }
                }

                return this;
            }

            /// <summary>
            /// Builds the StatsDClientConfig instance.
            /// </summary>
            /// <returns>A configured StatsDClientConfig instance.</returns>
            public StatsDClientConfig Build()
            {
                config.Validate();
                return config;
            }
        }
    }
}
