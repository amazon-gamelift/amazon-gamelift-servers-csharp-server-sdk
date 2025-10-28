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

using Aws.GameLift.Server.Model.Metrics.DerivedMetrics;

namespace Aws.GameLift.Tests.Server
{
    using System;
    using System.Collections.Generic;
    using Aws.GameLift.Server;
    using Aws.GameLift.Server.Model.Metrics;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    public class MetricBuilderTest
    {
        private Mock<IStatsDClient> mockStatsDClient;
        private Metrics metrics;

        [SetUp]
        public void SetUp()
        {
            mockStatsDClient = new Mock<IStatsDClient>();

            mockStatsDClient.Setup(client => client.AddGlobalTag(It.IsAny<string>()));
            mockStatsDClient.Setup(client => client.RemoveGlobalTag(It.IsAny<string>()));
        }

        [TearDown]
        public void TearDown()
        {
            metrics?.Dispose();
        }

        [Test]
        public void GIVEN_valid_counter_name_and_tags_WHEN_counter_created_THEN_returns_counter_instance()
        {
            // GIVEN
            metrics = Metrics.Create().SetStatsDClient(mockStatsDClient.Object).Build();
            string counterName = "test_counter";
            var tags = new List<string> { "environment:test", "service:game" };

            // WHEN
            var counter = metrics.NewCounter(counterName)
                .AddTags(tags.ToArray())
                .Build();

            // THEN
            Assert.IsNotNull(counter);
            Assert.IsInstanceOf<ICounter>(counter);
        }

        [Test]
        public void GIVEN_valid_counter_name_with_no_tags_WHEN_counter_created_THEN_returns_counter_instance()
        {
            // GIVEN
            metrics = Metrics.Create().SetStatsDClient(mockStatsDClient.Object).Build();
            string counterName = "test_counter";

            // WHEN
            var counter = metrics.NewCounter(counterName).Build();

            // THEN
            Assert.IsNotNull(counter);
            Assert.IsInstanceOf<ICounter>(counter);
        }

        [Test]
        public void GIVEN_null_counter_name_WHEN_counter_created_THEN_throws_argument_exception()
        {
            // GIVEN
            metrics = Metrics.Create().SetStatsDClient(mockStatsDClient.Object).Build();
            string nullName = null;

            // WHEN & THEN
            var exception = Assert.Throws<ArgumentException>(() => metrics.NewCounter(nullName).Build());
            Assert.That(exception.Message, Does.Contain("Counter name cannot be null or empty"));
            Assert.AreEqual("name", exception.ParamName);
        }

        [Test]
        public void GIVEN_empty_counter_name_WHEN_counter_created_THEN_throws_argument_exception()
        {
            // GIVEN
            metrics = Metrics.Create().SetStatsDClient(mockStatsDClient.Object).Build();
            string emptyName = string.Empty;

            // WHEN & THEN
            var exception = Assert.Throws<ArgumentException>(() => metrics.NewCounter(emptyName).Build());
            Assert.That(exception.Message, Does.Contain("Counter name cannot be null or empty"));
            Assert.AreEqual("name", exception.ParamName);
        }

        [Test]
        public void GIVEN_whitespace_counter_name_WHEN_counter_created_THEN_throws_argument_exception()
        {
            // GIVEN
            metrics = Metrics.Create().SetStatsDClient(mockStatsDClient.Object).Build();
            string whitespaceName = "   ";

            // WHEN & THEN
            var exception = Assert.Throws<ArgumentException>(() => metrics.NewCounter(whitespaceName).Build());
            Assert.That(exception.Message, Does.Contain("Counter name cannot be null or empty"));
            Assert.AreEqual("name", exception.ParamName);
        }

        [Test]
        public void GIVEN_existing_counter_WHEN_counter_with_same_name_created_THEN_throws_invalid_operation_exception()
        {
            // GIVEN
            metrics = Metrics.Create().SetStatsDClient(mockStatsDClient.Object).Build();
            string counterName = "duplicate_counter";

            metrics.NewCounter(counterName).AddTag("environment:test").Build();

            // WHEN & THEN
            var exception = Assert.Throws<InvalidOperationException>(() =>
                metrics.NewCounter(counterName).Build());
            Assert.That(exception.Message, Does.Contain($"A metric with the name '{counterName}' already exists."));
        }

        [Test]
        public void GIVEN_counter_builder_WHEN_tags_and_sample_rate_set_THEN_creates_counter_with_configuration()
        {
            // GIVEN
            metrics = Metrics.Create().SetStatsDClient(mockStatsDClient.Object).Build();
            string counterName = "configured_counter";
            var sampleRate = new SampleFractional(0.5);

            // WHEN
            var counter = metrics.NewCounter(counterName)
                .AddTag("environment:test")
                .AddTag("service:game")
                .SetSampleRate(sampleRate)
                .Build();

            // THEN
            Assert.IsNotNull(counter);
            Assert.IsInstanceOf<ICounter>(counter);
        }

        [Test]
        public void GIVEN_counter_builder_WHEN_derived_set_THEN_creates_counter_with_configuration()
        {
            // GIVEN
            metrics = Metrics.Create().SetStatsDClient(mockStatsDClient.Object).Build();
            string counterName = "configured_counter";

            // WHEN
            var counter = metrics.NewCounter(counterName)
                .SetDerivedMetrics(new HashSet<IDerivedMetric>
                    { new Sum(), new Latest(), new Percentile(50) })
                .Build();

            // THEN
            Assert.IsNotNull(counter);
            Assert.IsInstanceOf<ICounter>(counter);
        }

        [Test]
        public void GIVEN_valid_gauge_name_and_tags_WHEN_gauge_created_THEN_returns_gauge_instance()
        {
            // GIVEN
            metrics = Metrics.Create().SetStatsDClient(mockStatsDClient.Object).Build();
            string gaugeName = "test_gauge";
            var tags = new List<string> { "environment:test", "service:game" };

            // WHEN
            var gauge = metrics.NewGauge(gaugeName)
                .AddTags(tags.ToArray())
                .Build();

            // THEN
            Assert.IsNotNull(gauge);
            Assert.IsInstanceOf<IGauge>(gauge);
        }

        [Test]
        public void GIVEN_valid_gauge_name_with_no_tags_WHEN_gauge_created_THEN_returns_gauge_instance()
        {
            // GIVEN
            metrics = Metrics.Create().SetStatsDClient(mockStatsDClient.Object).Build();
            string gaugeName = "test_gauge";

            // WHEN
            var gauge = metrics.NewGauge(gaugeName).Build();

            // THEN
            Assert.IsNotNull(gauge);
            Assert.IsInstanceOf<IGauge>(gauge);
        }

        [Test]
        public void GIVEN_null_gauge_name_WHEN_gauge_created_THEN_throws_argument_exception()
        {
            // GIVEN
            metrics = Metrics.Create().SetStatsDClient(mockStatsDClient.Object).Build();
            string nullName = null;

            // WHEN & THEN
            var exception = Assert.Throws<ArgumentException>(() => metrics.NewGauge(nullName).Build());
            Assert.That(exception.Message, Does.Contain("Gauge name cannot be null or empty"));
            Assert.AreEqual("name", exception.ParamName);
        }

        [Test]
        public void GIVEN_empty_gauge_name_WHEN_gauge_created_THEN_throws_argument_exception()
        {
            // GIVEN
            metrics = Metrics.Create().SetStatsDClient(mockStatsDClient.Object).Build();
            string emptyName = string.Empty;

            // WHEN & THEN
            var exception = Assert.Throws<ArgumentException>(() => metrics.NewGauge(emptyName).Build());
            Assert.That(exception.Message, Does.Contain("Gauge name cannot be null or empty"));
            Assert.AreEqual("name", exception.ParamName);
        }

        [Test]
        public void GIVEN_whitespace_gauge_name_WHEN_gauge_created_THEN_throws_argument_exception()
        {
            // GIVEN
            metrics = Metrics.Create().SetStatsDClient(mockStatsDClient.Object).Build();
            string whitespaceName = "   ";

            // WHEN & THEN
            var exception = Assert.Throws<ArgumentException>(() => metrics.NewGauge(whitespaceName).Build());
            Assert.That(exception.Message, Does.Contain("Gauge name cannot be null or empty"));
            Assert.AreEqual("name", exception.ParamName);
        }

        [Test]
        public void GIVEN_existing_counter_WHEN_gauge_with_same_name_created_THEN_throws_invalid_operation_exception()
        {
            // GIVEN
            metrics = Metrics.Create().SetStatsDClient(mockStatsDClient.Object).Build();
            string metricName = "shared_name";

            metrics.NewCounter(metricName).AddTag("environment:test").Build();

            // WHEN & THEN
            var exception = Assert.Throws<InvalidOperationException>(() =>
                metrics.NewGauge(metricName).Build());
            Assert.That(exception.Message, Does.Contain($"A metric with the name '{metricName}' already exists."));
        }

        [Test]
        public void GIVEN_gauge_builder_WHEN_tags_and_sample_rate_set_THEN_creates_gauge_with_configuration()
        {
            // GIVEN
            metrics = Metrics.Create().SetStatsDClient(mockStatsDClient.Object).Build();
            string gaugeName = "configured_gauge";
            var sampleRate = new SampleFractional(0.3);

            // WHEN
            var gauge = metrics.NewGauge(gaugeName)
                .AddTag("environment:test")
                .AddTag("service:game")
                .SetSampleRate(sampleRate)
                .Build();

            // THEN
            Assert.IsNotNull(gauge);
            Assert.IsInstanceOf<IGauge>(gauge);
        }

        [Test]
        public void GIVEN_valid_timer_name_and_tags_WHEN_timer_created_THEN_returns_timer_instance()
        {
            // GIVEN
            metrics = Metrics.Create().SetStatsDClient(mockStatsDClient.Object).Build();
            string timerName = "test_timer";
            var tags = new List<string> { "environment:test", "service:game" };

            // WHEN
            var timer = metrics.NewTimer(timerName)
                .AddTags(tags.ToArray())
                .Build();

            // THEN
            Assert.IsNotNull(timer);
            Assert.IsInstanceOf<ITimer>(timer);
        }

        [Test]
        public void GIVEN_valid_timer_name_with_no_tags_WHEN_timer_created_THEN_returns_timer_instance()
        {
            // GIVEN
            metrics = Metrics.Create().SetStatsDClient(mockStatsDClient.Object).Build();
            string timerName = "test_timer";

            // WHEN
            var timer = metrics.NewTimer(timerName).Build();

            // THEN
            Assert.IsNotNull(timer);
            Assert.IsInstanceOf<ITimer>(timer);
        }

        [Test]
        public void GIVEN_null_timer_name_WHEN_timer_created_THEN_throws_argument_exception()
        {
            // GIVEN
            metrics = Metrics.Create().SetStatsDClient(mockStatsDClient.Object).Build();
            string nullName = null;

            // WHEN & THEN
            var exception = Assert.Throws<ArgumentException>(() => metrics.NewTimer(nullName).Build());
            Assert.That(exception.Message, Does.Contain("Timer name cannot be null or empty"));
            Assert.AreEqual("name", exception.ParamName);
        }

        [Test]
        public void GIVEN_empty_timer_name_WHEN_timer_created_THEN_throws_argument_exception()
        {
            // GIVEN
            metrics = Metrics.Create().SetStatsDClient(mockStatsDClient.Object).Build();
            string emptyName = string.Empty;

            // WHEN & THEN
            var exception = Assert.Throws<ArgumentException>(() => metrics.NewTimer(emptyName).Build());
            Assert.That(exception.Message, Does.Contain("Timer name cannot be null or empty"));
            Assert.AreEqual("name", exception.ParamName);
        }

        [Test]
        public void GIVEN_whitespace_timer_name_WHEN_timer_created_THEN_throws_argument_exception()
        {
            // GIVEN
            metrics = Metrics.Create().SetStatsDClient(mockStatsDClient.Object).Build();
            string whitespaceName = "   ";

            // WHEN & THEN
            var exception = Assert.Throws<ArgumentException>(() => metrics.NewTimer(whitespaceName).Build());
            Assert.That(exception.Message, Does.Contain("Timer name cannot be null or empty"));
            Assert.AreEqual("name", exception.ParamName);
        }

        [Test]
        public void GIVEN_timer_builder_WHEN_tags_and_sample_rate_set_THEN_creates_timer_with_configuration()
        {
            // GIVEN
            metrics = Metrics.Create().SetStatsDClient(mockStatsDClient.Object).Build();
            string timerName = "configured_timer";
            var sampleRate = new SampleFractional(0.8);

            // WHEN
            var timer = metrics.NewTimer(timerName)
                .AddTag("environment:test")
                .AddTag("service:game")
                .SetSampleRate(sampleRate)
                .Build();

            // THEN
            Assert.IsNotNull(timer);
            Assert.IsInstanceOf<ITimer>(timer);
        }

        [Test]
        public void GIVEN_counter_builder_WHEN_fluent_configuration_used_THEN_creates_properly_configured_counter()
        {
            // GIVEN
            metrics = Metrics.Create().SetStatsDClient(mockStatsDClient.Object).Build();

            // WHEN
            var counter = metrics.NewCounter("fluent_counter")
                .AddTag("environment:test")
                .AddTag("service:gamelift")
                .AddTags("region:us-east-1", "instance:i-123456")
                .SetSampleRate(new SampleFractional(0.5))
                .Build();

            // THEN
            Assert.IsNotNull(counter);
            Assert.IsInstanceOf<ICounter>(counter);
        }

        [Test]
        public void GIVEN_gauge_builder_WHEN_fluent_configuration_used_THEN_creates_properly_configured_gauge()
        {
            // GIVEN
            metrics = Metrics.Create().SetStatsDClient(mockStatsDClient.Object).Build();

            // WHEN
            var gauge = metrics.NewGauge("fluent_gauge")
                .AddTag("metric_type:gauge")
                .AddTags("component:server", "version:1.0")
                .SetSampleRate(new SampleFractional(1.0))
                .Build();

            // THEN
            Assert.IsNotNull(gauge);
            Assert.IsInstanceOf<IGauge>(gauge);
        }

        [Test]
        public void GIVEN_timer_builder_WHEN_fluent_configuration_used_THEN_creates_properly_configured_timer()
        {
            // GIVEN
            metrics = Metrics.Create().SetStatsDClient(mockStatsDClient.Object).Build();

            // WHEN
            var timer = metrics.NewTimer("fluent_timer")
                .AddTag("operation:game_session")
                .AddTags("priority:high", "timeout:30s")
                .SetSampleRate(new SampleFractional(0.75))
                .Build();

            // THEN
            Assert.IsNotNull(timer);
            Assert.IsInstanceOf<ITimer>(timer);
        }

        [Test]
        public void GIVEN_builder_with_null_or_empty_tags_WHEN_added_THEN_ignores_invalid_tags()
        {
            // GIVEN
            metrics = Metrics.Create().SetStatsDClient(mockStatsDClient.Object).Build();

            // WHEN & THEN - Should not throw and should ignore null/empty tags
            Assert.DoesNotThrow(() =>
            {
                var counter = metrics.NewCounter("test_counter")
                    .AddTag("valid:tag")
                    .AddTag(null)
                    .AddTag(string.Empty)
                    .AddTag("   ")
                    .AddTag("another:valid")
                    .Build();

                Assert.IsNotNull(counter);
            });
        }
    }
}
