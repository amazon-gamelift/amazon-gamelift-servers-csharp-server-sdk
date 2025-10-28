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

    /// <summary>
    /// Sample rate that allows fractional sampling between 0% and 100%.
    /// </summary>
    public sealed class SampleFractional : SampleRate
    {
        private readonly double rate;

        /// <summary>
        /// Initializes a new instance of the <see cref="SampleFractional"/> class.
        /// </summary>
        /// <param name="rate">The sampling rate as a double between 0.0 and 1.0.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when rate is not between 0.0 and 1.0.</exception>
        public SampleFractional(double rate)
        {
            if (rate < 0.0 || rate > 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(rate), "Sample rate must be between 0.0 and 1.0.");
            }

            this.rate = rate;
        }

        /// <summary>
        /// Returns the fractional sampling rate.
        /// </summary>
        /// <returns>The sampling rate as a double between 0.0 and 1.0.</returns>
        public override double ToDouble()
        {
            return rate;
        }
    }
}
