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
namespace Aws.GameLift.Server
{
    using System;

    /// <summary>
    /// Random number generator that provides thread-safe random number generation.
    /// Uses ThreadLocal to ensure each thread has its own Random instance when no seed is provided,
    /// or a synchronized Random instance when a seed is specified.
    /// </summary>
    public class RandomGenerator
    {
        private readonly Random seededRandom;
        private readonly object lockObject;

        /// <summary>
        /// Initializes a new instance of the <see cref="RandomGenerator"/> class.
        /// </summary>
        /// <param name="seed">Optional seed for the random number generator. If null, uses thread-local random instances.</param>
        public RandomGenerator(int? seed)
        {
            if (seed.HasValue)
            {
                seededRandom = new Random(seed.Value);
                lockObject = new object();
            }
        }

        /// <summary>
        /// Returns a random floating-point number between 0.0 and 1.0.
        /// </summary>
        /// <returns>A double-precision floating point number that is greater than or equal to 0.0, and less than 1.0.</returns>
        public double NextDouble()
        {
            if (seededRandom != null)
            {
                lock (lockObject)
                {
                    return seededRandom.NextDouble();
                }
            }
            else
            {
                return ThreadSafeRandom.NextDouble();
            }
        }
    }
}
