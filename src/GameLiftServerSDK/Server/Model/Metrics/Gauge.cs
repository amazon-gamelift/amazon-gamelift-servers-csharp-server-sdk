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
    /// Represents a gauge metric that can be set to a specific value.
    /// </summary>
    public class Gauge : MetricBase, IGauge
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Gauge"/> class.
        /// </summary>
        /// <param name="name">The name of the gauge metric.</param>
        /// <param name="tags">The tags associated with this gauge.</param>
        /// <param name="statsDClient">The StatsD client to use for sending metrics.</param>
        /// <param name="sampleRate">The sampling rate for the gauge.</param>
        /// <param name="derivedMetrics">A Set of the Derived metric types to be calculated on flush.</param>
        /// <param name="randomSeed">Optional seed for the random number generator.</param>
        public Gauge(string name, IList<Tag> tags, IStatsDClient statsDClient, SampleRate sampleRate, ISet<IDerivedMetric> derivedMetrics, int? randomSeed)
            : base(name, tags, statsDClient, sampleRate, derivedMetrics, randomSeed)
        {
            DerivedMetrics.Add(new Latest());
        }

        /// <summary>
        /// Sets the gauge to the specified value.
        /// </summary>
        /// <param name="value">The value to set the gauge to.</param>
        /// <returns>A GenericOutcome indicating the success or failure of the operation.</returns>
        public GenericOutcome Set(double value)
        {
            try
            {
                SetValue(value);
                return new GenericOutcome();
            }
            catch (Exception ex)
            {
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.METRICS_CONFIGURATION_FAILED, $"Failed to set gauge value: {ex.Message}"));
            }
        }

        /// <summary>
        /// Adds the specified value to the current gauge value.
        /// </summary>
        /// <param name="value">The value to add to the gauge.</param>
        /// <returns>A GenericOutcome indicating the success or failure of the operation.</returns>
        public GenericOutcome Add(double value)
        {
            try
            {
                AdjustValue(value);
                return new GenericOutcome();
            }
            catch (Exception ex)
            {
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.METRICS_CONFIGURATION_FAILED, $"Failed to add to gauge: {ex.Message}"));
            }
        }

        /// <summary>
        /// Subtracts the specified value from the current gauge value.
        /// </summary>
        /// <param name="value">The value to subtract from the gauge.</param>
        /// <returns>A GenericOutcome indicating the success or failure of the operation.</returns>
        public GenericOutcome Subtract(double value)
        {
            try
            {
                AdjustValue(-value);
                return new GenericOutcome();
            }
            catch (Exception ex)
            {
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.METRICS_CONFIGURATION_FAILED, $"Failed to subtract from gauge: {ex.Message}"));
            }
        }

        /// <summary>
        /// Resets the gauge to zero.
        /// </summary>
        /// <returns>A GenericOutcome indicating the success or failure of the operation.</returns>
        public GenericOutcome Reset()
        {
            try
            {
                SetValue(0.0);
                return new GenericOutcome();
            }
            catch (Exception ex)
            {
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.METRICS_CONFIGURATION_FAILED, $"Failed to reset gauge: {ex.Message}"));
            }
        }

        /// <summary>
        /// Increments the gauge by 1.
        /// </summary>
        /// <returns>A GenericOutcome indicating the success or failure of the operation.</returns>
        public GenericOutcome Increment()
        {
            try
            {
                AdjustValue(1);
                return new GenericOutcome();
            }
            catch (Exception ex)
            {
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.METRICS_CONFIGURATION_FAILED, $"Failed to increment gauge: {ex.Message}"));
            }
        }

        /// <summary>
        /// Decrements the gauge by 1.
        /// </summary>
        /// <returns>A GenericOutcome indicating the success or failure of the operation.</returns>
        public GenericOutcome Decrement()
        {
            try
            {
                AdjustValue(-1);
                return new GenericOutcome();
            }
            catch (Exception ex)
            {
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.METRICS_CONFIGURATION_FAILED, $"Failed to decrement gauge: {ex.Message}"));
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
                // Send the latest value as base metric
                StatsDClient.Gauge(Name, derivedMetricValues[nameof(Latest)], new List<Tag>(Tags.Values), SampleRate);
                // Remove Latest from derived metrics to avoid double submission
                derivedMetricValues.Remove(nameof(Latest));

                List<string> derivedMetricErrors = new List<string>();

                // Send all remaining derived metrics
                foreach (var derivedMetric in derivedMetricValues)
                {
                    try
                    {
                        StatsDClient.Gauge(
                            FormatDerivedMetricName(Name, derivedMetric.Key),
                            derivedMetric.Value,
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
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.METRICS_SUBMISSION_FAILED, $"Failed to flush gauge metric: {ex.Message}"));
            }
        }
    }
}
