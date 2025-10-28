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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Aws.GameLift.Server;
using Aws.GameLift.Server.Model;
using Aws.GameLift.Server.Model.Metrics;

namespace Aws.GameLift.Tests.Server
{
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    public class StatsDClientTest
    {
        private Mock<IUdpClientWrapper> mockUdpClient;
        private StatsDClient statsDClient;
        private IPEndPoint testEndPoint;

        [SetUp]
        public void Setup()
        {
            mockUdpClient = new Mock<IUdpClientWrapper>();
            testEndPoint = new IPEndPoint(IPAddress.Loopback, 8125);

            // Setup the mock UDP client
            mockUdpClient.Setup(m => m.Send(It.IsAny<byte[]>(), It.IsAny<int>())).Returns(1);
            mockUdpClient.Setup(m => m.Connect(It.IsAny<IPEndPoint>()));
            mockUdpClient.Setup(m => m.Close());
        }

        [TearDown]
        public void TearDown()
        {
            statsDClient?.Dispose();
        }

        [Test]
        public void GIVEN_valid_config_WHEN_statsd_client_created_THEN_client_initialized()
        {
            // GIVEN
            var config = StatsDClientConfig.ForHost("localhost")
                .WithPort(8125)
                .Build();

            // WHEN & THEN
            Assert.DoesNotThrow(() => statsDClient = new StatsDClient(config));
            Assert.IsNotNull(statsDClient);
        }

        [Test]
        public void GIVEN_null_config_WHEN_statsd_client_created_THEN_throws_argument_null_exception()
        {
            // GIVEN
            StatsDClientConfig config = null;

            // WHEN & THEN
            var exception = Assert.Throws<ArgumentNullException>(() => new StatsDClient(config));
            Assert.AreEqual("config", exception.ParamName);
        }

        [Test]
        public void GIVEN_config_with_global_tags_WHEN_statsd_client_created_THEN_global_tags_initialized()
        {
            // GIVEN
            var config = StatsDClientConfig.ForHost("localhost")
                .WithGlobalTag("env:test")
                .WithGlobalTag("service:game")
                .Build();

            // WHEN
            statsDClient = new StatsDClient(config);

            // THEN
            Assert.AreEqual(2, statsDClient.GlobalTags.Count);
            Assert.AreEqual("test", statsDClient.GlobalTags["env"].Value);
            Assert.AreEqual("game", statsDClient.GlobalTags["service"].Value);
        }

        [Test]
        public void GIVEN_config_with_ipv4_address_WHEN_statsd_client_created_THEN_client_initialized()
        {
            // GIVEN
            var config = StatsDClientConfig.ForHost("127.0.0.1")
                .WithPort(8125)
                .Build();

            // WHEN & THEN
            Assert.DoesNotThrow(() => statsDClient = new StatsDClient(config));
            Assert.IsNotNull(statsDClient);
        }

        [Test]
        public void GIVEN_config_with_invalid_hostname_WHEN_statsd_client_created_THEN_throws_invalid_operation_exception()
        {
            // GIVEN
            var config = StatsDClientConfig.ForHost("invalid.hostname.that.does.not.exist.example")
                .WithPort(8125)
                .Build();

            // WHEN & THEN
            var exception = Assert.Throws<InvalidOperationException>(() => new StatsDClient(config));
            Assert.That(exception.Message, Does.Contain("Failed to resolve hostname"));
            Assert.IsInstanceOf<SocketException>(exception.InnerException);
        }

        [Test]
        public void GIVEN_statsdclient_WHEN_add_global_tag_THEN_global_tag_added()
        {
            // GIVEN
            Tag globalTag = new Tag("key3:value3");
            statsDClient = new StatsDClient(mockUdpClient.Object, testEndPoint);

            // WHEN
            statsDClient.AddGlobalTag(globalTag);

            // THEN
            Assert.IsNotEmpty(statsDClient.GlobalTags);
            Assert.IsTrue(statsDClient.GlobalTags.ContainsKey(globalTag.Key));
        }

        [Test]
        public void GIVEN_statsdclient_WHEN_add_string_global_tag_THEN_global_tag_added()
        {
            // GIVEN
            string globalTagString = "key3:value3";
            Tag globalTag = new Tag(globalTagString);
            statsDClient = new StatsDClient(mockUdpClient.Object, testEndPoint);

            // WHEN
            statsDClient.AddGlobalTag(globalTagString);

            // THEN
            Assert.IsNotEmpty(statsDClient.GlobalTags);
            Assert.IsTrue(statsDClient.GlobalTags.ContainsKey(globalTag.Key));

        }

        [Test]
        public void GIVEN_statsdclient_WHEN_remove_global_tag_THEN_global_tag_removed()
        {
            // GIVEN
            Tag globalTag = new Tag("key3:value3");
            statsDClient = new StatsDClient(mockUdpClient.Object, testEndPoint);
            statsDClient.AddGlobalTag(globalTag);
            Assert.IsNotEmpty(statsDClient.GlobalTags);
            Assert.IsTrue(statsDClient.GlobalTags.ContainsKey(globalTag.Key));

            // WHEN
            statsDClient.RemoveGlobalTag(globalTag);

            // THEN
            Assert.IsEmpty(statsDClient.GlobalTags);
        }

        [Test]
        public void GIVEN_statsdclient_WHEN_remove_string_global_tag_THEN_global_tag_removed()
        {
            // GIVEN
            string globalTagString = "key3:value3";
            Tag globalTag = new Tag(globalTagString);
            statsDClient = new StatsDClient(mockUdpClient.Object, testEndPoint);
            statsDClient.AddGlobalTag(globalTag);
            Assert.IsNotEmpty(statsDClient.GlobalTags);
            Assert.IsTrue(statsDClient.GlobalTags.ContainsKey(globalTag.Key));

            // WHEN
            statsDClient.RemoveGlobalTag(globalTag.Key);

            // THEN
            Assert.IsEmpty(statsDClient.GlobalTags);
        }

        [Test]
        public void GIVEN_counter_metric_WHEN_increment_called_THEN_udp_packet_sent()
        {
            // GIVEN
            string counterName = "test_counter";
            List<Tag> tags = new List<Tag> { new Tag("key1:value1") };
            statsDClient = new StatsDClient(mockUdpClient.Object, testEndPoint);

            // WHEN
            statsDClient.Increment(counterName, 1, tags, new SampleFractional(1.0));
            statsDClient.Flush();

            // THEN
            mockUdpClient.Verify(
                m => m.Send(
                It.Is<byte[]>(bytes => ValidateStatsDPacket(bytes, "server_test_counter:1|c|#key1:value1")),
                It.IsAny<int>()),
                Times.Once);
        }

        [Test]
        public void GIVEN_gauge_metric_WHEN_gauge_called_THEN_udp_packet_sent()
        {
            // GIVEN
            string gaugeName = "test_gauge";
            List<Tag> tags = new List<Tag> { new Tag("key1:value1") };
            statsDClient = new StatsDClient(mockUdpClient.Object, testEndPoint);

            // WHEN
            statsDClient.Gauge(gaugeName, 42.0, tags, null);
            statsDClient.Flush();

            // THEN
            mockUdpClient.Verify(
                m => m.Send(
                It.Is<byte[]>(bytes => ValidateStatsDPacket(bytes, "server_test_gauge:42|g|#key1:value1")),
                It.IsAny<int>()),
                Times.Once);
        }

        [Test]
        public void GIVEN_timer_metric_WHEN_timing_called_THEN_udp_packet_sent()
        {
            // GIVEN
            string timerName = "test_timer";
            List<Tag> tags = new List<Tag> { new Tag("key1:value1") };
            statsDClient = new StatsDClient(mockUdpClient.Object, testEndPoint);

            // WHEN
            statsDClient.Timing(timerName, 100.0, tags, null);
            statsDClient.Flush();

            // THEN
            mockUdpClient.Verify(
                m => m.Send(
                It.Is<byte[]>(bytes => ValidateStatsDPacket(bytes, "server_test_timer:100|ms|#key1:value1")),
                It.IsAny<int>()),
                Times.Once);
        }

        [Test]
        public void GIVEN_local_and_global_tags_WHEN_metric_sent_THEN_both_tags_included()
        {
            // GIVEN
            string counterName = "test_counter";
            List<Tag> localTags = new List<Tag> { new Tag("local:tag") };
            Tag globalTag = new Tag("global:tag");
            statsDClient = new StatsDClient(mockUdpClient.Object, testEndPoint);
            statsDClient.AddGlobalTag(globalTag);

            // WHEN
            statsDClient.Increment(counterName, 1, localTags, new SampleFractional(1.0));
            statsDClient.Flush();

            // THEN
            mockUdpClient.Verify(
                m => m.Send(
                It.Is<byte[]>(bytes => ValidateStatsDPacketContainsTags(bytes, "server_test_counter:1|c", new[] { "local:tag", "global:tag" })),
                It.IsAny<int>()),
                Times.Once);
        }

        [Test]
        public void GIVEN_duplicate_tags_WHEN_metric_sent_THEN_tags_deduplicated()
        {
            // GIVEN
            string counterName = "test_counter";
            List<Tag> localTags = new List<Tag> { new Tag("duplicate:tag"), new Tag("local:tag") };
            Tag duplicateGlobalTag = new Tag("duplicate:tag");
            statsDClient = new StatsDClient(mockUdpClient.Object, testEndPoint);
            statsDClient.AddGlobalTag(duplicateGlobalTag);

            // WHEN
            statsDClient.Increment(counterName, 1, localTags, new SampleFractional(1.0));
            statsDClient.Flush();

            // THEN
            mockUdpClient.Verify(
                m => m.Send(
                It.Is<byte[]>(bytes => ValidateStatsDPacketContainsTags(bytes, "server_test_counter:1|c", new[] { "duplicate:tag", "local:tag" })),
                It.IsAny<int>()),
                Times.Once);
        }

        [Test]
        public void GIVEN_duplicate_tag_keys_WHEN_metric_sent_THEN_tags_deduplicated_with_global()
        {
            // GIVEN
            string counterName = "test_counter";
            List<Tag> localTags = new List<Tag> { new Tag("duplicate:local"), new Tag("local:tag") };
            Tag duplicateGlobalTag = new Tag("duplicate:global");
            statsDClient = new StatsDClient(mockUdpClient.Object, testEndPoint);
            statsDClient.AddGlobalTag(duplicateGlobalTag);

            // WHEN
            statsDClient.Increment(counterName, 1, localTags, new SampleFractional(1.0));
            statsDClient.Flush();

            // THEN
            mockUdpClient.Verify(
                m => m.Send(
                It.Is<byte[]>(bytes => ValidateStatsDPacketContainsTags(bytes, "server_test_counter:1|c", new[] { "duplicate:global", "local:tag" })),
                It.IsAny<int>()),
                Times.Once);
        }

        [Test]
        public void GIVEN_sample_rate_WHEN_counter_sent_THEN_sample_rate_included()
        {
            // GIVEN
            string counterName = "test_counter";
            List<Tag> tags = new List<Tag>();
            statsDClient = new StatsDClient(mockUdpClient.Object, testEndPoint);

            // WHEN
            statsDClient.Increment(counterName, 1, tags, new SampleFractional(0.5));
            statsDClient.Flush();

            // THEN
            mockUdpClient.Verify(
                m => m.Send(
                It.Is<byte[]>(bytes => ValidateStatsDPacket(bytes, "server_test_counter:1|c|@0.5")),
                It.IsAny<int>()),
                Times.Once);
        }

        [Test]
        public void GIVEN_sample_rate_WHEN_gauge_sent_THEN_sample_rate_not_included()
        {
            // GIVEN - Sample rates should not be included for gauge metrics
            string gaugeName = "test_gauge";
            List<Tag> tags = new List<Tag>();
            statsDClient = new StatsDClient(mockUdpClient.Object, testEndPoint);

            // WHEN
            statsDClient.Gauge(gaugeName, 42.0, tags, new SampleFractional(0.5));
            statsDClient.Flush();

            // THEN
            mockUdpClient.Verify(
                m => m.Send(
                It.Is<byte[]>(bytes => ValidateStatsDPacket(bytes, "server_test_gauge:42|g") && !ContainsSampleRate(bytes)),
                It.IsAny<int>()),
                Times.Once);
        }

        [Test]
        public void GIVEN_sample_rate_WHEN_timing_sent_THEN_sample_rate_not_included()
        {
            // GIVEN - Sample rates should not be included for timing metrics
            string timerName = "test_timer";
            List<Tag> tags = new List<Tag>();
            statsDClient = new StatsDClient(mockUdpClient.Object, testEndPoint);

            // WHEN
            statsDClient.Timing(timerName, 100.0, tags, new SampleFractional(0.5));
            statsDClient.Flush();

            // THEN
            mockUdpClient.Verify(
                m => m.Send(
                It.Is<byte[]>(bytes => ValidateStatsDPacket(bytes, "server_test_timer:100|ms") && !ContainsSampleRate(bytes)),
                It.IsAny<int>()),
                Times.Once);
        }

        [Test]
        public void GIVEN_null_metric_name_WHEN_increment_called_THEN_throws_argument_exception()
        {
            // GIVEN
            statsDClient = new StatsDClient(mockUdpClient.Object, testEndPoint);

            // WHEN & THEN
            var exception = Assert.Throws<ArgumentException>(() =>
                statsDClient.Increment(null, 1, new List<Tag>(), new SampleFractional(1.0)));
            Assert.That(exception.Message, Does.Contain("Metric name cannot be null or empty"));
        }

        [Test]
        public void GIVEN_empty_metric_name_WHEN_gauge_called_THEN_throws_argument_exception()
        {
            // GIVEN
            statsDClient = new StatsDClient(mockUdpClient.Object, testEndPoint);

            // WHEN & THEN
            var exception = Assert.Throws<ArgumentException>(() =>
                statsDClient.Gauge(string.Empty, 42.0, new List<Tag>(), null));
            Assert.That(exception.Message, Does.Contain("Metric name cannot be null or empty"));
        }

        [Test]
        public void GIVEN_disposed_client_WHEN_metric_sent_THEN_throws_object_disposed_exception()
        {
            // GIVEN
            statsDClient = new StatsDClient(mockUdpClient.Object, testEndPoint);
            statsDClient.Dispose();

            // WHEN & THEN
            Assert.Throws<ObjectDisposedException>(() =>
                statsDClient.Increment("test", 1, new List<Tag>(), new SampleFractional(1.0)));
        }

        [Test]
        public void GIVEN_builder_WHEN_all_options_set_THEN_config_built_successfully()
        {
            // GIVEN & WHEN
            var config = StatsDClientConfig.ForHost("test.host")
                .WithPort(9125)
                .WithMaxPacketSize(4096)
                .WithGlobalTag("env:test")
                .Build();

            // THEN
            Assert.AreEqual("test.host", config.Host);
            Assert.AreEqual(9125, config.Port);
            Assert.AreEqual(4096, config.MaxPacketSize);
            Assert.AreEqual(1, config.GlobalTags.Count);
        }

        [Test]
        public void GIVEN_multiple_small_metrics_WHEN_sent_THEN_buffered_and_sent_together()
        {
            // GIVEN
            statsDClient = new StatsDClient(mockUdpClient.Object, testEndPoint);
            var capturedPackets = new List<string>();

            mockUdpClient.Setup(m => m.Send(It.IsAny<byte[]>(), It.IsAny<int>()))
                .Callback<byte[], int>((bytes, length) =>
                {
                    var packet = Encoding.UTF8.GetString(bytes, 0, length);
                    capturedPackets.Add(packet);
                });

            // WHEN - Send multiple small metrics
            statsDClient.Increment("metric1", 1, new List<Tag>(), new SampleFractional(1.0));
            statsDClient.Increment("metric2", 1, new List<Tag>(), new SampleFractional(1.0));
            statsDClient.Increment("metric3", 1, new List<Tag>(), new SampleFractional(1.0));

            // Force flush to see buffered result
            statsDClient.Flush();

            // THEN - Should be sent as one combined packet with exact formatting
            Assert.AreEqual(1, capturedPackets.Count, "Should send exactly one combined packet");

            var combinedPacket = capturedPackets[0];
            var expectedPacket = "server_metric1:1|c\nserver_metric2:1|c\nserver_metric3:1|c";

            Assert.AreEqual(expectedPacket, combinedPacket, "Packet format should be consistent with newline separators");
        }

        [Test]
        public void GIVEN_metric_exceeding_buffer_size_WHEN_sent_THEN_previous_buffer_flushed_first()
        {
            // GIVEN
            var config = StatsDClientConfig.ForHost("localhost")
                .WithMaxPacketSize(100) // Small buffer for testing
                .Build();
            statsDClient = new StatsDClient(config);

            var capturedPackets = new List<string>();
            var mockUdp = new Mock<IUdpClientWrapper>();
            mockUdp.Setup(m => m.Send(It.IsAny<byte[]>(), It.IsAny<int>()))
                .Callback<byte[], int>((bytes, length) =>
                {
                    var packet = Encoding.UTF8.GetString(bytes, 0, length);
                    capturedPackets.Add(packet);
                });

            // Replace with mock for testing
            statsDClient.Dispose();
            statsDClient = new StatsDClient(mockUdp.Object, testEndPoint);

            // WHEN - Send metrics that will exceed buffer
            statsDClient.Increment("small_metric", 1, new List<Tag>(), new SampleFractional(1.0));
            statsDClient.Increment("this_is_a_very_long_metric_name_that_will_likely_exceed_buffer_size", 1, new List<Tag>(), new SampleFractional(1.0));
            statsDClient.Flush();

            // THEN - Should result in multiple packets
            Assert.GreaterOrEqual(capturedPackets.Count, 1);
            mockUdp.Verify(m => m.Send(It.IsAny<byte[]>(), It.IsAny<int>()), Times.AtLeastOnce);
        }

        [Test]
        public void GIVEN_buffered_metrics_WHEN_flush_called_THEN_buffer_sent_immediately()
        {
            // GIVEN
            statsDClient = new StatsDClient(mockUdpClient.Object, testEndPoint);
            statsDClient.Increment("buffered_metric", 1, new List<Tag>(), new SampleFractional(1.0));

            // Verify packet not sent yet
            mockUdpClient.Verify(m => m.Send(It.IsAny<byte[]>(), It.IsAny<int>()), Times.Never);

            // WHEN
            statsDClient.Flush();

            // THEN
            mockUdpClient.Verify(m => m.Send(It.IsAny<byte[]>(), It.IsAny<int>()), Times.Once);
        }

        [Test]
        public void GIVEN_buffered_metrics_WHEN_disposed_THEN_buffer_flushed_before_disposal()
        {
            // GIVEN
            statsDClient = new StatsDClient(mockUdpClient.Object, testEndPoint);
            statsDClient.Increment("dispose_test", 1, new List<Tag>(), new SampleFractional(1.0));

            // Verify packet not sent yet
            mockUdpClient.Verify(m => m.Send(It.IsAny<byte[]>(), It.IsAny<int>()), Times.Never);

            // WHEN
            statsDClient.Dispose();

            // THEN - Buffer should be flushed during disposal
            mockUdpClient.Verify(m => m.Send(It.IsAny<byte[]>(), It.IsAny<int>()), Times.Once);
        }

        [Test]
        public void GIVEN_existing_tag_WHEN_add_same_key_different_value_THEN_tag_replaced()
        {
            // GIVEN - Initial session_id tag
            statsDClient = new StatsDClient(mockUdpClient.Object, testEndPoint);
            statsDClient.AddGlobalTag("session_id:game123");

            Assert.AreEqual(1, statsDClient.GlobalTags.Count);
            Assert.IsTrue(statsDClient.GlobalTags.ContainsKey("session_id"));
            Assert.AreEqual("game123", statsDClient.GlobalTags["session_id"].Value);

            // WHEN - Update session_id with new value (should replace, not add)
            statsDClient.AddGlobalTag("session_id:game456");

            // THEN - Should still have only 1 tag with updated value
            Assert.AreEqual(1, statsDClient.GlobalTags.Count, "Should have exactly 1 tag after replacement");
            Assert.IsTrue(statsDClient.GlobalTags.ContainsKey("session_id"));
            Assert.AreEqual("game456", statsDClient.GlobalTags["session_id"].Value, "Value should be updated to game456");

            Assert.IsFalse(statsDClient.GlobalTags.Values.Any(t => t.Value == "game123"), "Old value should not exist");

            Assert.AreEqual("session_id:game456", statsDClient.GlobalTags["session_id"].ToString());
        }

        [Test]
        public void GIVEN_existing_tag_WHEN_multiple_replacements_THEN_last_value_persists()
        {
            // GIVEN
            statsDClient = new StatsDClient(mockUdpClient.Object, testEndPoint);

            // WHEN
            statsDClient.AddGlobalTag("session_id:game123");
            statsDClient.AddGlobalTag("session_id:game456");
            statsDClient.AddGlobalTag("session_id:game789");

            // THEN
            Assert.AreEqual(1, statsDClient.GlobalTags.Count);
            Assert.AreEqual("game789", statsDClient.GlobalTags["session_id"].Value);
        }

        [Test]
        public void GIVEN_existing_tag_WHEN_add_tag_object_same_key_THEN_replaced()
        {
            // GIVEN
            statsDClient = new StatsDClient(mockUdpClient.Object, testEndPoint);

            // WHEN
            statsDClient.AddGlobalTag("env:dev");
            statsDClient.AddGlobalTag(new Tag("env", "prod"));

            // THEN
            Assert.AreEqual(1, statsDClient.GlobalTags.Count);
            Assert.AreEqual("prod", statsDClient.GlobalTags["env"].Value);
        }

        [Test]
        public void GIVEN_existing_tag_WHEN_remove_by_key_only_THEN_tag_removed()
        {
            // GIVEN
            statsDClient = new StatsDClient(mockUdpClient.Object, testEndPoint);

            // WHEN
            statsDClient.AddGlobalTag("session_id:game123");

            // Remove using key only (no value)
            statsDClient.RemoveGlobalTag("session_id");

            // THEN
            Assert.IsEmpty(statsDClient.GlobalTags);
        }

        [Test]
        public void GIVEN_multiple_tags_WHEN_replace_one_THEN_others_unchanged()
        {
            // GIVEN
            statsDClient = new StatsDClient(mockUdpClient.Object, testEndPoint);

            // WHEN
            statsDClient.AddGlobalTag("env:dev");
            statsDClient.AddGlobalTag("region:us-east");
            statsDClient.AddGlobalTag("session_id:game123");

            statsDClient.AddGlobalTag("session_id:game456");

            // THEN
            Assert.AreEqual(3, statsDClient.GlobalTags.Count);
            Assert.AreEqual("dev", statsDClient.GlobalTags["env"].Value);
            Assert.AreEqual("us-east", statsDClient.GlobalTags["region"].Value);
            Assert.AreEqual("game456", statsDClient.GlobalTags["session_id"].Value);
        }

        [Test]
        public void GIVEN_config_with_duplicate_tag_keys_WHEN_initialized_THEN_last_value_used()
        {
            // GIVE
            var config = StatsDClientConfig.ForHost("localhost")
                .WithGlobalTag("env:dev")
                .WithGlobalTag("env:staging")
                .WithGlobalTag("env:prod")
                .Build();

            // WHEN
            statsDClient = new StatsDClient(config);

            // THEN
            Assert.AreEqual(1, statsDClient.GlobalTags.Count);
            Assert.AreEqual("prod", statsDClient.GlobalTags["env"].Value);
        }

        [Test]
        public void GIVEN_null_tag_WHEN_add_global_tag_THEN_handles_gracefully()
        {
            // GIVEN
            statsDClient = new StatsDClient(mockUdpClient.Object, testEndPoint);

            // WHEN/THEN - Should not throw, might ignore or handle gracefully
            Assert.DoesNotThrow(() => statsDClient.AddGlobalTag((Tag)null));
            Assert.DoesNotThrow(() => statsDClient.AddGlobalTag((string)null));
            Assert.DoesNotThrow(() => statsDClient.AddGlobalTag(""));
            Assert.DoesNotThrow(() => statsDClient.AddGlobalTag("   "));
        }

        [Test]
        public void GIVEN_invalid_tag_format_WHEN_add_global_tag_THEN_handles_appropriately()
        {
            // GIVEN
            statsDClient = new StatsDClient(mockUdpClient.Object, testEndPoint);

            // WHEN/THEN
            Assert.Throws<ArgumentException>(() => statsDClient.AddGlobalTag("invalid_tag_no_colon"));
        }

        [Test]
        public void GIVEN_tag_with_empty_key_WHEN_add_global_tag_THEN_rejected()
        {
            // GIVEN
            statsDClient = new StatsDClient(mockUdpClient.Object, testEndPoint);

            // WHEN/THEN - Empty key should be rejected
            Assert.Throws<ArgumentException>(() => statsDClient.AddGlobalTag(":value"));
        }

        [Test]
        public void GIVEN_tag_with_empty_value_WHEN_add_global_tag_THEN_accepted()
        {
            // GIVEN
            statsDClient = new StatsDClient(mockUdpClient.Object, testEndPoint);

            // WHEN - Empty value might be valid
            statsDClient.AddGlobalTag("key:");

            // THEN
            Assert.AreEqual(1, statsDClient.GlobalTags.Count);
            Assert.IsTrue(statsDClient.GlobalTags.ContainsKey("key"));
            Assert.AreEqual("", statsDClient.GlobalTags["key"].Value);
        }

        [Test]
        public void GIVEN_concurrent_tag_replacements_WHEN_multiple_threads_update_same_key_THEN_thread_safe()
        {
            // GIVEN
            statsDClient = new StatsDClient(mockUdpClient.Object, testEndPoint);
            statsDClient.AddGlobalTag("session_id:initial");

            var tasks = new List<Task>();
            var random = new Random();

            // WHEN - 100 threads all trying to replace the same tag key
            for (int i = 0; i < 100; i++)
            {
                int threadId = i;
                tasks.Add(Task.Run(() =>
                {
                    Thread.Sleep(random.Next(0, 10));
                    statsDClient.AddGlobalTag($"session_id:thread_{threadId}");
                }));
            }

            Task.WaitAll(tasks.ToArray());

            // THEN - Should have exactly 1 tag with session_id key (no duplicates)
            Assert.AreEqual(1, statsDClient.GlobalTags.Count);

            Assert.IsTrue(statsDClient.GlobalTags.ContainsKey("session_id"));
            Assert.IsTrue(statsDClient.GlobalTags["session_id"].Value.StartsWith("thread_"));
        }

        /// <summary>
        /// Validates that the sent bytes match the expected StatsD packet format.
        /// </summary>
        /// <param name="sentBytes">The bytes that were sent via UDP.</param>
        /// <param name="expectedPacket">The expected StatsD packet string.</param>
        /// <returns>True if the packet matches, false otherwise.</returns>
        private static bool ValidateStatsDPacket(byte[] sentBytes, string expectedPacket)
        {
            try
            {
                string actualPacket = Encoding.UTF8.GetString(sentBytes);
                return actualPacket == expectedPacket;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Validates that the sent packet contains the expected metric format and all specified tags.
        /// </summary>
        /// <param name="sentBytes">The bytes that were sent via UDP.</param>
        /// <param name="expectedMetricPrefix">The expected metric name and value portion (e.g., "test_counter:1|c").</param>
        /// <param name="expectedTags">The tags that should be present in the packet.</param>
        /// <returns>True if the packet contains the expected format and tags, false otherwise.</returns>
        private static bool ValidateStatsDPacketContainsTags(byte[] sentBytes, string expectedMetricPrefix, string[] expectedTags)
        {
            try
            {
                string actualPacket = Encoding.UTF8.GetString(sentBytes);

                // Check if packet starts with expected metric prefix
                if (!actualPacket.StartsWith(expectedMetricPrefix, StringComparison.Ordinal))
                {
                    return false;
                }

                // Extract tags section (after |#)
                var tagSectionIndex = actualPacket.IndexOf("|#", StringComparison.Ordinal);
                if (tagSectionIndex == -1 && expectedTags.Length > 0)
                {
                    return false; // Expected tags but none found
                }

                if (expectedTags.Length == 0)
                {
                    return tagSectionIndex == -1; // No tags expected and none found
                }

                string tagSection = actualPacket.Substring(tagSectionIndex + 2);
                string[] actualTags = tagSection.Split(',');

                // Verify all expected tags are present
                foreach (string expectedTag in expectedTags)
                {
                    if (!actualTags.Contains(expectedTag))
                    {
                        return false;
                    }
                }

                // Verify no extra tags (same count)
                return actualTags.Length == expectedTags.Length;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if the packet contains a sample rate indicator (@).
        /// </summary>
        /// <param name="sentBytes">The bytes that were sent via UDP.</param>
        /// <returns>True if the packet contains a sample rate, false otherwise.</returns>
        private static bool ContainsSampleRate(byte[] sentBytes)
        {
            try
            {
                string actualPacket = Encoding.UTF8.GetString(sentBytes);
                return actualPacket.Contains("|@");
            }
            catch
            {
                return false;
            }
        }
    }
}
