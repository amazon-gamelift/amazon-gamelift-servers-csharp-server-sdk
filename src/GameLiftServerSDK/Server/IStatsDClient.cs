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
    using System.Collections.Generic;
    using Aws.GameLift.Server.Model.Metrics;

    /// <summary>
    /// Interface for StatsDClient to enable testing.
    /// </summary>
    public interface IStatsDClient : IDisposable
    {
        /// <summary>
        /// Increments a counter metric by the given value.
        /// </summary>
        /// <param name="metric">The name of the metric.</param>
        /// <param name="value">The value to increment by.</param>
        /// <param name="tags">Tags to associate with the metric.</param>
        /// <param name="sampleRate">The sample rate for the metric.</param>
        void Increment(string metric, int value, IList<Tag> tags, SampleRate sampleRate);

        /// <summary>
        /// Decrements a counter metric by the given value.
        /// </summary>
        /// <param name="metric">The name of the metric.</param>
        /// <param name="value">The value to decrement by.</param>
        /// <param name="tags">Tags to associate with the metric.</param>
        /// <param name="sampleRate">The sample rate for the metric.</param>
        void Decrement(string metric, int value, IList<Tag> tags, SampleRate sampleRate);

        /// <summary>
        /// Sets a gauge metric to a specific value.
        /// </summary>
        /// <param name="metric">The name of the metric.</param>
        /// <param name="value">The value to set.</param>
        /// <param name="tags">Tags to associate with the metric.</param>
        /// <param name="sampleRate">The sample rate for the metric.</param>
        void Gauge(string metric, double value, IList<Tag> tags, SampleRate sampleRate);

        /// <summary>
        /// Records a timing metric.
        /// </summary>
        /// <param name="metric">The name of the metric.</param>
        /// <param name="value">The timing value in milliseconds.</param>
        /// <param name="tags">Tags to associate with the metric.</param>
        /// <param name="sampleRate">The sample rate for the metric.</param>
        void Timing(string metric, double value, IList<Tag> tags, SampleRate sampleRate);

        /// <summary>
        /// Add to the global tags list sent with every metric.
        /// If a tag with the same key already exists, it will be replaced.
        /// </summary>
        /// <param name="tag">The Tag object to be added.</param>
        void AddGlobalTag(Tag tag);

        /// <summary>
        /// Add to the global tags list sent with every metric.
        /// If a tag with the same key already exists, it will be replaced.
        /// </summary>
        /// <param name="tag">The tag string to be added (format: "key:value").</param>
        void AddGlobalTag(string tag);

        /// <summary>
        /// Remove a tag from the global tags list sent with every metric.
        /// </summary>
        /// <param name="tag">The Tag object to be removed.</param>
        void RemoveGlobalTag(Tag tag);

        /// <summary>
        /// Remove a tag from the global tags list sent with every metric.
        /// </summary>
        /// <param name="tag">The tag key to be removed.</param>
        void RemoveGlobalTag(string tag);

        /// <summary>
        /// Flushes any buffered metrics immediately.
        /// </summary>
        void Flush();
    }
}
