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
    /// Sample rate that causes all metric events to be sampled (100% sampling).
    /// </summary>
#pragma warning disable S3453 // Singleton class should not have public constructors
    public sealed class SampleAll : SampleRate
#pragma warning restore S3453
    {
        /// <summary>
        /// Gets the singleton instance of SampleAll.
        /// </summary>
        public static readonly SampleAll Instance = new SampleAll();

        private SampleAll()
        {
        }

        /// <summary>
        /// Returns 1.0 indicating 100% sampling rate.
        /// </summary>
        /// <returns>1.0.</returns>
        public override double ToDouble()
        {
            return 1.0;
        }
    }
}
