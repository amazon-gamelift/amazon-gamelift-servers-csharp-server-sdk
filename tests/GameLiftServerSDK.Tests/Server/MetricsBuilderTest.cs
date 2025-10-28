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

namespace Aws.GameLift.Tests.Server
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using Aws.GameLift.Server;
    using Aws.GameLift.Server.Model;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    public class MetricsBuilderTest
    {
        private Mock<IUdpClientWrapper> mockUdpClient;
        private IPEndPoint testEndPoint;

        [SetUp]
        public void SetUp()
        {
            mockUdpClient = new Mock<IUdpClientWrapper>();
            testEndPoint = new IPEndPoint(IPAddress.Loopback, 8125);

            // Setup the mock UDP client
            mockUdpClient.Setup(m => m.Send(It.IsAny<byte[]>(), It.IsAny<int>())).Returns(1);
            mockUdpClient.Setup(m => m.Connect(It.IsAny<IPEndPoint>()));
            mockUdpClient.Setup(m => m.Close());
        }

        private static StatsDClient GetInternalStatsDClient(Metrics manager)
        {
            // Access private readonly field 'statsDClient' via reflection for verification
            var field = typeof(Metrics).GetField("statsDClient", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "Could not find internal statsDClient field via reflection.");
            var value = field.GetValue(manager) as StatsDClient;
            Assert.IsNotNull(value, "Internal statsDClient is not an instance of StatsDClient.");
            return value;
        }

        [Test]
        public void GIVEN_custom_environment_variables_WHEN_builder_used_THEN_creates_statsd_client_with_custom_config()
        {
            // GIVEN
            string customCrashReporterdHost = "localhost";
            string customCrashReporterPort = "9877";
            string customStatsdHost = "localhost";
            string customStatsdPort = "9876";
            string customFlushInterval = "5000";
            string customMaxPacketSize = "1024";

            Environment.SetEnvironmentVariable("GAMELIFT_CRASH_REPORTER_HOST", customCrashReporterdHost);
            Environment.SetEnvironmentVariable("GAMELIFT_CRASH_REPORTER_PORT", customCrashReporterPort);
            Environment.SetEnvironmentVariable("GAMELIFT_STATSD_HOST", customStatsdHost);
            Environment.SetEnvironmentVariable("GAMELIFT_STATSD_PORT", customStatsdPort);
            Environment.SetEnvironmentVariable("GAMELIFT_FLUSH_INTERVAL_MS", customFlushInterval);
            Environment.SetEnvironmentVariable("GAMELIFT_MAX_PACKET_SIZE", customMaxPacketSize);

            Metrics metrics = null;
            try
            {
                // WHEN & THEN - Should not throw and creates working client
                Assert.DoesNotThrow(() => metrics = Metrics.Create().Build());
                Assert.IsNotNull(metrics);
            }
            finally
            {
                // Clean up environment variables
                Environment.SetEnvironmentVariable("GAMELIFT_CRASH_REPORTER_HOST", null);
                Environment.SetEnvironmentVariable("GAMELIFT_CRASH_REPORTER_PORT", null);
                Environment.SetEnvironmentVariable("GAMELIFT_STATSD_HOST", null);
                Environment.SetEnvironmentVariable("GAMELIFT_STATSD_PORT", null);
                Environment.SetEnvironmentVariable("GAMELIFT_FLUSH_INTERVAL_MS", null);
                Environment.SetEnvironmentVariable("GAMELIFT_MAX_PACKET_SIZE", null);
                metrics?.Dispose();
            }
        }

        [Test]
        public void GIVEN_invalid_crashReporter_configuration_WHEN_builder_used_THEN_throws_appropriate_exceptions()
        {
            // Test null host
            Assert.Throws<ArgumentException>(() =>
                Metrics.Create().SetCrashReporterHost(null).Build());

            // Test empty host
            Assert.Throws<ArgumentException>(() =>
                Metrics.Create().SetCrashReporterHost(string.Empty).Build());

            // Test invalid port (zero)
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                Metrics.Create().SetCrashReporterPort(0).Build());

            // Test invalid port (negative)
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                Metrics.Create().SetCrashReporterPort(-1).Build());

            // Test invalid port (greater than 65535)
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                Metrics.Create().SetCrashReporterPort(65536).Build());
        }

        [Test]
        public void GIVEN_invalid_statsd_configuration_WHEN_builder_used_THEN_throws_appropriate_exceptions()
        {
            // Test null host
            Assert.Throws<ArgumentException>(() =>
                Metrics.Create().SetStatsdHost(null).Build());

            // Test empty host
            Assert.Throws<ArgumentException>(() =>
                Metrics.Create().SetStatsdHost(string.Empty).Build());

            // Test invalid port (zero)
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                Metrics.Create().SetStatsdPort(0).Build());

            // Test invalid port (negative)
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                Metrics.Create().SetStatsdPort(-1).Build());

            // Test invalid port (greater than 65535)
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                Metrics.Create().SetStatsdPort(65536).Build());
        }

        [Test]
        public void GIVEN_different_builder_configurations_WHEN_metrics_managers_created_THEN_all_work_correctly()
        {
            // Test default builder
            var manager1 = Metrics.Create().Build();
            Assert.IsNotNull(manager1);
            manager1.Dispose();

            // Test custom client builder using real StatsDClient with mock UDP
            var statsDClient = new StatsDClient(mockUdpClient.Object, testEndPoint);
            var manager2 = Metrics.Create()
                .SetStatsDClient(statsDClient)
                .SetFlushInterval(10000)
                .Build();
            Assert.IsNotNull(manager2);
            manager2.Dispose();

            // Test configuration builder
            var manager3 = Metrics.Create()
                .SetCrashReporterHost("localhost")
                .SetCrashReporterPort(8126)
                .SetStatsdHost("localhost")
                .SetStatsdPort(8125)
                .SetFlushInterval(10000)
                .Build();
            Assert.IsNotNull(manager3);
            manager3.Dispose();
        }

        [Test]
        public void GIVEN_valid_crashReporterPort_boundaries_WHEN_builder_used_THEN_creates_successfully()
        {
            // Test minimum valid port (1)
            Assert.DoesNotThrow(() =>
            {
                var manager = Metrics.Create().SetCrashReporterPort(1).Build();
                manager.Dispose();
            });

            // Test maximum valid port (65535)
            Assert.DoesNotThrow(() =>
            {
                var manager = Metrics.Create().SetCrashReporterPort(65535).Build();
                manager.Dispose();
            });

            // Test common valid port
            Assert.DoesNotThrow(() =>
            {
                var manager = Metrics.Create().SetCrashReporterPort(8125).Build();
                manager.Dispose();
            });
        }        

        [Test]
        public void GIVEN_valid_statsdPort_boundaries_WHEN_builder_used_THEN_creates_successfully()
        {
            // Test minimum valid port (1)
            Assert.DoesNotThrow(() =>
            {
                var manager = Metrics.Create().SetStatsdPort(1).Build();
                manager.Dispose();
            });

            // Test maximum valid port (65535)
            Assert.DoesNotThrow(() =>
            {
                var manager = Metrics.Create().SetStatsdPort(65535).Build();
                manager.Dispose();
            });

            // Test common valid port
            Assert.DoesNotThrow(() =>
            {
                var manager = Metrics.Create().SetStatsdPort(8125).Build();
                manager.Dispose();
            });
        }

        [Test]
        public void GIVEN_null_statsd_client_WHEN_builder_used_THEN_throws_argument_null_exception()
        {
            // GIVEN
            IStatsDClient nullClient = null;

            // WHEN & THEN
            var exception = Assert.Throws<ArgumentNullException>(() =>
                Metrics.Create().SetStatsDClient(nullClient).Build());
            Assert.AreEqual("statsDClient", exception.ParamName);
        }

        [Test]
        public void GIVEN_metrics_manager_builder_WHEN_chained_configuration_used_THEN_creates_properly_configured_manager()
        {
            // GIVEN & WHEN
            var metrics = Metrics.Create()
                .SetCrashReporterHost("localhost")
                .SetCrashReporterPort(9998)
                .SetStatsdHost("localhost")
                .SetStatsdPort(9999)
                .SetFlushInterval(5000)
                .Build();

            // THEN
            Assert.IsNotNull(metrics);
            metrics.Dispose();
        }

        /// <summary>
        /// Helper class to access internal GetEffectiveFlushInterval method for testing.
        /// </summary>
        private class TestableMetricsBuilder : MetricsBuilder
        {
            public new int GetEffectiveFlushInterval()
            {
                return base.GetEffectiveFlushInterval();
            }
        }

        [Test]
        [TestCase(1, 1, TestName = "Valid minimum flush interval")]
        [TestCase(5000, 5000, TestName = "Valid mid-range flush interval")]
        [TestCase(10000, 10000, TestName = "Valid maximum flush interval")]
        [TestCase(-1, 10000, TestName = "Negative flush interval defaults to 10000ms")]
        [TestCase(0, 10000, TestName = "Zero flush interval defaults to 10000ms")]
        [TestCase(-5000, 10000, TestName = "Large negative flush interval defaults to 10000ms")]
        [TestCase(10001, 10000, TestName = "Flush interval 1ms above limit defaults to 10000ms")]
        [TestCase(25000, 10000, TestName = "Flush interval 25000ms defaults to 10000ms")]
        [TestCase(int.MaxValue, 10000, TestName = "Maximum int flush interval defaults to 10000ms")]
        public void GIVEN_valid_flush_interval_WHEN_set_flush_interval_called_THEN_accepts_exact_value(int inputInterval, int expectedInterval)
        {
            // GIVEN & WHEN
            var builder = new TestableMetricsBuilder();
            builder.SetFlushInterval(inputInterval);

            // THEN
            Assert.AreEqual(expectedInterval, builder.GetEffectiveFlushInterval());
        }

        [Test]
        [TestCase("5000", 5000, TestName = "Valid environment flush interval")]
        [TestCase("1", 1, TestName = "Minimum valid environment flush interval")]
        [TestCase("10000", 10000, TestName = "Maximum valid environment flush interval")]
        [TestCase("15000", 10000, TestName = "Environment flush interval above limit defaults to 10000ms")]
        [TestCase("-1", 10000, TestName = "Negative environment flush interval defaults to 10000ms")]
        [TestCase("0", 10000, TestName = "Zero environment flush interval defaults to 10000ms")]
        public void GIVEN_valid_environment_flush_interval_WHEN_get_effective_flush_interval_called_THEN_uses_environment_value(string envValue, int expectedInterval)
        {
            // GIVEN
            Environment.SetEnvironmentVariable("GAMELIFT_FLUSH_INTERVAL_MS", envValue);

            try
            {
                var builder = new TestableMetricsBuilder();

                // WHEN
                int actualInterval = builder.GetEffectiveFlushInterval();

                // THEN
                Assert.AreEqual(expectedInterval, actualInterval);
            }
            finally
            {
                Environment.SetEnvironmentVariable("GAMELIFT_FLUSH_INTERVAL_MS", null);
            }
        }

        [Test]
        [TestCase("invalid", TestName = "Non-numeric environment variable")]
        [TestCase("", TestName = "Empty environment variable")]
        [TestCase("abc123", TestName = "Alphanumeric environment variable")]
        public void GIVEN_non_numeric_environment_flush_interval_WHEN_get_effective_flush_interval_called_THEN_uses_default(string envValue)
        {
            // GIVEN
            Environment.SetEnvironmentVariable("GAMELIFT_FLUSH_INTERVAL_MS", envValue);

            try
            {
                var builder = new TestableMetricsBuilder();

                // WHEN
                int actualInterval = builder.GetEffectiveFlushInterval();

                // THEN
                Assert.AreEqual(10000, actualInterval);
            }
            finally
            {
                Environment.SetEnvironmentVariable("GAMELIFT_FLUSH_INTERVAL_MS", null);
            }
        }

        [Test]
        public void GIVEN_builder_flush_interval_set_WHEN_environment_variable_also_set_THEN_builder_takes_precedence()
        {
            // GIVEN
            Environment.SetEnvironmentVariable("GAMELIFT_FLUSH_INTERVAL_MS", "8000");

            try
            {
                var builder = new TestableMetricsBuilder();
                builder.SetFlushInterval(3000);

                // WHEN
                int actualInterval = builder.GetEffectiveFlushInterval();

                // THEN
                Assert.AreEqual(3000, actualInterval);
            }
            finally
            {
                Environment.SetEnvironmentVariable("GAMELIFT_FLUSH_INTERVAL_MS", null);
            }
        }

        [Test]
        public void GIVEN_no_flush_interval_configuration_WHEN_get_effective_flush_interval_called_THEN_uses_default_10000ms()
        {
            // GIVEN
            var builder = new TestableMetricsBuilder();

            // WHEN
            int actualInterval = builder.GetEffectiveFlushInterval();

            // THEN
            Assert.AreEqual(10000, actualInterval);
        }

        [Test]
        public void GIVEN_multiple_set_flush_interval_calls_WHEN_get_effective_flush_interval_called_THEN_uses_last_valid_value()
        {
            // GIVEN
            var builder = new TestableMetricsBuilder();
            builder.SetFlushInterval(1000);
            builder.SetFlushInterval(2000);
            builder.SetFlushInterval(3000);

            // WHEN
            int actualInterval = builder.GetEffectiveFlushInterval();

            // THEN
            Assert.AreEqual(3000, actualInterval);
        }

        [Test]
        public void GIVEN_invalid_then_valid_flush_interval_WHEN_get_effective_flush_interval_called_THEN_uses_last_valid_value()
        {
            // GIVEN
            var builder = new TestableMetricsBuilder();
            builder.SetFlushInterval(15000); // Invalid - will be clamped to 10000
            builder.SetFlushInterval(5000);  // Valid

            // WHEN
            int actualInterval = builder.GetEffectiveFlushInterval();

            // THEN
            Assert.AreEqual(5000, actualInterval);
        }

        [Test]
        public void GIVEN_single_global_tag_WHEN_builder_used_THEN_creates_manager_with_global_tag()
        {
            // GIVEN
            string globalTag = "environment:test";

            // Build StatsDClient with global tag first
            var config = StatsDClientConfig.ForHost("localhost")
                .WithGlobalTag(globalTag)
                .Build();
            var statsDClient = new StatsDClient(config);

            // WHEN
            var metrics = Metrics.Create()
                .SetStatsDClient(statsDClient)
                .Build();

            // THEN
            Assert.IsNotNull(metrics);
            Assert.IsTrue(statsDClient.GlobalTags.ContainsKey("environment"));
            Assert.AreEqual("test", statsDClient.GlobalTags["environment"].Value);
            metrics.Dispose();
        }

        [Test]
        public void GIVEN_multiple_global_tags_WHEN_builder_used_THEN_creates_manager_with_all_global_tags()
        {
            // GIVEN
            string[] globalTags = { "environment:test", "service:game", "version:1.0" };

            // Build StatsDClient with global tags
            var configBuilder = StatsDClientConfig.ForHost("localhost");
            foreach (var tag in globalTags)
            {
                configBuilder.WithGlobalTag(tag);
            }

            var config = configBuilder.Build();
            var statsDClient = new StatsDClient(config);

            // WHEN
            var metrics = Metrics.Create()
                .SetStatsDClient(statsDClient)
                .Build();

            // THEN
            Assert.IsNotNull(metrics);
            Assert.IsTrue(statsDClient.GlobalTags.ContainsKey("environment"));
            Assert.AreEqual("test", statsDClient.GlobalTags["environment"].Value);
            Assert.IsTrue(statsDClient.GlobalTags.ContainsKey("service"));
            Assert.AreEqual("game", statsDClient.GlobalTags["service"].Value);
            Assert.IsTrue(statsDClient.GlobalTags.ContainsKey("version"));
            Assert.AreEqual("1.0", statsDClient.GlobalTags["version"].Value);

            metrics.Dispose();
        }

        [Test]
        public void GIVEN_global_tags_collection_WHEN_builder_used_THEN_creates_manager_with_all_global_tags()
        {
            // GIVEN
            var globalTags = new List<string> { "region:us-west-2", "team:backend" };

            // Build StatsDClient with global tags
            var configBuilder = StatsDClientConfig.ForHost("localhost");
            foreach (var tag in globalTags)
            {
                configBuilder.WithGlobalTag(tag);
            }

            var config = configBuilder.Build();
            var statsDClient = new StatsDClient(config);

            // WHEN
            var metrics = Metrics.Create()
                .SetStatsDClient(statsDClient)
                .Build();

            // THEN
            Assert.IsNotNull(metrics);
            Assert.IsTrue(statsDClient.GlobalTags.ContainsKey("region"));
            Assert.AreEqual("us-west-2", statsDClient.GlobalTags["region"].Value);
            Assert.IsTrue(statsDClient.GlobalTags.ContainsKey("team"));
            Assert.AreEqual("backend", statsDClient.GlobalTags["team"].Value);

            metrics.Dispose();
        }

        [Test]
        public void GIVEN_chained_global_tag_calls_WHEN_builder_used_THEN_creates_manager_with_all_tags()
        {
            // GIVEN & WHEN
            var config = StatsDClientConfig.ForHost("localhost")
                .WithGlobalTag("environment:production")
                .WithGlobalTag("service:gamelift")
                .WithGlobalTag("region:us-east-1")
                .WithGlobalTag("datacenter:primary")
                .Build();
            var statsDClient = new StatsDClient(config);

            var metrics = Metrics.Create()
                .SetStatsDClient(statsDClient)
                .Build();

            // THEN
            Assert.IsNotNull(metrics);
            Assert.AreEqual(4, statsDClient.GlobalTags.Count);

            Assert.IsTrue(statsDClient.GlobalTags.ContainsKey("environment"));
            Assert.AreEqual("production", statsDClient.GlobalTags["environment"].Value);

            Assert.IsTrue(statsDClient.GlobalTags.ContainsKey("service"));
            Assert.AreEqual("gamelift", statsDClient.GlobalTags["service"].Value);

            Assert.IsTrue(statsDClient.GlobalTags.ContainsKey("region"));
            Assert.AreEqual("us-east-1", statsDClient.GlobalTags["region"].Value);

            Assert.IsTrue(statsDClient.GlobalTags.ContainsKey("datacenter"));
            Assert.AreEqual("primary", statsDClient.GlobalTags["datacenter"].Value);

            metrics.Dispose();
        }

        [Test]
        public void GIVEN_null_or_empty_global_tag_WHEN_builder_used_THEN_throws_argument_exception()
        {
            // Test null global tag
            var exception1 = Assert.Throws<ArgumentException>(() =>
                Metrics.Create().AddGlobalTag(null));
            Assert.AreEqual("tag", exception1.ParamName);

            // Test empty global tag
            var exception2 = Assert.Throws<ArgumentException>(() =>
                Metrics.Create().AddGlobalTag(string.Empty));
            Assert.AreEqual("tag", exception2.ParamName);

            // Test whitespace global tag
            var exception3 = Assert.Throws<ArgumentException>(() =>
                Metrics.Create().AddGlobalTag("   "));
            Assert.AreEqual("tag", exception3.ParamName);
        }

        [Test]
        public void GIVEN_null_global_tags_array_WHEN_builder_used_THEN_throws_argument_null_exception()
        {
            // GIVEN
            string[] nullTags = null;

            // WHEN & THEN
            var exception = Assert.Throws<ArgumentNullException>(() =>
                Metrics.Create().AddGlobalTags(nullTags));
            Assert.AreEqual("tags", exception.ParamName);
        }

        [Test]
        public void GIVEN_null_global_tags_collection_WHEN_builder_used_THEN_throws_argument_null_exception()
        {
            // GIVEN
            List<string> nullTags = null;

            // WHEN & THEN
            var exception = Assert.Throws<ArgumentNullException>(() =>
                Metrics.Create().AddGlobalTags(nullTags));
            Assert.AreEqual("tags", exception.ParamName);
        }

        [Test]
        public void GIVEN_no_global_tags_WHEN_builder_used_THEN_creates_manager_without_adding_tags()
        {
            // GIVEN & WHEN
            var statsDClient = new StatsDClient(mockUdpClient.Object, testEndPoint);
            var metrics = Metrics.Create()
                .SetStatsDClient(statsDClient)
                .Build();

            // THEN
            Assert.IsNotNull(metrics);
            Assert.AreEqual(0, statsDClient.GlobalTags.Count);

            metrics.Dispose();
        }

        [Test]
        public void GIVEN_builder_WHEN_build_called_THEN_includes_process_id_global_tag()
        {
            // WHEN
            Metrics manager = null;
            try
            {
                manager = Metrics.Create().Build();
                var stats = GetInternalStatsDClient(manager);

                // THEN
                Assert.IsTrue(stats.GlobalTags.ContainsKey("process_pid"), "process_pid tag missing");
                var expected = Process.GetCurrentProcess().Id.ToString();
                Assert.AreEqual(expected, stats.GlobalTags["process_pid"].Value);
            }
            finally
            {
                manager?.Dispose();
            }
        }

        [Test]
        public void GIVEN_env_gamelift_process_id_set_WHEN_build_called_THEN_includes_gamelift_process_id_tag()
        {
            // GIVEN
            var prior = Environment.GetEnvironmentVariable("GAMELIFT_SDK_PROCESS_ID");
            var testValue = "unit-test-proc";
            Environment.SetEnvironmentVariable("GAMELIFT_SDK_PROCESS_ID", testValue);

            Metrics manager = null;
            try
            {
                // WHEN
                manager = Metrics.Create().Build();
                var stats = GetInternalStatsDClient(manager);

                // THEN
                Assert.IsTrue(stats.GlobalTags.ContainsKey("gamelift_process_id"), "gamelift_process_id tag missing");
                Assert.AreEqual(testValue, stats.GlobalTags["gamelift_process_id"].Value);
            }
            finally
            {
                // Clean up
                Environment.SetEnvironmentVariable("GAMELIFT_SDK_PROCESS_ID", prior);
                manager?.Dispose();
            }
        }

        [Test]
        public void GIVEN_env_gamelift_process_id_unset_WHEN_build_called_THEN_gamelift_process_id_tag_absent()
        {
            // GIVEN
            var prior = Environment.GetEnvironmentVariable("GAMELIFT_SDK_PROCESS_ID");
            Environment.SetEnvironmentVariable("GAMELIFT_SDK_PROCESS_ID", null);

            Metrics manager = null;
            try
            {
                // WHEN
                manager = Metrics.Create().Build();
                var stats = GetInternalStatsDClient(manager);

                // THEN
                Assert.IsFalse(stats.GlobalTags.ContainsKey("gamelift_process_id"), "gamelift_process_id tag should be absent when env var is not set");
            }
            finally
            {
                // Restore
                Environment.SetEnvironmentVariable("GAMELIFT_SDK_PROCESS_ID", prior);
                manager?.Dispose();
            }
        }
    }
}
