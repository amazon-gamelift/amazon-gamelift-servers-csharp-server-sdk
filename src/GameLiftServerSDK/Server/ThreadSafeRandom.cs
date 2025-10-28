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
namespace Aws.GameLift
{
    using System;
    using System.Threading;

    /// <summary>
    /// Thread-safe random number generator using ThreadLocal to ensure each thread
    /// has its own Random instance, avoiding the need for locks.
    /// </summary>
    internal static class ThreadSafeRandom
    {
        private static readonly ThreadLocal<Random> ThreadLocalRandom = new ThreadLocal<Random>(() => new Random());

        /// <summary>
        /// Gets a Random instance for the current thread.
        /// </summary>
        public static Random Instance => ThreadLocalRandom.Value;

        /// <summary>
        /// Returns a random floating-point number between 0.0 and 1.0.
        /// </summary>
        /// <returns>A double-precision floating point number that is greater than or equal to 0.0, and less than 1.0.</returns>
        public static double NextDouble()
        {
            return Instance.NextDouble();
        }
    }
}
