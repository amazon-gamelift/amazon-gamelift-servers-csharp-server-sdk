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
    using System.Collections.Generic;

    /// <summary>
    /// Get the Count (number of elements) from a list of samples.
    /// </summary>
    public class Count : BaseDerivedMetric, IDerivedMetric
    {
        /// <summary>
        /// Calculate the count of elements in the list of samples.
        /// </summary>
        /// <param name="samples">The list of samples to count.</param>
        /// <returns>The number of elements in the provided samples, 0 if empty.</returns>
        public double? CalculateValue(IList<double> samples)
        {
            if (samples == null)
            {
                return 0.0;
            }

            return samples.Count;
        }
    }
}
