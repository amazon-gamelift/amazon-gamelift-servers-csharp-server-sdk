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

namespace Aws.GameLift.Tests.Server.Model.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Aws.GameLift.Server;
    using Aws.GameLift.Server.Model.Metrics;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    public class MetricsTest
    {
        // Deterministic seed for reproducible sampling tests (first random value: 0.06674693481379511)
        private const int TestSeed = 12345;

        private Mock<IStatsDClient> mockStatsDClient;
        private string metricName;
        private IList<Tag> tags;

        [SetUp]
        public void Setup()
        {
            mockStatsDClient = new Mock<IStatsDClient>();
            metricName = "test_metric";
            tags = new List<Tag>();
            tags.Add(new Tag("tag1:value1"));
            tags.Add(new Tag("tag2:value2"));
        }

        [Test]
        public void GIVEN_valid_sample_rate_WHEN_metric_created_THEN_sample_rate_is_set_correctly()
        {
            // GIVEN
            var expectedSampleRate = new SampleFractional(0.5);

            // WHEN
            var metric = new TestMetric(metricName, tags, mockStatsDClient.Object, expectedSampleRate);

            // THEN
            Assert.AreEqual(expectedSampleRate, metric.SampleRate);
            Assert.AreEqual(0.5, metric.SampleRate.ToDouble());
        }

        [Test]
        public void GIVEN_sample_all_WHEN_metric_created_THEN_sample_rate_is_one()
        {
            // GIVEN
            var sampleRate = SampleAll.Instance;

            // WHEN
            var metric = new TestMetric(metricName, tags, mockStatsDClient.Object, sampleRate);

            // THEN
            Assert.AreEqual(sampleRate, metric.SampleRate);
            Assert.AreEqual(1.0, metric.SampleRate.ToDouble());
        }

        [Test]
        public void GIVEN_null_sample_rate_WHEN_metric_created_THEN_sample_rate_defaults_to_sample_all()
        {
            // GIVEN
            SampleRate nullSampleRate = null;

            // WHEN
            var metric = new TestMetric(metricName, tags, mockStatsDClient.Object, nullSampleRate);

            // THEN
            Assert.AreEqual(SampleAll.Instance, metric.SampleRate);
            Assert.AreEqual(1.0, metric.SampleRate.ToDouble());
        }

        [Test]
        public void GIVEN_sample_fractional_with_invalid_rate_WHEN_created_THEN_throws_argument_out_of_range_exception()
        {
            // GIVEN & WHEN & THEN
            Assert.Throws<ArgumentOutOfRangeException>(() => new SampleFractional(1.5));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SampleFractional(-0.1));
        }

        [Test]
        public void GIVEN_sample_rate_WHEN_implicit_conversion_to_double_THEN_returns_correct_value()
        {
            // GIVEN
            var sampleAll = SampleAll.Instance;
            var sampleFractional = new SampleFractional(0.25);

            // WHEN
            double allValue = sampleAll;
            double fractionalValue = sampleFractional;

            // THEN
            Assert.AreEqual(1.0, allValue);
            Assert.AreEqual(0.25, fractionalValue);
        }

        [Test]
        public void GIVEN_metric_with_sample_rate_zero_WHEN_values_added_THEN_no_samples_recorded()
        {
            // GIVEN
            var zeroSampleRate = new SampleFractional(0.0);
            var counter = new Counter(metricName, tags, mockStatsDClient.Object, zeroSampleRate, null, TestSeed);

            // WHEN
            counter.Add(10);
            counter.Add(20);
            counter.Add(30);
            counter.Flush();

            // THEN - With 0% sample rate, no samples should be recorded
            mockStatsDClient.Verify(
                client => client.Increment(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<IList<Tag>>(), It.IsAny<SampleRate>()),
                Times.Never);
        }

        [Test]
        public void GIVEN_metric_with_sample_rate_point_one_WHEN_values_added_THEN_only_qualifying_samples_recorded()
        {
            // GIVEN - 0.1 sample rate means only randomly generated values < 0.1 are sampled
            var lowSampleRate = new SampleFractional(0.1);
            var counter = new Counter(metricName, tags, mockStatsDClient.Object, lowSampleRate, null, TestSeed);

            // WHEN - Add 6 values, with TestSeed only first two should sample (0.067 < 0.1, 0.070 < 0.1)
            counter.Add(10); // Random: 0.067 - samples
            counter.Add(20); // Random: 0.070 - samples
            counter.Add(30); // Random: 0.775 - doesn't sample
            counter.Add(40); // Random: 0.511 - doesn't sample
            counter.Add(50); // Random: 0.797 - doesn't sample
            counter.Add(60); // Random: 0.827 - doesn't sample
            counter.Flush();

            // THEN - Only first two values should be sampled (10 + 20 = 30)
            mockStatsDClient.Verify(
                client => client.Increment(metricName, 30, tags, lowSampleRate),
                Times.Once);
        }

        [Test]
        public void GIVEN_metric_with_sample_rate_point_eight_WHEN_values_added_THEN_most_samples_recorded()
        {
            // GIVEN - 0.8 sample rate means values < 0.8 are sampled
            var highSampleRate = new SampleFractional(0.8);
            var counter = new Counter(metricName, tags, mockStatsDClient.Object, highSampleRate, null, TestSeed);

            // WHEN - Add 6 values, with TestSeed first 4 should sample (all < 0.8 except 0.827)
            counter.Add(10); // Random: 0.067 - samples
            counter.Add(20); // Random: 0.070 - samples
            counter.Add(30); // Random: 0.775 - samples
            counter.Add(40); // Random: 0.511 - samples
            counter.Add(50); // Random: 0.797 - samples (0.797 < 0.8)
            counter.Add(60); // Random: 0.827 - doesn't sample (0.827 > 0.8)
            counter.Flush();

            // THEN - First 5 values should be sampled (10 + 20 + 30 + 40 + 50 = 150)
            mockStatsDClient.Verify(
                client => client.Increment(metricName, 150, tags, highSampleRate),
                Times.Once);
        }

        [Test]
        public void GIVEN_gauge_with_fractional_sample_rate_WHEN_values_set_THEN_sampling_applied_correctly()
        {
            // GIVEN
            var sampleRate = new SampleFractional(0.1);
            var gauge = new Gauge(metricName, tags, mockStatsDClient.Object, sampleRate, null, TestSeed);

            // WHEN - Set 3 values, only first two should sample with TestSeed
            gauge.Set(100); // Random: 0.067 - samples
            gauge.Set(200); // Random: 0.070 - samples
            gauge.Set(300); // Random: 0.775 - doesn't sample
            gauge.Flush();

            // THEN - Latest derived metric should use last sampled value (200)
            mockStatsDClient.Verify(
                client => client.Gauge(metricName, 200, tags, sampleRate),
                Times.Once);
        }

        [Test]
        public void GIVEN_timer_with_fractional_sample_rate_WHEN_values_recorded_THEN_sampling_applied_correctly()
        {
            // GIVEN
            var sampleRate = new SampleFractional(0.1);
            var timer = new Aws.GameLift.Server.Model.Metrics.Timer(metricName, tags, mockStatsDClient.Object, sampleRate, null, TestSeed);

            // WHEN - Record 3 values, only first two should sample with TestSeed
            timer.Set(50);  // Random: 0.067 - samples
            timer.Set(100); // Random: 0.070 - samples
            timer.Set(150); // Random: 0.775 - doesn't sample
            timer.Flush();

            // THEN - Mean derived metric should use sampled values ((50 + 100) / 2 = 75)
            mockStatsDClient.Verify(
                client => client.Timing(metricName, 75, tags, sampleRate),
                Times.Once);
        }

        [Test]
        public void GIVEN_metric_with_sample_all_WHEN_values_added_THEN_all_samples_recorded()
        {
            // GIVEN
            var counter = new Counter(metricName, tags, mockStatsDClient.Object, SampleAll.Instance, null, TestSeed);

            // WHEN - Add multiple values
            counter.Add(10);
            counter.Add(20);
            counter.Add(30);
            counter.Flush();

            // THEN - All samples should be recorded regardless of random values
            mockStatsDClient.Verify(
                client => client.Increment(metricName, 60, tags, SampleAll.Instance),
                Times.Once);
        }

        [Test]
        public void GIVEN_counter_WHEN_add_called_with_value_THEN_add_called_with_provided_value()
        {
            // GIVEN
            int incrementValue = 42;
            var counter = new Counter(metricName, tags, mockStatsDClient.Object, SampleAll.Instance, null, TestSeed);

            // WHEN
            GenericOutcome addOutcome = counter.Add(incrementValue);
            GenericOutcome flushOutcome = counter.Flush();

            // THEN
            Assert.IsTrue(addOutcome.Success);
            Assert.IsTrue(flushOutcome.Success);
            mockStatsDClient.Verify(
                client => client.Increment(metricName, incrementValue, tags, SampleAll.Instance),
                Times.Once);
        }

        [Test]
        public void GIVEN_counter_WHEN_increment_called_THEN_add_called_with_set_value()
        {
            // GIVEN
            var counter = new Counter(metricName, tags, mockStatsDClient.Object, SampleAll.Instance, null, TestSeed);

            // WHEN
            GenericOutcome incrementOutcome = counter.Increment();
            GenericOutcome flushOutcome = counter.Flush();

            // THEN
            Assert.IsTrue(incrementOutcome.Success);
            Assert.IsTrue(flushOutcome.Success);
            mockStatsDClient.Verify(
                client => client.Increment(metricName, 1, tags, SampleAll.Instance),
                Times.Once);
        }

        [Test]
        public void GIVEN_counter_WHEN_add_called_with_negative_value_THEN_returns_failed_outcome()
        {
            // GIVEN
            int incrementValue = -10;
            var counter = new Counter(metricName, tags, mockStatsDClient.Object, SampleAll.Instance, null, null);

            // WHEN
            GenericOutcome outcome = counter.Add(incrementValue);

            // THEN
            Assert.IsFalse(outcome.Success);
            Assert.IsNotNull(outcome.Error);
            Assert.AreEqual(GameLiftErrorType.VALIDATION_EXCEPTION, outcome.Error.ErrorType);
        }

        [Test]
        public void GIVEN_counter_with_fractional_sample_rate_WHEN_add_called_THEN_sample_rate_is_passed_correctly()
        {
            // GIVEN
            var customSampleRate = new SampleFractional(0.25);
            int incrementValue = 42;
            var counter = new Counter(metricName, tags, mockStatsDClient.Object, customSampleRate, null, TestSeed);

            // WHEN
            GenericOutcome addOutcome = counter.Add(incrementValue);
            GenericOutcome flushOutcome = counter.Flush();

            // THEN
            Assert.IsTrue(addOutcome.Success);
            Assert.IsTrue(flushOutcome.Success);
            // With seed 12345 and rate 0.25, this specific call should sample (0.067 < 0.25)
            mockStatsDClient.Verify(
                client => client.Increment(metricName, incrementValue, tags, customSampleRate),
                Times.Once);
        }

        [Test]
        public void GIVEN_counter_with_fractional_sample_rate_WHEN_increment_called_THEN_sample_rate_is_passed_correctly()
        {
            // GIVEN
            var customSampleRate = new SampleFractional(0.25);
            var counter = new Counter(metricName, tags, mockStatsDClient.Object, customSampleRate, null, TestSeed);

            // WHEN
            GenericOutcome incrementOutcome = counter.Increment();
            GenericOutcome flushOutcome = counter.Flush();

            // THEN
            Assert.IsTrue(incrementOutcome.Success);
            Assert.IsTrue(flushOutcome.Success);
            // With seed 12345 and rate 0.25, this specific call should sample (0.067 < 0.25)
            mockStatsDClient.Verify(
                client => client.Increment(metricName, 1, tags, customSampleRate),
                Times.Once);
        }

        [Test]
        public void GIVEN_gauge_WHEN_set_called_with_positive_value_THEN_gauge_called_with_value()
        {
            // GIVEN
            double gaugeValue = 123.45;
            var gauge = new Gauge(metricName, tags, mockStatsDClient.Object, SampleAll.Instance, null, TestSeed);

            // WHEN
            GenericOutcome setOutcome = gauge.Set(gaugeValue);
            GenericOutcome flushOutcome = gauge.Flush();

            // THEN
            Assert.IsTrue(setOutcome.Success);
            Assert.IsTrue(flushOutcome.Success);
            mockStatsDClient.Verify(
                client => client.Gauge(metricName, gaugeValue, tags, SampleAll.Instance),
                Times.Once);
        }

        [Test]
        public void GIVEN_gauge_WHEN_set_called_with_negative_value_THEN_gauge_called_with_value()
        {
            // GIVEN
            double gaugeValue = -98.76;
            var gauge = new Gauge(metricName, tags, mockStatsDClient.Object, SampleAll.Instance, null, TestSeed);

            // WHEN
            GenericOutcome setOutcome = gauge.Set(gaugeValue);
            GenericOutcome flushOutcome = gauge.Flush();

            // THEN
            Assert.IsTrue(setOutcome.Success);
            Assert.IsTrue(flushOutcome.Success);
            mockStatsDClient.Verify(
                client => client.Gauge(metricName, gaugeValue, tags, SampleAll.Instance),
                Times.Once);
        }

        [Test]
        public void GIVEN_gauge_WHEN_set_called_with_zero_value_THEN_gauge_called_with_zero()
        {
            // GIVEN
            double gaugeValue = 0;
            var gauge = new Gauge(metricName, tags, mockStatsDClient.Object, SampleAll.Instance, null, TestSeed);

            // WHEN
            GenericOutcome setOutcome = gauge.Set(gaugeValue);
            GenericOutcome flushOutcome = gauge.Flush();

            // THEN
            Assert.IsTrue(setOutcome.Success);
            Assert.IsTrue(flushOutcome.Success);
            mockStatsDClient.Verify(
                client => client.Gauge(metricName, gaugeValue, tags, SampleAll.Instance),
                Times.Once);
        }

        [Test]
        public void GIVEN_gauge_WHEN_add_called_with_positive_value_THEN_gauge_called_with_delta_true()
        {
            // GIVEN
            double addValue = 50.5;
            var gauge = new Gauge(metricName, tags, mockStatsDClient.Object, SampleAll.Instance, null, TestSeed);

            // WHEN
            GenericOutcome addOutcome = gauge.Add(addValue);
            GenericOutcome flushOutcome = gauge.Flush();

            // THEN
            Assert.IsTrue(addOutcome.Success);
            Assert.IsTrue(flushOutcome.Success);
            mockStatsDClient.Verify(
                client => client.Gauge(metricName, addValue, tags, SampleAll.Instance),
                Times.Once);
        }

        [Test]
        public void GIVEN_gauge_WHEN_add_called_with_negative_value_THEN_gauge_called_with_negative_delta()
        {
            // GIVEN
            double addValue = -25.3;
            var gauge = new Gauge(metricName, tags, mockStatsDClient.Object, SampleAll.Instance, null, TestSeed);

            // WHEN
            GenericOutcome addOutcome = gauge.Add(addValue);
            GenericOutcome flushOutcome = gauge.Flush();

            // THEN
            Assert.IsTrue(addOutcome.Success);
            Assert.IsTrue(flushOutcome.Success);
            mockStatsDClient.Verify(
                client => client.Gauge(metricName, addValue, tags, SampleAll.Instance),
                Times.Once);
        }

        [Test]
        public void GIVEN_gauge_WHEN_subtract_called_with_positive_value_THEN_gauge_called_with_negative_delta()
        {
            // GIVEN
            double subtractValue = 30.7;
            var gauge = new Gauge(metricName, tags, mockStatsDClient.Object, SampleAll.Instance, null, TestSeed);

            // WHEN
            GenericOutcome subtractOutcome = gauge.Subtract(subtractValue);
            GenericOutcome flushOutcome = gauge.Flush();

            // THEN
            Assert.IsTrue(subtractOutcome.Success);
            Assert.IsTrue(flushOutcome.Success);
            mockStatsDClient.Verify(
                client => client.Gauge(metricName, -subtractValue, tags, SampleAll.Instance),
                Times.Once);
        }

        [Test]
        public void GIVEN_gauge_WHEN_subtract_called_with_negative_value_THEN_gauge_called_with_positive_delta()
        {
            // GIVEN
            double subtractValue = -15.2;
            var gauge = new Gauge(metricName, tags, mockStatsDClient.Object, SampleAll.Instance, null, TestSeed);

            // WHEN
            GenericOutcome subtractOutcome = gauge.Subtract(subtractValue);
            GenericOutcome flushOutcome = gauge.Flush();

            // THEN
            Assert.IsTrue(subtractOutcome.Success);
            Assert.IsTrue(flushOutcome.Success);
            mockStatsDClient.Verify(
                client => client.Gauge(metricName, -subtractValue, tags, SampleAll.Instance),
                Times.Once);
        }

        [Test]
        public void GIVEN_gauge_WHEN_reset_called_THEN_gauge_called_with_zero()
        {
            // GIVEN
            var gauge = new Gauge(metricName, tags, mockStatsDClient.Object, SampleAll.Instance, null, TestSeed);

            // WHEN
            GenericOutcome resetOutcome = gauge.Reset();
            GenericOutcome flushOutcome = gauge.Flush();

            // THEN
            Assert.IsTrue(resetOutcome.Success);
            Assert.IsTrue(flushOutcome.Success);
            mockStatsDClient.Verify(
                client => client.Gauge(metricName, 0, tags, SampleAll.Instance),
                Times.Once);
        }

        [Test]
        public void GIVEN_gauge_WHEN_increment_called_THEN_gauge_called_with_one_delta()
        {
            // GIVEN
            var gauge = new Gauge(metricName, tags, mockStatsDClient.Object, SampleAll.Instance, null, TestSeed);

            // WHEN
            GenericOutcome incrementOutcome = gauge.Increment();
            GenericOutcome flushOutcome = gauge.Flush();

            // THEN
            Assert.IsTrue(incrementOutcome.Success);
            Assert.IsTrue(flushOutcome.Success);
            mockStatsDClient.Verify(
                client => client.Gauge(metricName, 1, tags, SampleAll.Instance),
                Times.Once);
        }

        [Test]
        public void GIVEN_gauge_WHEN_decrement_called_THEN_gauge_called_with_negative_one_delta()
        {
            // GIVEN
            var gauge = new Gauge(metricName, tags, mockStatsDClient.Object, SampleAll.Instance, null, TestSeed);

            // WHEN
            GenericOutcome decrementOutcome = gauge.Decrement();
            GenericOutcome flushOutcome = gauge.Flush();

            // THEN
            Assert.IsTrue(decrementOutcome.Success);
            Assert.IsTrue(flushOutcome.Success);
            mockStatsDClient.Verify(
                client => client.Gauge(metricName, -1, tags, SampleAll.Instance),
                Times.Once);
        }

        [Test]
        public void GIVEN_timer_WHEN_record_called_with_positive_value_THEN_timing_called_with_value()
        {
            // GIVEN
            long timingValue = 250;
            var timer = new Aws.GameLift.Server.Model.Metrics.Timer(metricName, tags, mockStatsDClient.Object, SampleAll.Instance, null, TestSeed);

            // WHEN
            GenericOutcome setOutcome = timer.Set(timingValue);
            GenericOutcome flushOutcome = timer.Flush();

            // THEN
            Assert.IsTrue(setOutcome.Success);
            Assert.IsTrue(flushOutcome.Success);
            mockStatsDClient.Verify(
                client => client.Timing(metricName, timingValue, tags, SampleAll.Instance),
                Times.Once);
        }

        [Test]
        public void GIVEN_timer_WHEN_record_called_with_zero_value_THEN_timing_called_with_zero()
        {
            // GIVEN
            long timingValue = 0;
            var timer = new Aws.GameLift.Server.Model.Metrics.Timer(metricName, tags, mockStatsDClient.Object, SampleAll.Instance, null, TestSeed);

            // WHEN
            GenericOutcome setOutcome = timer.Set(timingValue);
            GenericOutcome flushOutcome = timer.Flush();

            // THEN
            Assert.IsTrue(setOutcome.Success);
            Assert.IsTrue(flushOutcome.Success);
            mockStatsDClient.Verify(
                client => client.Timing(metricName, timingValue, tags, SampleAll.Instance),
                Times.Once);
        }

        [Test]
        public void GIVEN_timer_with_custom_sample_rate_WHEN_record_called_THEN_sample_rate_is_passed_correctly()
        {
            // GIVEN
            var customSampleRate = new SampleFractional(0.75);
            long timingValue = 250;
            var timer = new Aws.GameLift.Server.Model.Metrics.Timer(metricName, tags, mockStatsDClient.Object, customSampleRate, null, TestSeed);

            // WHEN
            GenericOutcome setOutcome = timer.Set(timingValue);
            GenericOutcome flushOutcome = timer.Flush();

            // THEN
            Assert.IsTrue(setOutcome.Success);
            Assert.IsTrue(flushOutcome.Success);
            mockStatsDClient.Verify(
                client => client.Timing(metricName, timingValue, tags, customSampleRate),
                Times.Once);
        }

        [Test]
#pragma warning disable S3776
#pragma warning disable S131
        public void GIVEN_metric_WHEN_concurrent_mixed_operations_THEN_operations_are_atomic()
        {
            // GIVEN
            var metric = new ThreadSafetyTestMetric(metricName, tags, mockStatsDClient.Object, SampleAll.Instance);
            const int threadCount = 6;
            var tasks = new Task[threadCount];
            var barrier = new Barrier(threadCount);
            var completedOperations = 0;

            // WHEN
            for (int i = 0; i < threadCount; i++)
            {
                int threadId = i;
                tasks[i] = Task.Run(() =>
                {
                    barrier.SignalAndWait(); // Synchronize start
                    switch (threadId % 3)
                    {
                        case 0:
                            for (int j = 0; j < 25; j++)
                            {
                                metric.TestAdjustValue(1.0);
                                Interlocked.Increment(ref completedOperations);
                            }

                            break;
                        case 1:
                            for (int j = 0; j < 25; j++)
                            {
                                metric.TestAdjustValue(2.0);
                                Interlocked.Increment(ref completedOperations);
                            }

                            break;
                        case 2:
                            for (int j = 0; j < 25; j++)
                            {
                                metric.TestAdjustValue(-0.5);
                                Interlocked.Increment(ref completedOperations);
                            }

                            break;
                    }
                });
            }

            Task.WaitAll(tasks);

            // THEN
            // Verify all operations completed
            Assert.AreEqual(threadCount * 25, completedOperations);

            // Verify expected result: (25 * 1) + (25 * 2) + (25 * 2) + (25 * 1) + (25 * 2) + (25 * -0.5)
            // = 25 + 50 + 50 + 25 + 50 - 12.5 = 187.5
            double expectedValue = (2 * 25 * 1.0) + (2 * 25 * 2.0) + (2 * 25 * -0.5);
            Assert.AreEqual(expectedValue, metric.CurrentValue, 0.001);
        }
#pragma warning restore S3776
#pragma warning restore S131

        [Test]
        public void GIVEN_metric_WHEN_concurrent_read_and_write_operations_THEN_reads_are_consistent()
        {
            // GIVEN
            var metric = new ThreadSafetyTestMetric(metricName, tags, mockStatsDClient.Object, SampleAll.Instance);
            const int readerCount = 3;
            const int writerCount = 2;
            var readerTasks = new Task[readerCount];
            var writerTasks = new Task[writerCount];
            var readValues = new List<double>[readerCount];
            var barrier = new Barrier(readerCount + writerCount);

            for (int i = 0; i < readerCount; i++)
            {
                readValues[i] = new List<double>();
            }

            // WHEN
            // Start reader tasks
            for (int i = 0; i < readerCount; i++)
            {
                int readerId = i;
                readerTasks[i] = Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    for (int j = 0; j < 100; j++)
                    {
                        readValues[readerId].Add(metric.CurrentValue);
                        Thread.Sleep(1); // Small delay to interleave with writes
                    }
                });
            }

            // Start writer tasks
            for (int i = 0; i < writerCount; i++)
            {
                int writerId = i;
                writerTasks[i] = Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    for (int j = 0; j < 50; j++)
                    {
                        metric.TestSetValue(writerId * 50 + j);
                        Thread.Sleep(2); // Small delay
                    }
                });
            }

            Task.WaitAll(readerTasks.Concat(writerTasks).ToArray());

            // THEN
            // Verify that all read values are valid (no torn reads or inconsistent states)
            foreach (var readerValueList in readValues)
            {
                Assert.That(readerValueList.Count, Is.EqualTo(100));
                foreach (var value in readerValueList)
                {
                    Assert.That(value, Is.GreaterThanOrEqualTo(0));
                }
            }
        }

        [Test]
        public void GIVEN_two_tags_with_same_value_WHEN_compared_THEN_they_are_equal()
        {
            // GIVEN
            var tag1 = new Tag("environment:production");
            var tag2 = new Tag("environment:production");

            // WHEN & THEN
            Assert.AreEqual(tag1, tag2);
            Assert.AreEqual(tag1.GetHashCode(), tag2.GetHashCode());
        }

        [Test]
        public void GIVEN_two_tags_with_different_values_WHEN_compared_THEN_they_are_not_equal()
        {
            // GIVEN
            var tag1 = new Tag("environment:production");
            var tag2 = new Tag("environment:staging");

            // WHEN & THEN
            Assert.AreNotEqual(tag1, tag2);
        }

        [Test]
        public void GIVEN_tag_WHEN_compared_with_string_of_same_value_THEN_they_are_equal()
        {
            // GIVEN
            var tag = new Tag("environment:production");
            var stringValue = "environment:production";

            // WHEN & THEN
            Assert.AreEqual(tag, stringValue);
        }

        [Test]
        public void GIVEN_tag_WHEN_compared_with_string_of_different_value_THEN_they_are_not_equal()
        {
            // GIVEN
            var tag = new Tag("environment:production");
            var stringValue = "environment:staging";

            // WHEN & THEN
            Assert.AreNotEqual(tag, stringValue);
        }

        [Test]
        public void GIVEN_metric_WHEN_add_tag_with_same_value_THEN_duplicate_tags_are_not_added()
        {
            // GIVEN
            var metric = new TestMetric(metricName, new List<Tag>(), mockStatsDClient.Object, SampleAll.Instance);
            var tagValue = "environment:production";

            // WHEN
            GenericOutcome addOutcome1 = metric.AddTag(tagValue);
            GenericOutcome addOutcome2 = metric.AddTag(new Tag(tagValue));

            // THEN
            Assert.IsTrue(addOutcome1.Success);
            Assert.IsTrue(addOutcome2.Success);
            Assert.AreEqual(1, metric.Tags.Count);
            Assert.IsTrue(metric.Tags.ContainsKey("environment"));
        }

        [Test]
        public void GIVEN_metric_with_tag_WHEN_remove_tag_object_THEN_tag_is_removed()
        {
            // GIVEN
            var singleTag = new Tag("environment:production");
            var tagList = new List<Tag> { singleTag, new Tag("region:us-east-1") };
            var metric = new TestMetric(metricName, tagList, mockStatsDClient.Object, SampleAll.Instance);

            // WHEN
            GenericOutcome removeOutcome = metric.RemoveTag(singleTag);

            // THEN
            Assert.IsTrue(removeOutcome.Success);
            Assert.AreEqual(1, metric.Tags.Count);
            Assert.IsFalse(metric.Tags.ContainsKey("environment"));
            Assert.IsTrue(metric.Tags.ContainsKey("region"));
        }

        [Test]
        public void GIVEN_metric_WHEN_remove_nonexistent_tag_THEN_no_tags_are_removed()
        {
            // GIVEN
            var singleTag = new List<Tag> { new Tag("environment:production"), new Tag("region:us-east-1") };
            var metric = new TestMetric(metricName, singleTag, mockStatsDClient.Object, SampleAll.Instance);
            var originalCount = metric.Tags.Count;

            // WHEN
            GenericOutcome removeOutcome = metric.RemoveTag("nonexistent");

            // THEN
            Assert.IsTrue(removeOutcome.Success);
            Assert.AreEqual(originalCount, metric.Tags.Count);
        }

        [Test]
        public void GIVEN_metric_WHEN_concurrent_duplicate_tag_add_operations_THEN_only_one_tag_is_added()
        {
            // GIVEN
            var metric = new TestMetric(metricName, new List<Tag>(), mockStatsDClient.Object, SampleAll.Instance);
            const int threadCount = 10;
            const string duplicateTagValue = "duplicate:tag";
            var tasks = new Task[threadCount];
            var barrier = new Barrier(threadCount);

            // WHEN
            for (int i = 0; i < threadCount; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    barrier.SignalAndWait(); // Synchronize start

                    // Each thread tries to add the same tag
                    GenericOutcome outcome = metric.AddTag(duplicateTagValue);
                    Assert.IsTrue(outcome.Success);
                });
            }

            Task.WaitAll(tasks);

            // THEN
            Assert.AreEqual(1, metric.Tags.Count);
            Assert.IsTrue(metric.Tags.ContainsKey("duplicate"));
        }

        [Test]
        public void GIVEN_counter_WHEN_default_THEN_samples_summed()
        {
            // GIVEN
            int incrementValue = 10;
            var counter = new Counter(metricName, tags, mockStatsDClient.Object, SampleAll.Instance, null, TestSeed);

            // WHEN
            counter.Add(incrementValue);
            counter.Add(incrementValue);
            counter.Add(incrementValue);
            GenericOutcome flushOutcome = counter.Flush();

            // THEN
            Assert.IsTrue(flushOutcome.Success);
            mockStatsDClient.Verify(
                client => client.Increment(metricName, 30, tags, SampleAll.Instance),
                Times.Once);
        }

        [Test]
        public void GIVEN_gauge_WHEN_default_THEN_samples_summed()
        {
            // GIVEN
            var gauge = new Gauge(metricName, tags, mockStatsDClient.Object, SampleAll.Instance, null, TestSeed);

            // WHEN
            gauge.Set(10);
            gauge.Set(20);
            gauge.Set(30);
            GenericOutcome flushOutcome = gauge.Flush();

            // THEN
            Assert.IsTrue(flushOutcome.Success);
            mockStatsDClient.Verify(
                client => client.Gauge(metricName, 30, tags, SampleAll.Instance),
                Times.Once);
        }

        [Test]
        public void GIVEN_timer_WHEN_default_THEN_samples_summed()
        {
            // GIVEN
            var timer = new GameLift.Server.Model.Metrics.Timer(metricName, tags, mockStatsDClient.Object, SampleAll.Instance, null, TestSeed);

            // WHEN
            timer.Set(10);
            timer.Set(30);
            timer.Set(20);
            GenericOutcome flushOutcome = timer.Flush();

            // THEN
            Assert.IsTrue(flushOutcome.Success);
            mockStatsDClient.Verify(
                client => client.Timing(metricName, 20, tags, SampleAll.Instance),
                Times.Once);
        }

        [Test]
        public void GIVEN_counter_WHEN_latest_THEN_latest_sent()
        {
            // GIVEN
            int incrementValue = 20;
            HashSet<IDerivedMetric> derivedMetrics = new HashSet<IDerivedMetric> { new Latest() };
            var counter = new Counter(metricName, tags, mockStatsDClient.Object, SampleAll.Instance, derivedMetrics, TestSeed);

            // WHEN
            counter.Add(10);
            counter.Add(incrementValue);
            GenericOutcome flushOutcome = counter.Flush();

            // THEN
            Assert.IsTrue(flushOutcome.Success);
            mockStatsDClient.Verify(
                client => client.Increment(metricName, 30, tags, SampleAll.Instance),
                Times.Once);
            mockStatsDClient.Verify(
                client => client.Increment(metricName + ".latest", incrementValue, tags, SampleAll.Instance),
                Times.Once);
        }

        [Test]
        public void GIVEN_counter_WHEN_percentile_THEN_samples_summed()
        {
            // GIVEN
            HashSet<IDerivedMetric> derivedMetrics = new HashSet<IDerivedMetric> { new Percentile(50) };
            var counter = new Counter(metricName, tags, mockStatsDClient.Object, SampleAll.Instance, derivedMetrics, TestSeed);

            // WHEN
            GenericOutcome addOutcome1 = counter.Add(10);
            GenericOutcome addOutcome2 = counter.Add(20);
            GenericOutcome addOutcome3 = counter.Add(30);
            GenericOutcome flushOutcome = counter.Flush();

            // THEN
            Assert.IsTrue(addOutcome1.Success);
            Assert.IsTrue(addOutcome2.Success);
            Assert.IsTrue(addOutcome3.Success);
            Assert.IsTrue(flushOutcome.Success);
            mockStatsDClient.Verify(
                client => client.Increment(metricName, 60, tags, SampleAll.Instance),
                Times.Once);
            mockStatsDClient.Verify(
                client => client.Increment(metricName + ".p50", 20, tags, SampleAll.Instance),
                Times.Once);
        }

        [Test]
        public void GIVEN_counter_WHEN_all_derived_THEN_all_sent()
        {
            // GIVEN
            HashSet<IDerivedMetric> derivedMetrics = new HashSet<IDerivedMetric>
            {
                new Latest(),
                new Sum(),
                new Mean(),
                new Count(),
                new Min(),
                new Max(),
                new Percentile(0),
                new Percentile(25),
                new Percentile(50),
                new Percentile(75),
                new Percentile(100),
            };
            var counter = new Counter(metricName, tags, mockStatsDClient.Object, SampleAll.Instance, derivedMetrics, TestSeed);

            // WHEN
            GenericOutcome addOutcome1 = counter.Add(1);
            GenericOutcome addOutcome2 = counter.Add(2);
            GenericOutcome addOutcome3 = counter.Add(3);
            GenericOutcome addOutcome4 = counter.Add(4);
            GenericOutcome addOutcome5 = counter.Add(5);
            GenericOutcome flushOutcome = counter.Flush();

            // THEN
            Assert.IsTrue(addOutcome1.Success);
            Assert.IsTrue(addOutcome2.Success);
            Assert.IsTrue(addOutcome3.Success);
            Assert.IsTrue(addOutcome4.Success);
            Assert.IsTrue(addOutcome5.Success);
            Assert.IsTrue(flushOutcome.Success);
            mockStatsDClient.Verify(
                client => client.Increment(metricName, 15, tags, SampleAll.Instance),
                Times.Once);
            mockStatsDClient.Verify(
                client => client.Increment(metricName + ".latest", 5, tags, SampleAll.Instance),
                Times.Once);
            mockStatsDClient.Verify(
                client => client.Increment(metricName + ".mean", 3, tags, SampleAll.Instance),
                Times.Once);
            mockStatsDClient.Verify(
                client => client.Increment(metricName + ".count", 5, tags, SampleAll.Instance),
                Times.Once);
            mockStatsDClient.Verify(
                client => client.Increment(metricName + ".min", 1, tags, SampleAll.Instance),
                Times.Once);
            mockStatsDClient.Verify(
                client => client.Increment(metricName + ".max", 5, tags, SampleAll.Instance),
                Times.Once);
            mockStatsDClient.Verify(
                client => client.Increment(metricName + ".p0", 1, tags, SampleAll.Instance),
                Times.Once);
            mockStatsDClient.Verify(
                client => client.Increment(metricName + ".p25", 2, tags, SampleAll.Instance),
                Times.Once);
            mockStatsDClient.Verify(
                client => client.Increment(metricName + ".p50", 3, tags, SampleAll.Instance),
                Times.Once);
            mockStatsDClient.Verify(
                client => client.Increment(metricName + ".p75", 4, tags, SampleAll.Instance),
                Times.Once);
            mockStatsDClient.Verify(
                client => client.Increment(metricName + ".p100", 5, tags, SampleAll.Instance),
                Times.Once);
            // Note: No .sum derived metric because Sum is the base metric for Counter
        }

        [Test]
        public void GIVEN_counter_with_zero_sample_rate_WHEN_add_called_THEN_no_samples_recorded()
        {
            // GIVEN
            var zeroSampleRate = new SampleFractional(0.0);
            var counter = new Counter(metricName, tags, mockStatsDClient.Object, zeroSampleRate, null, TestSeed);

            // WHEN
            GenericOutcome addOutcome1 = counter.Add(10);
            GenericOutcome addOutcome2 = counter.Add(20);
            GenericOutcome addOutcome3 = counter.Flush();

            // THEN
            Assert.IsTrue(addOutcome1.Success);
            Assert.IsTrue(addOutcome2.Success);
            Assert.IsTrue(addOutcome3.Success);

            mockStatsDClient.Verify(
                client => client.Increment(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<IList<Tag>>(), It.IsAny<SampleRate>()),
                Times.Never);
        }

        [Test]
        public void GIVEN_counter_with_deterministic_seed_WHEN_fractional_sampling_THEN_sampling_is_predictable()
        {
            // GIVEN
            var sampleRate = new SampleFractional(0.5);
            var counter = new Counter(metricName, tags, mockStatsDClient.Object, sampleRate, null, 42);

            // WHEN - Add multiple values to test sampling
            for (int i = 0; i < 10; i++)
            {
                GenericOutcome addOutcome = counter.Add(1);
                Assert.IsTrue(addOutcome.Success);
            }

            GenericOutcome flushOutcome = counter.Flush();

            // THEN
            Assert.IsTrue(flushOutcome.Success);
            // With seed 42 and rate 0.5, verify that some but not all samples are recorded
            mockStatsDClient.Verify(
                client => client.Increment(It.Is<string>(s => s == metricName), It.IsInRange(1, 9, Moq.Range.Inclusive), tags, sampleRate),
                Times.Once);
        }

        [Test]
        public void GIVEN_gauge_with_multiple_derived_metrics_including_failing_WHEN_flush_called_THEN_returns_failed_outcome_with_aggregated_errors()
        {
            // GIVEN
            var workingDerivedMetric = new Sum();
            var failingDerivedMetric1 = new FailingDerivedMetric("First failure");
            var failingDerivedMetric2 = new FailingDerivedMetric("Second failure");
            HashSet<IDerivedMetric> derivedMetrics = new HashSet<IDerivedMetric>
            {
                workingDerivedMetric,
                failingDerivedMetric1,
                failingDerivedMetric2,
            };
            var gauge = new Gauge(metricName, tags, mockStatsDClient.Object, SampleAll.Instance, derivedMetrics, TestSeed);

            // WHEN
            gauge.Set(25.5);
            GenericOutcome flushOutcome = gauge.Flush();

            // THEN
            Assert.IsFalse(flushOutcome.Success);
            Assert.IsNotNull(flushOutcome.Error);
            Assert.AreEqual(GameLiftErrorType.METRICS_SUBMISSION_FAILED, flushOutcome.Error.ErrorType);
            Assert.That(flushOutcome.Error.ErrorMessage, Does.Contain("Failed to flush gauge metric"));
            Assert.That(flushOutcome.Error.ErrorMessage, Does.Contain("First failure"));
        }

        [Test]
        public void GIVEN_counter_with_failing_derived_metric_WHEN_flush_called_THEN_returns_failed_outcome()
        {
            // GIVEN
            var failingDerivedMetric = new FailingDerivedMetric("Counter computation failed");
            HashSet<IDerivedMetric> derivedMetrics = new HashSet<IDerivedMetric> { failingDerivedMetric };
            var counter = new Counter(metricName, tags, mockStatsDClient.Object, SampleAll.Instance, derivedMetrics, TestSeed);

            // WHEN
            counter.Add(10);
            GenericOutcome flushOutcome = counter.Flush();

            // THEN
            Assert.IsFalse(flushOutcome.Success);
            Assert.IsNotNull(flushOutcome.Error);
            Assert.AreEqual(GameLiftErrorType.METRICS_SUBMISSION_FAILED, flushOutcome.Error.ErrorType);
            Assert.That(flushOutcome.Error.ErrorMessage, Does.Contain("Failed to flush counter metric: Counter computation failed"));
        }

        [Test]
        public void GIVEN_existing_metric_tag_WHEN_add_same_key_different_value_THEN_tag_replaced()
        {
            // GIVEN - Create counter with initial tag
            var initialTags = new List<Tag> { new Tag("session_id:game123") };
            var counter = new Counter(metricName, initialTags, mockStatsDClient.Object, SampleAll.Instance, null, TestSeed);

            // Verify initial state
            Assert.AreEqual(1, counter.Tags.Count);
            Assert.IsTrue(counter.Tags.ContainsKey("session_id") && counter.Tags["session_id"].Value == "game123");

            // WHEN - Add same key with different value
            counter.AddTag("session_id:game456");

            // THEN - Should replace, not add
            Assert.AreEqual(1, counter.Tags.Count, "Should have exactly 1 tag after replacement");
            Assert.IsTrue(counter.Tags.ContainsKey("session_id") && counter.Tags["session_id"].Value == "game456", "Should have new value");
            Assert.IsFalse(counter.Tags.Values.Any(t => t.Value == "game123"), "Old value should not exist");
        }

        [Test]
        public void GIVEN_existing_metric_tag_WHEN_multiple_replacements_THEN_last_value_persists()
        {
            // GIVEN - Create counter with initial tag
            var initialTags = new List<Tag> { new Tag("session_id:game123") };
            var counter = new Counter(metricName, initialTags, mockStatsDClient.Object, SampleAll.Instance, null, TestSeed);

            // Verify initial state
            Assert.AreEqual(1, counter.Tags.Count);
            Assert.IsTrue(counter.Tags.ContainsKey("session_id") && counter.Tags["session_id"].Value == "game123");

            // WHEN - Add same key twice with different value
            counter.AddTag("session_id:game456");
            counter.AddTag("session_id:game789");

            // THEN - Should replace, not add
            Assert.AreEqual(1, counter.Tags.Count, "Should have exactly 1 tag after replacement");
            Assert.IsTrue(counter.Tags.ContainsKey("session_id") && counter.Tags["session_id"].Value == "game789", "Should have new value");

            var unwantedValues = new[] { "game123","game456" };
            Assert.IsFalse(counter.Tags.Values.Any(t => unwantedValues.Contains(t.Value)), "Old values should not exist");
        }

        [Test]
        public void GIVEN_metric_WHEN_add_tag_object_same_key_THEN_replaced()
        {
            // GIVEN - Create counter with no initial tags
            var counter = new Counter(metricName, new List<Tag>(), mockStatsDClient.Object, SampleAll.Instance, null, TestSeed);

            // Verify initially empty
            Assert.AreEqual(0, counter.Tags.Count);

            // WHEN - Add tag using string first, then Tag object with same key
            counter.AddTag("env:dev");
            Assert.AreEqual(1, counter.Tags.Count);
            Assert.IsTrue(counter.Tags.ContainsKey("env") && counter.Tags["env"].Value == "dev");

            counter.AddTag(new Tag("env", "prod")); //Same key, different value using Tag object

            // THEN - Should replace, not add
            Assert.AreEqual(1, counter.Tags.Count, "Should have exactly 1 tag after replacement");
            Assert.IsTrue(counter.Tags.ContainsKey("env") && counter.Tags["env"].Value == "prod", "Should have new value from Tag object");
            Assert.IsFalse(counter.Tags.Values.Any(t => t.Value == "dev"), "Old string value should not exist");
        }

        [Test]
        public void GIVEN_existing_metric_tag_WHEN_remove_by_key_only_THEN_tag_removed()
        {

            // GIVEN - Create counter with initial tag
            var counter = new Counter(metricName, new List<Tag>(), mockStatsDClient.Object, SampleAll.Instance, null, TestSeed);
            var gauge = new Gauge(metricName, new List<Tag>(), mockStatsDClient.Object, SampleAll.Instance, null, TestSeed);

            counter.AddTag("env:dev");
            gauge.AddTag("env:prod");
            // Verify initial tag length of 1
            Assert.AreEqual(1, counter.Tags.Count);
            Assert.AreEqual(1, gauge.Tags.Count);

            // WHEN - Remove tag is called, with only the key of the tag
            counter.RemoveTag("env");
            gauge.RemoveTag("env");
            // THEN - Should remove any tags with key of "env"
            Assert.AreEqual(0, counter.Tags.Count, "Should have exactly 0 tags on counter after removal");
            Assert.AreEqual(0, gauge.Tags.Count, "Should have exactly 0 tags on gauge after removal");
        }

        [Test]
        public void GIVEN_null_tag_WHEN_add_to_metric_THEN_handles_gracefully()
        {
            // GIVEN
            var counter = new Counter(metricName, new List<Tag>(), mockStatsDClient.Object,SampleAll.Instance, null, TestSeed);

            // WHEN/THEN - Should handle nulls without throwing
            GenericOutcome outcome1 = counter.AddTag((string)null);
            GenericOutcome outcome2 = counter.AddTag("");
            GenericOutcome outcome3 = counter.AddTag("   ");

            Assert.IsFalse(outcome1.Success); // Should fail gracefully
            Assert.IsFalse(outcome2.Success);
            Assert.IsFalse(outcome3.Success);
            Assert.AreEqual(0, counter.Tags.Count); // No invalid tags added
        }

        [Test]
        public void GIVEN_invalid_tag_format_WHEN_add_to_metric_THEN_handles_appropriately()
        {
            // GIVEN
            var gauge = new Gauge(metricName, new List<Tag>(), mockStatsDClient.Object, SampleAll.Instance, null, TestSeed);

            // WHEN - Tag without colon separator
            GenericOutcome outcome = gauge.AddTag("invalid_tag_no_colon");

            // THEN - Should fail with validation error
            Assert.IsFalse(outcome.Success);
            Assert.AreEqual(GameLiftErrorType.VALIDATION_EXCEPTION, outcome.Error.ErrorType);
        }

        [Test]
        public void 
        GIVEN_concurrent_metric_tag_replacements_WHEN_multiple_threads_THEN_thread_safe()
        {
            // GIVEN (fully qualified timer due to threading import `Timer` conflict)
            var timer = new Aws.GameLift.Server.Model.Metrics.Timer(metricName, new List<Tag>(), mockStatsDClient.Object, SampleAll.Instance, null, TestSeed);
            timer.AddTag("request_id:initial");

            var tasks = new List<Task>();
            var random = new Random();

            // WHEN - Multiple threads replacing same tag
            for (int i = 0; i < 50; i++)
            {
                int threadId = i;
                tasks.Add(Task.Run(() =>
                {
                    Thread.Sleep(random.Next(0, 5));
                    timer.AddTag($"request_id:thread_{threadId}");
                }));
            }

            Task.WaitAll(tasks.ToArray());

            // THEN - Should have exactly 1 request_id tag
            Assert.AreEqual(1, timer.Tags.Count);
            Assert.IsTrue(timer.Tags.ContainsKey("request_id"));
            Assert.IsTrue(timer.Tags["request_id"].Value.StartsWith("thread_"));
        }

        [Test]
        public void GIVEN_metric_tag_WHEN_add_then_remove_by_key_THEN_tag_removed()
        {
            // GIVEN
            var counter = new Counter(metricName, new List<Tag>(), mockStatsDClient.Object, SampleAll.Instance, null, TestSeed);

            // WHEN
            counter.AddTag("environment:production");
            counter.AddTag("service:api");
            Assert.AreEqual(2, counter.Tags.Count);

            counter.RemoveTag("environment"); // Remove by key only

            // THEN
            Assert.AreEqual(1, counter.Tags.Count);
            Assert.IsFalse(counter.Tags.ContainsKey("environment"));
            Assert.IsTrue(counter.Tags.ContainsKey("service"));
        }

        [Test]
        public void GIVEN_metric_WHEN_add_duplicate_tags_in_builder_THEN_last_wins()
        {
            // GIVEN/WHEN
            var metrics = Metrics.Create().Build();
            var counter = metrics.NewCounter("test")
                .AddTag("env:dev")
                .AddTag("env:staging")
                .AddTag("env:prod")
                .Build();

            // THEN
            Assert.AreEqual(1, counter.Tags.Count);
            Assert.IsTrue(counter.Tags.ContainsKey("env") && counter.Tags["env"].Value == "prod");
        }

        [Test]
        public void GIVEN_different_metric_types_WHEN_add_same_key_different_values_THEN_all_replace_correctly()
        {
            // GIVEN - all metric types (Counter, Gauge, Timer), (fully qualified timer due to threading import `Timer` conflict)
            var counter = new Counter(metricName, new List<Tag>(), mockStatsDClient.Object, SampleAll.Instance, null, TestSeed);
            var gauge = new Gauge(metricName, new List<Tag>(), mockStatsDClient.Object, SampleAll.Instance, null, TestSeed);
            var timer = new Aws.GameLift.Server.Model.Metrics.Timer(metricName, new List<Tag>(), mockStatsDClient.Object, SampleAll.Instance, null, TestSeed);

            // WHEN
            // Add initial tags
            counter.AddTag("version:1.0");
            gauge.AddTag("version:1.0");
            timer.AddTag("version:1.0");

            // Replace with new version
            counter.AddTag("version:2.0");
            gauge.AddTag("version:2.0");
            timer.AddTag("version:2.0");

            // THEN
            // All should have exactly 1 tag with new value
            Assert.AreEqual(1, counter.Tags.Count);
            Assert.AreEqual(1, gauge.Tags.Count);
            Assert.AreEqual(1, timer.Tags.Count);

            Assert.AreEqual("2.0", counter.Tags["version"].Value);
            Assert.AreEqual("2.0", gauge.Tags["version"].Value);
            Assert.AreEqual("2.0", timer.Tags["version"].Value);
        }
        [Test]
        public void GIVEN_existing_metric_tag_WHEN_remove_by_full_value_THEN_tag_removed()
        {
            // GIVEN - Create counter and gauge metric
            var counter = new Counter(metricName, new List<Tag>(), mockStatsDClient.Object, SampleAll.Instance, null, TestSeed);
            var gauge = new Gauge(metricName, new List<Tag>(), mockStatsDClient.Object, SampleAll.Instance, null, TestSeed);

            counter.AddTag("env:dev");
            gauge.AddTag("env:prod");
            // Verify initial tag length of 1
            Assert.AreEqual(1, counter.Tags.Count);
            Assert.AreEqual(1, gauge.Tags.Count);

            // WHEN - Remove tag is called, with only the key of the tag
            counter.RemoveTag("env");
            gauge.RemoveTag("env");
            // THEN - Should remove any tags with key of "env"
            Assert.AreEqual(0, counter.Tags.Count, "Should have exactly 0 tags on counter after removal");
            Assert.AreEqual(0, gauge.Tags.Count, "Should have exactly 0 tags on gauge after removal");

        }

        [Test]
        public void GIVEN_multiple_metric_tags_WHEN_replace_one_THEN_others_unchanged()
        {
            // GIVEN - Create counter metric with a multiple tags
            var initialTags = new List<Tag> { new Tag("session_id:game123") };

            var counter = new Counter(metricName, initialTags, mockStatsDClient.Object, SampleAll.Instance, null, TestSeed);
            counter.AddTag("env:dev");
            counter.AddTag("region:eu-west");


            Assert.AreEqual(3, counter.Tags.Count);
            Assert.IsTrue(counter.Tags.ContainsKey("session_id") && counter.Tags["session_id"].Value == "game123");
            Assert.IsTrue(counter.Tags.ContainsKey("env") && counter.Tags["env"].Value == "dev");
            Assert.IsTrue(counter.Tags.ContainsKey("region") && counter.Tags["region"].Value == "eu-west");

            // WHEN - Replace only the env tag
            counter.AddTag("env:prod");
            Assert.AreEqual(3, counter.Tags.Count);


            // THEN - Should still have 3 tags, with env replaced but others unchanged
            Assert.AreEqual(3, counter.Tags.Count, "Should still have exactly 3 tags after replacement");

            Assert.IsTrue(counter.Tags.ContainsKey("env") && counter.Tags["env"].Value == "prod", "env should be updated to prod");
            Assert.IsFalse(counter.Tags.Values.Any(t => t.Key == "env" && t.Value == "dev"), "old env:dev should not exist");

            // Check that other tags are unchanged
            Assert.IsTrue(counter.Tags.ContainsKey("session_id") && counter.Tags["session_id"].Value == "game123", "session_id should remain unchanged");
            Assert.IsTrue(counter.Tags.ContainsKey("region") && counter.Tags["region"].Value == "eu-west", "region should remain unchanged");
        }

        [Test]
        public void GIVEN_metric_builder_WHEN_duplicate_tag_keys_THEN_last_value_used()
        {
            // GIVEN  - Create counter using builder
            var metrics = Metrics.Create().Build();
            // WHEN - Duplicated tag keys are assigned via builder
            var counter = metrics.NewCounter("test_counter")
                .AddTag("env:dev")
                .AddTag("region:us-east")
                .AddTag("env:staging")
                .AddTag("env:prod")
                .Build();

            // THEN - Last tag value is used
            Assert.AreEqual(2, counter.Tags.Count, "Should still have exactly 2 tags after build");
            Assert.IsTrue(counter.Tags.ContainsKey("env") && counter.Tags["env"].Value == "prod");

        }

        [Test]
        public void GIVEN_metric_with_replaced_tags_WHEN_flushed_THEN_uses_new_tag_values()
        {
            // GIVEN - Create counter with initial tag
            var initialTags = new List<Tag> { new Tag("session_id:old_session") };
            var counter = new Counter(metricName, initialTags, mockStatsDClient.Object, SampleAll.Instance, null, TestSeed);

            // Replace tag with new value
            counter.AddTag("session_id:new_session");
            counter.Add(42);

            // WHEN - Flush the metric
            counter.Flush();

            // THEN - Should send StatsD packet with new tag value, not old one
            mockStatsDClient.Verify(
                client => client.Increment(
                    metricName,
                    42,
                    It.Is<IList<Tag>>(tags => tags.Any(t => t.Key == "session_id" && t.Value == "new_session")),
                    SampleAll.Instance),
                Times.Once);

            // Verify old tag value is NOT sent
            mockStatsDClient.Verify(
                client => client.Increment(
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.Is<IList<Tag>>(tags => tags.Any(t => t.Value == "old_session")),
                    It.IsAny<SampleRate>()),
                Times.Never);
        }

        /// <summary>
        /// Test implementation of MetricBase for testing purposes.
        /// </summary>
        private class TestMetric : MetricBase
        {
            public TestMetric(string name, IList<Tag> tags, IStatsDClient statsDClient, SampleRate sampleRate = null)
            : base(name, tags, statsDClient, sampleRate ?? SampleAll.Instance, null, null)
            {
            }
        }

        /// <summary>
        /// Test implementation of MetricBase for thread safety testing.
        /// </summary>
        private class ThreadSafetyTestMetric : MetricBase
        {
            public ThreadSafetyTestMetric(string name, IList<Tag> tags, IStatsDClient statsDClient, SampleRate sampleRate = null)
                : base(name, tags, statsDClient, sampleRate ?? SampleAll.Instance, null, null)
            {
            }

            public void TestSetValue(double value) => SetValue(value);

            public void TestAdjustValue(double delta) => AdjustValue(delta);
        }

        /// <summary>
        /// Test implementation of IDerivedMetric that always fails computation.
        /// </summary>
        private class FailingDerivedMetric : IDerivedMetric
        {
            private readonly string errorMessage;

            public FailingDerivedMetric(string errorMessage = "Simulated derived metric failure")
            {
                this.errorMessage = errorMessage;
            }

            public string Name => "failing";

            public double? CalculateValue(IList<double> samples)
            {
                throw new System.InvalidOperationException(errorMessage);
            }
        }
    }
}
