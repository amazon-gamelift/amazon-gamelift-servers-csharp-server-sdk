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
    using System.Linq;
    using System.Net;
    using Aws.GameLift.Server;
    using Aws.GameLift.Server.Model.Metrics;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    public class MetricsTest
    {
        private Mock<IUdpClientWrapper> mockUdpClient;
        private Metrics metrics;
        private StatsDClient statsDClient;
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

            // Create metrics manager with StatsDClient for most tests
            statsDClient = new StatsDClient(mockUdpClient.Object, testEndPoint);
            metrics = Metrics.Create()
                .SetStatsDClient(statsDClient)
                .Build();
        }

        [TearDown]
        public void TearDown()
        {
            metrics?.Dispose();
        }

        [Test]
        public void GIVEN_valid_tag_WHEN_global_tag_added_THEN_tag_added()
        {
            // GIVEN
            string tag = "environment:test";

            // WHEN
            GenericOutcome outcome = metrics.AddGlobalTag(tag);

            // THEN
            Assert.IsTrue(outcome.Success);
            Assert.IsTrue(statsDClient.GlobalTags.ContainsKey("environment"));
            Assert.AreEqual("test", statsDClient.GlobalTags["environment"].Value);

        }

        [Test]
        public void GIVEN_whitespace_tag_WHEN_global_tag_added_THEN_returns_failed_outcome()
        {
            // GIVEN
            string tag = "    ";

            // WHEN
            GenericOutcome outcome = metrics.AddGlobalTag(tag);

            // THEN
            Assert.IsFalse(outcome.Success);
            Assert.IsNotNull(outcome.Error);
            Assert.AreEqual(GameLiftErrorType.VALIDATION_EXCEPTION, outcome.Error.ErrorType);
            Assert.That(outcome.Error.ErrorMessage, Does.Contain("Tag value cannot be null or empty"));
        }

        [Test]
        public void GIVEN_empty_tag_WHEN_global_tag_added_THEN_returns_failed_outcome()
        {
            // GIVEN
            string tag = string.Empty;

            // WHEN
            GenericOutcome outcome = metrics.AddGlobalTag(tag);

            // THEN
            Assert.IsFalse(outcome.Success);
            Assert.IsNotNull(outcome.Error);
            Assert.AreEqual(GameLiftErrorType.VALIDATION_EXCEPTION, outcome.Error.ErrorType);
            Assert.That(outcome.Error.ErrorMessage, Does.Contain("Tag value cannot be null or empty"));
        }

        [Test]
        public void GIVEN_null_tag_WHEN_global_tag_added_THEN_returns_failed_outcome()
        {
            // GIVEN
            string tag = null;

            // WHEN
            GenericOutcome outcome = metrics.AddGlobalTag(tag);

            // THEN
            Assert.IsFalse(outcome.Success);
            Assert.IsNotNull(outcome.Error);
            Assert.AreEqual(GameLiftErrorType.VALIDATION_EXCEPTION, outcome.Error.ErrorType);
            Assert.That(outcome.Error.ErrorMessage, Does.Contain("Tag value cannot be null or empty"));
        }

        [Test]
        public void GIVEN_valid_tag_list_WHEN_global_tag_added_THEN_tag_added()
        {
            // GIVEN
            var tags = new List<string> { "environment:test", "service:game" };

            // WHEN
            GenericOutcome outcome = metrics.AddGlobalTags(tags);

            // THEN
            Assert.IsTrue(outcome.Success);
            Assert.AreEqual(2, statsDClient.GlobalTags.Count);

            Assert.IsTrue(statsDClient.GlobalTags.ContainsKey("environment"));
            Assert.AreEqual("test", statsDClient.GlobalTags["environment"].Value);

            Assert.IsTrue(statsDClient.GlobalTags.ContainsKey("service"));
            Assert.AreEqual("game", statsDClient.GlobalTags["service"].Value);
        }

        [Test]
        public void GIVEN_whitespace_tag_in_list_WHEN_global_tag_added_THEN_returns_failed_outcome()
        {
            // GIVEN
            var tags = new List<string> { "environment:test", "    " };

            // WHEN
            GenericOutcome outcome = metrics.AddGlobalTags(tags);

            // THEN
            Assert.IsFalse(outcome.Success);
            Assert.IsNotNull(outcome.Error);
            Assert.AreEqual(GameLiftErrorType.VALIDATION_EXCEPTION, outcome.Error.ErrorType);
            Assert.That(outcome.Error.ErrorMessage, Does.Contain("Tag value cannot be null or empty"));
            Assert.AreEqual(1, statsDClient.GlobalTags.Count); // First tag should have been added
        }

        [Test]
        public void GIVEN_empty_tag_in_list_WHEN_global_tag_added_THEN_returns_failed_outcome()
        {
            // GIVEN
            var tags = new List<string> { "environment:test", string.Empty };

            // WHEN
            GenericOutcome outcome = metrics.AddGlobalTags(tags);

            // THEN
            Assert.IsFalse(outcome.Success);
            Assert.IsNotNull(outcome.Error);
            Assert.AreEqual(GameLiftErrorType.VALIDATION_EXCEPTION, outcome.Error.ErrorType);
            Assert.That(outcome.Error.ErrorMessage, Does.Contain("Tag value cannot be null or empty"));
            Assert.AreEqual(1, statsDClient.GlobalTags.Count); // First tag should have been added
        }

        [Test]
        public void GIVEN_null_tag_in_list_WHEN_global_tag_added_THEN_returns_failed_outcome()
        {
            // GIVEN
            var tags = new List<string> { "environment:test", null };

            // WHEN
            GenericOutcome outcome = metrics.AddGlobalTags(tags);

            // THEN
            Assert.IsFalse(outcome.Success);
            Assert.IsNotNull(outcome.Error);
            Assert.AreEqual(GameLiftErrorType.VALIDATION_EXCEPTION, outcome.Error.ErrorType);
            Assert.That(outcome.Error.ErrorMessage, Does.Contain("Tag value cannot be null or empty"));
            Assert.AreEqual(1, statsDClient.GlobalTags.Count); // First tag should have been added
        }

        [Test]
        public void GIVEN_valid_tag_WHEN_global_tag_removed_THEN_tag_removed()
        {
            // GIVEN
            string tagValue = "environment:test";
            string tagKey = "environment";
            GenericOutcome addOutcome = metrics.AddGlobalTag(tagValue);
            Assert.IsTrue(addOutcome.Success);
            Assert.AreEqual(1, statsDClient.GlobalTags.Count);

            // WHEN
            GenericOutcome removeOutcome = metrics.RemoveGlobalTag(tagKey);

            // THEN
            Assert.IsTrue(removeOutcome.Success);
            Assert.AreEqual(0, statsDClient.GlobalTags.Count);
        }

        [Test]
        public void GIVEN_whitespace_key_WHEN_global_tag_removed_THEN_returns_failed_outcome()
        {
            // GIVEN
            string key = "    ";

            // WHEN
            GenericOutcome outcome = metrics.RemoveGlobalTag(key);

            // THEN
            Assert.IsFalse(outcome.Success);
            Assert.IsNotNull(outcome.Error);
            Assert.AreEqual(GameLiftErrorType.VALIDATION_EXCEPTION, outcome.Error.ErrorType);
            Assert.That(outcome.Error.ErrorMessage, Does.Contain("Tag key cannot be null or empty"));
        }

        [Test]
        public void GIVEN_empty_key_WHEN_global_tag_removed_THEN_returns_failed_outcome()
        {
            // GIVEN
            string key = string.Empty;

            // WHEN
            GenericOutcome outcome = metrics.RemoveGlobalTag(key);

            // THEN
            Assert.IsFalse(outcome.Success);
            Assert.IsNotNull(outcome.Error);
            Assert.AreEqual(GameLiftErrorType.VALIDATION_EXCEPTION, outcome.Error.ErrorType);
            Assert.That(outcome.Error.ErrorMessage, Does.Contain("Tag key cannot be null or empty"));
        }

        [Test]
        public void GIVEN_null_key_WHEN_global_tag_removed_THEN_returns_failed_outcome()
        {
            // GIVEN
            string key = null;

            // WHEN
            GenericOutcome outcome = metrics.RemoveGlobalTag(key);

            // THEN
            Assert.IsFalse(outcome.Success);
            Assert.IsNotNull(outcome.Error);
            Assert.AreEqual(GameLiftErrorType.VALIDATION_EXCEPTION, outcome.Error.ErrorType);
            Assert.That(outcome.Error.ErrorMessage, Does.Contain("Tag key cannot be null or empty"));
        }

        [Test]
        public void GIVEN_metrics_manager_with_owned_client_WHEN_disposed_THEN_disposes_client()
        {
            // GIVEN
            metrics = Metrics.Create().Build();

            // WHEN & THEN
            Assert.DoesNotThrow(() => metrics.Dispose());
        }

        [Test]
        public void GIVEN_metrics_manager_WHEN_disposed_multiple_times_THEN_does_not_throw()
        {
            // WHEN & THEN
            Assert.DoesNotThrow(() => metrics.Dispose());
            Assert.DoesNotThrow(() => metrics.Dispose());
        }

        [Test]
        public void GIVEN_deleted_metric_WHEN_metric_with_same_name_created_THEN_creates_successfully()
        {
            // GIVEN
            string metricName = "test_metric";

            metrics.NewCounter(metricName).AddTag("environment:test").Build();
            metrics.DeleteMetric(metricName);

            // WHEN & THEN
            Assert.DoesNotThrow(() => metrics.NewGauge(metricName).AddTag("environment:test").Build());
        }

        [Test]
        public void GIVEN_existing_metric_WHEN_metric_deleted_THEN_returns_true()
        {
            // GIVEN
            string metricName = "test_metric";

            metrics.NewCounter(metricName).AddTag("environment:test").Build();

            // WHEN
            bool result = metrics.DeleteMetric(metricName);

            // THEN
            Assert.IsTrue(result);
        }

        [Test]
        public void GIVEN_non_existing_metric_WHEN_metric_deleted_THEN_returns_false()
        {
            // GIVEN
            string metricName = "non_existing_metric";

            // WHEN
            bool result = metrics.DeleteMetric(metricName);

            // THEN
            Assert.IsFalse(result);
        }

        [Test]
        public void GIVEN_null_metric_name_WHEN_metric_deleted_THEN_throws_argument_exception()
        {
            // GIVEN
            string nullName = null;

            // WHEN & THEN
            var exception = Assert.Throws<ArgumentException>(() => metrics.DeleteMetric(nullName));
            Assert.That(exception.Message, Does.Contain("Metric name cannot be null or empty"));
            Assert.AreEqual("name", exception.ParamName);
        }

        [Test]
        public void GIVEN_empty_metric_name_WHEN_metric_deleted_THEN_throws_argument_exception()
        {
            // GIVEN
            string emptyName = string.Empty;

            // WHEN & THEN
            var exception = Assert.Throws<ArgumentException>(() => metrics.DeleteMetric(emptyName));
            Assert.That(exception.Message, Does.Contain("Metric name cannot be null or empty"));
            Assert.AreEqual("name", exception.ParamName);
        }

        [Test]
        public void GIVEN_metrics_manager_WHEN_flush_all_metrics_THEN_server_up_gauge_set_to_one()
        {
            // Capture sent UDP packets
            var sentPackets = new List<string>();
            mockUdpClient.Setup(m => m.Send(It.IsAny<byte[]>(), It.IsAny<int>()))
                .Callback<byte[], int>((buffer, length) =>
                {
                    var text = System.Text.Encoding.UTF8.GetString(buffer, 0, length);
                    lock (sentPackets)
                    {
                        sentPackets.Add(text);
                    }
                })
                .Returns(1);

            // WHEN
            var outcome = metrics.FlushAllMetrics();
            Assert.IsTrue(outcome.Success, "FlushAllMetrics should succeed");

            // THEN - wait for asynchronous flush logic to complete
            bool found = System.Threading.SpinWait.SpinUntil(
                () =>
                        {
                            lock (sentPackets)
                            {
                                return sentPackets.Any(p => p.Split('\n').Any(line => line.StartsWith("server_up:1|g")));
                            }
                        }, TimeSpan.FromSeconds(2));

            Assert.IsTrue(found, "Expected 'up' gauge with value 1 to be flushed (pattern 'server.up:1|g').");
        }

        [Test]
        public void GIVEN_metrics_manager_WHEN_disposed_THEN_server_up_gauge_set_to_zero()
        {
            // Capture sent UDP packets
            var sentPackets = new List<string>();
            mockUdpClient.Reset();
            mockUdpClient.Setup(m => m.Connect(It.IsAny<IPEndPoint>()));
            mockUdpClient.Setup(m => m.Close());
            mockUdpClient.Setup(m => m.Send(It.IsAny<byte[]>(), It.IsAny<int>()))
                .Callback<byte[], int>((buffer, length) =>
                {
                    var text = System.Text.Encoding.UTF8.GetString(buffer, 0, length);
                    lock (sentPackets)
                    {
                        sentPackets.Add(text);
                    }
                })
                .Returns(1);

            // WHEN
            metrics.Dispose();

            // THEN - wait for asynchronous flush logic to complete
            bool found = System.Threading.SpinWait.SpinUntil(
                () =>
                        {
                            lock (sentPackets)
                            {
                                return sentPackets.Any(p => p.Split('\n').Any(line => line.StartsWith("server_up:0|g")));
                            }
                        }, TimeSpan.FromSeconds(2));

            Assert.IsTrue(found, "Expected 'up' gauge with value 0 to be flushed on dispose (pattern 'server_up:0|g').");
        }
    }
}
