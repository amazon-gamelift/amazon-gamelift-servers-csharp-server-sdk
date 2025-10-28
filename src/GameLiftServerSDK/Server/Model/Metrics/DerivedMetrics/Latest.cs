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
    using System.Linq;

    /// <summary>
    /// Get the latest value from a list of samples.
    /// </summary>
    public class Latest : BaseDerivedMetric, IDerivedMetric
    {
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

            return samples.Last();
        }
    }
}
