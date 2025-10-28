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

    /// <summary>
    /// Represents a counter metric that can be incremented.
    /// </summary>
    public class Counter : MetricBase, ICounter
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Counter"/> class.
        /// </summary>
        /// <param name="name">The name of the counter metric.</param>
        /// <param name="tags">The tags associated with this counter.</param>
        /// <param name="statsDClient">The StatsD client to use for sending metrics.</param>
        /// <param name="sampleRate">The sample rate for the counter.</param>
        /// <param name="derivedMetrics">A Set of the Derived metric types to be calculated on flush.</param>
        /// <param name="randomSeed">Optional seed for the random number generator.</param>
        public Counter(string name, IList<Tag> tags, IStatsDClient statsDClient, SampleRate sampleRate, ISet<IDerivedMetric> derivedMetrics, int? randomSeed)
            : base(name, tags, statsDClient, sampleRate, derivedMetrics, randomSeed)
        {
            DerivedMetrics.Add(new Sum()); // No need to pass sample rate or tags to Sum, it uses the base metric's sample rate and tags.
        }

        /// <summary>
        /// Increments the counter by 1.
        /// </summary>
        /// <returns>A GenericOutcome indicating the success or failure of the operation.</returns>
        public GenericOutcome Increment()
        {
            try
            {
                SetValue(1);
                return new GenericOutcome();
            }
            catch (Exception ex)
            {
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.METRICS_CONFIGURATION_FAILED, $"Failed to increment counter: {ex.Message}"));
            }
        }

        /// <summary>
        /// Increments the counter by the specified value.
        /// </summary>
        /// <param name="value">The amount to increment the counter by.</param>
        /// <returns>A GenericOutcome indicating the success or failure of the operation.</returns>
        /// <exception cref="ArgumentException">Thrown when the value is negative.</exception>
        public GenericOutcome Add(int value)
        {
            if (value < 0)
            {
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.VALIDATION_EXCEPTION, "Value cannot be negative."));
            }

            try
            {
                SetValue(value);
                return new GenericOutcome();
            }
            catch (Exception ex)
            {
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.METRICS_CONFIGURATION_FAILED, $"Failed to add to counter: {ex.Message}"));
            }
        }

        /// <summary>
        /// Flushes the metric data to the GameLift service.
        /// </summary>
        /// <returns>A GenericOutcome indicating the success or failure of the operation.</returns>
        public override GenericOutcome Flush()
        {
            try
            {
                Dictionary<string, double> derivedMetricValues = CalculateDerivedMetricValues();

                // Only send metrics if there are samples to flush
                if (derivedMetricValues.Count == 0)
                {
                    return new GenericOutcome();
                }

                // Suppress IDE0028: Collection initialization can be simplified
                // We need to use explicit type initialization for .NET Framework 4.6.2 compatibility
                // The simplified 'new()' syntax is only available in C# 9.0+, which isn't supported by all our target frameworks
#pragma warning disable IDE0028
                // Send the sum metric as base value
                StatsDClient.Increment(Name, (int)derivedMetricValues[nameof(Sum)], new List<Tag>(Tags.Values), SampleRate);
                derivedMetricValues.Remove(nameof(Sum)); // Remove Sum from derived metrics to avoid double submission

                List<string> derivedMetricErrors = new List<string>();

                // Send all remaining derived metrics
                foreach (var derivedMetric in derivedMetricValues)
                {
                    try
                    {
                        StatsDClient.Increment(
                            FormatDerivedMetricName(Name, derivedMetric.Key),
                            (int)derivedMetric.Value,
                            new List<Tag>(Tags.Values),
                            SampleRate);
#pragma warning restore IDE0028
                    }
                    catch (InvalidOperationException ex)
                    {
                        derivedMetricErrors.Add($"Failed to submit derived metric {FormatDerivedMetricName(Name, derivedMetric.Key)}: {ex.Message}");
                    }
                }

                if (derivedMetricErrors.Count > 0)
                {
                    return new GenericOutcome(new GameLiftError(GameLiftErrorType.METRICS_SUBMISSION_FAILED, string.Join(", ", derivedMetricErrors)));
                }

                return new GenericOutcome();
            }
            catch (Exception ex)
            {
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.METRICS_SUBMISSION_FAILED, $"Failed to flush counter metric: {ex.Message}"));
            }
        }
    }
}
