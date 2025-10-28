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

namespace Aws.GameLift.Server.Model.Metrics.DerivedMetrics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Calculate the Percentile value from a list of samples.
    /// </summary>
    public class Percentile : BaseDerivedMetric, IDerivedMetric
    {
        private const int MaxPercentile = 100;
        private readonly double percentile;

        /// <summary>
        /// Initializes a new instance of the <see cref="Percentile"/> class.
        /// </summary>
        /// <param name="percentile">The Percentile to be calculated.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the percentile is not between 0 and 100.</exception>
        public Percentile(int percentile)
        {
            if (percentile < 0 || percentile > MaxPercentile)
            {
                throw new System.ArgumentOutOfRangeException(nameof(percentile), "Percentile must be between 0 and 100.");
            }

            this.percentile = percentile;
            Name = "p" + percentile;
        }

        /// <summary>
        /// Calculate the value of the derived metric based on a list of values.
        /// </summary>
        /// <param name="samples">The list of sample to calculate from.</param>
        /// <returns>The value of the derived metric, or null if no samples are provided.</returns>
        public double? CalculateValue(IList<double> samples)
        {
            if (samples == null || samples.Count == 0)
            {
                return null;
            }

            var sortedSamples = samples.OrderBy(x => x).ToList();

            double position = (percentile / 100.0) * (sortedSamples.Count - 1);
            int lowerIndex = (int)Math.Floor(position);
            int upperIndex = (int)Math.Ceiling(position);

            if (lowerIndex == upperIndex)
            {
                return sortedSamples[lowerIndex];
            }

            // Using linear interpolation to get the value between the two indices
            double fraction = position - lowerIndex;
            double fractionalDelta = fraction * (sortedSamples[upperIndex] - sortedSamples[lowerIndex]);
            return sortedSamples[lowerIndex] + fractionalDelta;
        }
    }
}
