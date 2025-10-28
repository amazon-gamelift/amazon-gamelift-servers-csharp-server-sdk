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
    using Aws.GameLift.Server.Model.Metrics;
    using Aws.GameLift.Server.Model.Metrics.DerivedMetrics;

    /// <summary>
    /// Enum representing the different types of metrics.
    /// </summary>
    public enum MetricType
    {
        Counter,
        Gauge,
        Timer,
    }

    /// <summary>
    /// Generic builder for creating metrics with fluent configuration.
    /// </summary>
    /// <typeparam name="T">The type of metric interface (ICounter, IGauge, or ITimer).</typeparam>
    public class MetricBuilder<T>
        where T : class
    {
        private readonly string name;
        private readonly Metrics metrics;
        private readonly MetricType metricType;

    // Suppress IDE0028: Collection initialization can be simplified
    // We need to use explicit type initialization for .NET Framework 4.6.2 compatibility
    // The simplified 'new()' syntax is only available in C# 9.0+, which isn't supported by all our target frameworks
#pragma warning disable IDE0028
        private readonly List<string> tags = new List<string>();
#pragma warning restore IDE0028
        private SampleRate sampleRate = SampleAll.Instance;
        private ISet<IDerivedMetric> derivedMetrics = new HashSet<IDerivedMetric>();
        private int? randomSeed = null;

        internal MetricBuilder(string name, Metrics metrics, MetricType metricType)
        {
            this.name = name;
            this.metrics = metrics;
            this.metricType = metricType;
        }

        /// <summary>
        /// Adds a tag to the metric.
        /// </summary>
        /// <param name="tag">The tag to add.</param>
        /// <returns>This builder instance for method chaining.</returns>
        public MetricBuilder<T> AddTag(string tag)
        {
            if (!string.IsNullOrWhiteSpace(tag))
            {
                tags.Add(tag);
            }

            return this;
        }

        /// <summary>
        /// Adds multiple tags to the metric.
        /// </summary>
        /// <param name="tags">The tags to add.</param>
        /// <returns>This builder instance for method chaining.</returns>
        public MetricBuilder<T> AddTags(params string[] tags)
        {
            foreach (var tag in tags)
            {
                AddTag(tag);
            }

            return this;
        }

        /// <summary>
        /// Sets the sample rate for the metric.
        /// </summary>
        /// <param name="sampleRate">The sample rate.</param>
        /// <returns>This builder instance for method chaining.</returns>
        public MetricBuilder<T> SetSampleRate(SampleRate sampleRate)
        {
            this.sampleRate = sampleRate ?? SampleAll.Instance;
            return this;
        }

        /// <summary>
        /// Sets the random seed for sampling operations.
        /// </summary>
        /// <param name="seed">The seed value for the random number generator.</param>
        /// <returns>This builder instance for method chaining.</returns>
        public MetricBuilder<T> SetRandomSeed(int seed)
        {
            randomSeed = seed;
            return this;
        }

        /// <summary>
        /// Set the derived metrics for this metric, replacing any currently set derived metrics.
        /// </summary>
        /// <param name="newDerivedMetrics">A set of all the derivedMetrics to be calculated by this metric.</param>
        /// <returns>This builder instance for method chaining.</returns>
        public MetricBuilder<T> SetDerivedMetrics(ISet<IDerivedMetric> newDerivedMetrics)
        {
            derivedMetrics = newDerivedMetrics ?? new HashSet<IDerivedMetric>();
            return this;
        }

        /// <summary>
        /// Adds a Derived metric to the list to be calculated.
        /// </summary>
        /// <param name="derivedMetric">A New Derived metric to be added to the list.</param>
        /// <returns>This builder instance for method chaining.</returns>
        public MetricBuilder<T> AddDerivedMetric(IDerivedMetric derivedMetric)
        {
            derivedMetrics.Add(derivedMetric);
            return this;
        }

        /// <summary>
        /// Builds and registers the metric with the Metrics.
        /// </summary>
        /// <returns>The created metric instance.</returns>
        public T Build()
        {
            switch (metricType)
            {
                case MetricType.Counter:
                    return metrics.CreateCounterInternal(name, tags, sampleRate, derivedMetrics, randomSeed) as T;
                case MetricType.Gauge:
                    return metrics.CreateGaugeInternal(name, tags, sampleRate, derivedMetrics, randomSeed) as T;
                case MetricType.Timer:
                    return metrics.CreateTimerInternal(name, tags, sampleRate, derivedMetrics, randomSeed) as T;
                default:
                    throw new InvalidOperationException($"Unsupported metric type: {metricType}");
            }
        }
    }
}
