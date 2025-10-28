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
    /// Represents a timer metric that measures durations.
    /// </summary>
    public class Timer : MetricBase, ITimer
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Timer"/> class.
        /// </summary>
        /// <param name="name">The name of the timer metric.</param>
        /// <param name="tags">The tags associated with this timer.</param>
        /// <param name="statsDClient">The StatsD client to use for sending metrics.</param>
        /// <param name="sampleRate">The sample rate for the timer.</param>
        /// <param name="derivedMetrics">A Set of the Derived metric types to be calculated on flush.</param>
        /// <param name="randomSeed">Optional seed for the random number generator.</param>
        public Timer(string name, IList<Tag> tags, IStatsDClient statsDClient, SampleRate sampleRate, ISet<IDerivedMetric> derivedMetrics, int? randomSeed)
            : base(name, tags, statsDClient, sampleRate, derivedMetrics, randomSeed)
        {
            DerivedMetrics.Add(new Mean());
        }

        /// <summary>
        /// Records a timing measurement.
        /// </summary>
        /// <param name="milliseconds">The duration in milliseconds to record.</param>
        /// <returns>A GenericOutcome indicating the success or failure of the operation.</returns>
        public GenericOutcome Set(double milliseconds)
        {
            try
            {
                SetValue(milliseconds);
                return new GenericOutcome();
            }
            catch (Exception ex)
            {
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.METRICS_CONFIGURATION_FAILED, $"Failed to set timer value: {ex.Message}"));
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
                // Send the mean timing metric as base value
                StatsDClient.Timing(Name, derivedMetricValues[nameof(Mean)], new List<Tag>(Tags.Values), SampleRate);
                // Remove Mean from derived metrics to avoid double submission
                derivedMetricValues.Remove(nameof(Mean));

                List<string> derivedMetricErrors = new List<string>();

                // Send all remaining derived metrics
                foreach (var derivedMetric in derivedMetricValues)
                {
                    try
                    {
                        StatsDClient.Timing(
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
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.METRICS_SUBMISSION_FAILED, $"Failed to flush timer metric: {ex.Message}"));
            }
        }
    }
}
