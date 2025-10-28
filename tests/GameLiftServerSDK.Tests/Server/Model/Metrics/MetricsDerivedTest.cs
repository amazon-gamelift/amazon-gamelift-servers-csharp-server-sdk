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

namespace Aws.GameLift.Tests.Server.Model.Metrics.DerivedMetrics
{
    using System;
    using System.Collections.Generic;
    using Aws.GameLift.Server.Model.Metrics.DerivedMetrics;
    using NUnit.Framework;

    [TestFixture]
    public class MetricsDerivedTest
    {
        private const double DoubleDelta = 0.0001;

        [Test]
        public void GIVEN_SampleList_WHEN_Last_CalculateValue_THEN_ReturnsLastElement()
        {
            // GIVEN
            List<double> samples = new List<double> { 1.0, 2.0, 3.0, 4.0, 5.0 };
            Latest derived = new Latest();

            // WHEN
            double? result = derived.CalculateValue(samples);

            // THEN
            Assert.IsTrue(result.HasValue);
            Assert.AreEqual(5.0, result.Value);
        }

        [Test]
        public void GIVEN_NoSamples_WHEN_Last_CalculateValue_THEN_Returns_Null()
        {
            // GIVEN
            List<double> samples = new List<double>();
            Latest derived = new Latest();

            // WHEN
            double? result = derived.CalculateValue(samples);

            // THEN
            Assert.IsNull(result);
        }

        [Test]
        public void GIVEN_SampleList_WHEN_Sum_CalculateValue_THEN_ReturnsSum()
        {
            // GIVEN
            List<double> samples = new List<double> { 1.0, 2.0, 3.0, 4.0, 5.0 };
            Sum derived = new Sum();

            // WHEN
            double? result = derived.CalculateValue(samples);

            // THEN
            Assert.IsTrue(result.HasValue);
            Assert.AreEqual(15.0, result.Value);
        }

        [Test]
        public void GIVEN_NoSamples_WHEN_Sum_CalculateValue_THEN_Returns_Zero()
        {
            // GIVEN
            List<double> samples = new List<double>();
            Sum derived = new Sum();

            // WHEN
            double? result = derived.CalculateValue(samples);

            // THEN
            Assert.IsNull(result);
        }

        [Test]
        public void GIVEN_SampleList_WHEN_Mean_CalculateValue_THEN_ReturnsMean()
        {
            // GIVEN
            List<double> samples = new List<double> { 1.0, 2.0, 3.0, 4.0, 5.0 };
            Mean derived = new Mean();

            // WHEN
            double? result = derived.CalculateValue(samples);

            // THEN
            Assert.IsTrue(result.HasValue);
            Assert.AreEqual(3.0, result.Value);
        }

        [Test]
        public void GIVEN_NoSamples_WHEN_Mean_CalculateValue_THEN_Returns_Null()
        {
            // GIVEN
            List<double> samples = new List<double>();
            Mean derived = new Mean();

            // WHEN
            double? result = derived.CalculateValue(samples);

            // THEN
            Assert.IsNull(result);
        }

        [TestCase(0, 1.0)]
        [TestCase(25, 2.0)]
        [TestCase(50, 3.0)]
        [TestCase(75, 4.0)]
        [TestCase(100, 5.0)]
        public void GIVEN_SampleList_WHEN_Percential_index_CalculateValue_THEN_ReturnsLastElement(int percentile, double expectedValue)
        {
            // GIVEN
            List<double> samples = new List<double> { 1.0, 2.0, 3.0, 4.0, 5.0 };
            Percentile derived = new Percentile(percentile);

            // WHEN
            double? result = derived.CalculateValue(samples);

            // THEN
            Assert.IsTrue(result.HasValue);
            Assert.AreEqual(expectedValue, result.Value);
        }

        [TestCase(0, 0.0)]
        [TestCase(50, 5.0)]
        [TestCase(90, 9.0)]
        [TestCase(95, 9.5)]
        [TestCase(100, 10.0)]
        public void GIVEN_SampleList_two_values_WHEN_Percential_interpolation_CalculateValue_THEN_InterprelatedResponse(int percentile, double expectedValue)
        {
            // GIVEN
            List<double> samples = new List<double> { 0.0, 10.0 };
            Percentile derived = new Percentile(percentile);

            // WHEN
            double? result = derived.CalculateValue(samples);

            // THEN
            Assert.IsTrue(result.HasValue);
            Assert.AreEqual(expectedValue, result.Value, DoubleDelta);
        }

        [TestCase(0, 0.0)]
        [TestCase(50, 5.0)]
        [TestCase(90, 9.7)]
        [TestCase(95, 9.85)]
        [TestCase(100, 10.0)]
        public void GIVEN_SampleList_four_values_WHEN_Percential_interpolation_CalculateValue_THEN_InterprelatedResponse(int percentile, double expectedValue)
        {
            // GIVEN
            List<double> samples = new List<double> { 0.0, 1.0, 9.0, 10.0 };
            Percentile derived = new Percentile(percentile);

            // WHEN
            double? result = derived.CalculateValue(samples);

            // THEN
            Assert.IsTrue(result.HasValue);
            Assert.AreEqual(expectedValue, result.Value, DoubleDelta);
        }

        [TestCase(0)]
        [TestCase(50)]
        [TestCase(100)]
        public void GIVEN_Single_Sample_WHEN_Percential_interpolation_CalculateValue_THEN_Sample(int percentile)
        {
            // GIVEN
            List<double> samples = new List<double> { 1.0 };
            Percentile derived = new Percentile(percentile);

            // WHEN
            double? result = derived.CalculateValue(samples);

            // THEN
            Assert.IsTrue(result.HasValue);
            Assert.AreEqual(1.0, result.Value, DoubleDelta);
        }

        [TestCase(-10)]
        [TestCase(110)]
        public void GIVEN_SampleList_WHEN_Percential_110_CalculateValue_THEN_Throws_exception(int percentile)
        {
            // GIVEN && WHEN && THEN
            Assert.Throws<ArgumentOutOfRangeException>(() => new Percentile(percentile));
        }

        [Test]
        public void GIVEN_NoSamples_WHEN_Percential_CalculateValue_THEN_Returns_Null()
        {
            // GIVEN
            List<double> samples = new List<double>();
            Percentile derived = new Percentile(75);

            // WHEN
            double? result = derived.CalculateValue(samples);

            // THEN
            Assert.IsNull(result);
        }

        [Test]
        public void GIVEN_NullSamples_WHEN_Percential_CalculateValue_THEN_Returns_Null()
        {
            // GIVEN
            Percentile derived = new Percentile(75);

            // WHEN
            double? result = derived.CalculateValue(null);

            // THEN
            Assert.IsNull(result);
        }
    }
}
