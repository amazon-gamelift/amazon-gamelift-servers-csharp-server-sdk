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
    /// Interface class for a derived metric calculation.
    /// </summary>
    public interface IDerivedMetric
    {
        /// <summary>
        /// Gets the name of the derived metric.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Calculate the value of the derived metric based on a list of values.
        /// </summary>
        /// <param name="samples">The list of sample to calculate from.</param>
        /// <returns>The value of the derived metric, or null if it doesn't make sense to return a value given an empty samples list.</returns>
        double? CalculateValue(IList<double> samples);
    }
}
