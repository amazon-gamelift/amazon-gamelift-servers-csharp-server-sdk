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
namespace Aws.GameLift.Server.Model.Metrics
{
    using System;
    using System.Collections.Generic;
    using Aws.GameLift.Server.Model.Metrics.DerivedMetrics;
    using log4net;

    /// <summary>
    /// Base class for all metric types that provides common functionality.
    /// This class serves as the foundation for different metric implementations by providing thread-safe value operations,
    /// tag management, and integration with StatsD clients for metric reporting.
    /// </summary>
    public class MetricBase : IMetric
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(MetricBase));

        private readonly Dictionary<string, Tag> tags;
        private readonly object sampleLock = new object();
        private readonly object tagsLock = new object();
        private readonly RandomGenerator randomGenerator;

        // Suppress IDE0028: Collection initialization can be simplified
        // We need to use explicit type initialization for .NET Framework 4.6.2 compatibility
        // The simplified 'new()' syntax is only available in C# 9.0+, which isn't supported by all our target frameworks
#pragma warning disable IDE0028
        protected IList<double> Samples { get; } = new List<double>();
#pragma warning restore IDE0028

        protected ISet<IDerivedMetric> DerivedMetrics { get; }

        /// <summary>
        /// Maintains the most recent value of the metric.
        /// </summary>
        private double currentValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="MetricBase"/> class.
        /// </summary>
        /// <param name="name">The name of the metric.</param>
        /// <param name="tags">The tags associated with this metric.</param>
        /// <param name="statsDClient">The StatsD client to use for sending metrics.</param>
        /// <param name="sampleRate">The sampling rate for the metric.</param>
        /// <param name="derivedMetrics">A List of the Derived metric types to be calculated on flush.</param>
        /// <param name="randomSeed">Optional seed for the random number generator. If null, uses current time.</param>
        protected MetricBase(string name, IList<Tag> tags, IStatsDClient statsDClient, SampleRate sampleRate, ISet<IDerivedMetric> derivedMetrics, int? randomSeed)
        {
            Name = name;
            // Suppress IDE0028: Collection initialization can be simplified
            // We need to use explicit type initialization for .NET Framework 4.6.2 compatibility
            // The simplified 'new()' syntax is only available in C# 9.0+, which isn't supported by all our target frameworks
#pragma warning disable IDE0028
            this.tags = new Dictionary<string, Tag>();
#pragma warning restore IDE0028
            if (tags != null)
            {
                foreach (var tag in tags)
                {
                    this.tags[tag.Key] = tag;
                }
            }

            StatsDClient = statsDClient;
            SampleRate = sampleRate;
            DerivedMetrics = derivedMetrics ?? new HashSet<IDerivedMetric>();
            randomGenerator = new RandomGenerator(randomSeed);
        }

        /// <summary>
        /// Gets the tags associated with this metric.
        /// </summary>
        public IDictionary<string, Tag> Tags
        {
            get
            {
                lock (tagsLock)
                {
                    return new Dictionary<string, Tag>(tags);
                }
            }
        }

        /// <summary>
        /// Gets the name of the metric.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the sample rate for the metric.
        /// </summary>
        public SampleRate SampleRate { get; }

        /// <summary>
        /// Gets Client used to send metrics to the StatsD server.
        /// </summary>
        internal IStatsDClient StatsDClient { get; }

        /// <summary>
        /// Gets the current value of the metric.
        /// </summary>
        public double CurrentValue
        {
            get
            {
                lock (sampleLock)
                {
                    return currentValue;
                }
            }
        }

        /// <summary>
        /// Sets the current value of the metric and returns the new value.
        /// </summary>
        /// <param name="value">The value to set.</param>
        /// <returns>The new current value.</returns>
        protected double SetValue(double value)
        {
            lock (sampleLock)
            {
                currentValue = value;
                if (ShouldSample())
                {
                    Samples.Add(value);
                }

                return currentValue;
            }
        }

        /// <summary>
        /// Adjusts the current value by the specified delta and returns the resultant value.
        /// </summary>
        /// <param name="delta">The delta value to add to the current value.</param>
        /// <returns>The new current value after applying the delta.</returns>
        protected double AdjustValue(double delta)
        {
            lock (sampleLock)
            {
                currentValue += delta;
                if (ShouldSample())
                {
                    Samples.Add(currentValue);
                }

                return currentValue;
            }
        }

        /// <summary>
        /// Determines whether a sample should be included based on the configured sample rate.
        /// </summary>
        /// <returns>True if the sample should be included, false otherwise.</returns>
        private bool ShouldSample()
        {
            return randomGenerator.NextDouble() < SampleRate.ToDouble();
        }

        /// <summary>
        /// Adds a tag to the metric. If a tag with the same key already exists, it will be replaced.
        /// </summary>
        /// <param name="tag">The tag to add.</param>
        /// <returns>A GenericOutcome indicating the success or failure of the operation.</returns>
        public GenericOutcome AddTag(Tag tag)
        {
            if (tag == null)
            {
                Log.Error("Ignoring attempt to add null tag to metric");
                return new GenericOutcome();
            }

            try
            {
                lock (tagsLock)
                {
                    tags[tag.Key] = tag;
                }

                return new GenericOutcome();
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to add tag to metric. Error: {ex.Message}", ex);
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.METRICS_CONFIGURATION_FAILED, $"Failed to add tag: {ex.Message}"));
            }
        }

        /// <summary>
        /// Adds a tag to the metric. If a tag with the same key already exists, it will be replaced.
        /// </summary>
        /// <param name="tagValue">The string value of the tag to add.</param>
        /// <returns>A GenericOutcome indicating the success or failure of the operation.</returns>
        public GenericOutcome AddTag(string tagValue)
        {
            if (string.IsNullOrWhiteSpace(tagValue))
            {
                Log.Error("Attempted to add null or empty tag string to metric");
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.VALIDATION_EXCEPTION, "Tag value cannot be null or empty."));
            }

            try
            {
                var tag = new Tag(tagValue);
                return AddTag(tag);
            }
            catch (ArgumentException ex)
            {
                Log.Error($"Failed to add tag to metric due to invalid format '{tagValue}'. Error: {ex.Message}", ex);
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.VALIDATION_EXCEPTION, $"Invalid tag format: {ex.Message}"));
            }
        }

        /// <summary>
        /// Removes a tag from the metric, if it exists.
        /// </summary>
        /// <param name="tag">The tag to remove.</param>
        /// <returns>A GenericOutcome indicating the success or failure of the operation.</returns>
        public GenericOutcome RemoveTag(Tag tag)
        {
            try
            {
                lock (tagsLock)
                {
                    tags.Remove(tag.Key);
                }

                return new GenericOutcome();
            }
            catch (Exception ex)
            {
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.METRICS_CONFIGURATION_FAILED, $"Failed to remove tag: {ex.Message}"));
            }
        }

        /// <summary>
        /// Removes a tag from the metric, if it exists.
        /// </summary>
        /// <param name="key">The tag key to remove.</param>
        /// <returns>A GenericOutcome indicating the success or failure of the operation.</returns>
        public GenericOutcome RemoveTag(string key)
        {
            try
            {
                lock (tagsLock)
                {
                    tags.Remove(key);
                }

                return new GenericOutcome();
            }
            catch (Exception ex)
            {
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.METRICS_CONFIGURATION_FAILED, $"Failed to remove tag: {ex.Message}"));
            }
        }

        /// <summary>
        /// Run through the assigned derived metrics and calculate their values based on the current samples.
        /// </summary>
        /// <returns>A Dictionary of the calculated derived metrics.</returns>
        protected Dictionary<string, double> CalculateDerivedMetricValues()
        {
            // Suppress IDE0028: Collection initialization can be simplified
            // We need to use explicit type initialization for .NET Framework 4.6.2 compatibility
            // The simplified 'new()' syntax is only available in C# 9.0+, which isn't supported by all our target frameworks
#pragma warning disable IDE0028
            Dictionary<string, double> derivedValues = new Dictionary<string, double>();
#pragma warning restore IDE0028
            lock (sampleLock)
            {
                foreach (IDerivedMetric derivedMetric in DerivedMetrics)
                {
                    double? calculatedValue = derivedMetric.CalculateValue(Samples);
                    if (calculatedValue.HasValue)
                    {
                        derivedValues.Add(derivedMetric.Name, calculatedValue.Value);
                    }
                }

                Samples.Clear();
            }

            return derivedValues;
        }

        /// <summary>
        /// formats the derived metric name for the statsDClient.
        /// </summary>
        /// <param name="metricName">Name of the metric.</param>
        /// <param name="derivedMetricName">Name of the derived metric.</param>
        /// <returns>the formatted name as a string.</returns>
        protected static string FormatDerivedMetricName(string metricName, string derivedMetricName)
        {
#pragma warning disable S4040 // The output should be lowercase not upper
            return metricName + "." + derivedMetricName.ToLowerInvariant();
#pragma warning restore S4040
        }

        /// <summary>
        /// Flushes the current metric value to the StatsD client.
        /// This method is typically called to send the metric data to the StatsD server.
        /// </summary>
        /// <returns>A GenericOutcome indicating the success or failure of the operation.</returns>
        public virtual GenericOutcome Flush()
        {
            // This method should be overridden in derived classes to implement specific metric reporting logic.
            return new GenericOutcome();
        }
    }
}
