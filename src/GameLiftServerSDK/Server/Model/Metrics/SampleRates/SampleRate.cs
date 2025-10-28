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
    /// <summary>
    /// Base class for sample rate specifications that control how frequently metrics are sampled.
    /// </summary>
    public abstract class SampleRate
    {
        /// <summary>
        /// Implicit conversion from SampleRate to double for backward compatibility.
        /// </summary>
        /// <param name="sampleRate">The sample rate to convert.</param>
        /// <returns>The double representation of the sample rate.</returns>
        public static implicit operator double(SampleRate sampleRate)
        {
            return sampleRate?.ToDouble() ?? 1.0;
        }

        /// <summary>
        /// Gets the double value representation of the sample rate for use with StatsD.
        /// </summary>
        /// <returns>A double value between 0.0 and 1.0 representing the sample rate.</returns>
        public abstract double ToDouble();
    }
}
