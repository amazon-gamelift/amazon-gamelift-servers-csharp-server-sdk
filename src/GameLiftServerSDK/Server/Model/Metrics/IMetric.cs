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
    using System.Collections.Generic;

    /// <summary>
    /// Base interface for all GameLift Servers metrics.
    /// </summary>
    public interface IMetric
    {
        /// <summary>
        /// Gets the name identifier for the metric.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets a thread-safe copy of the tags associated with this metric.
        /// </summary>
        IDictionary<string, Tag> Tags { get; }

        /// <summary>
        /// Gets the sample rate for the metric.
        /// </summary>
        SampleRate SampleRate { get; }

        /// <summary>
        /// Gets the current value of the metric.
        /// </summary>
        double CurrentValue { get; }

        /// <summary>
        /// Adds a tag to the metric. If a tag with the same key already exists, it will be replaced.
        /// </summary>
        /// <param name="tag">The tag to add.</param>
        /// <returns>A GenericOutcome indicating the success or failure of the operation.</returns>
        GenericOutcome AddTag(Tag tag);

        /// <summary>
        /// Adds a tag to the metric. If a tag with the same key already exists, it will be replaced.
        /// </summary>
        /// <param name="tagValue">The string value of the tag to add (format: "key:value").</param>
        /// <returns>A GenericOutcome indicating the success or failure of the operation.</returns>
        GenericOutcome AddTag(string tagValue);

        /// <summary>
        /// Removes a tag from the metric.
        /// </summary>
        /// <param name="tag">The tag to remove.</param>
        /// <returns>A GenericOutcome indicating the success or failure of the operation.</returns>
        GenericOutcome RemoveTag(Tag tag);

        /// <summary>
        /// Removes a tag from the metric.
        /// </summary>
        /// <param name="key">The tag key to remove.</param>
        /// <returns>A GenericOutcome indicating the success or failure of the operation.</returns>
        GenericOutcome RemoveTag(string key);

        /// <summary>
        /// Flushes the metric data to the GameLift service.
        /// </summary>
        /// <returns>A GenericOutcome indicating the success or failure of the operation.</returns>
        GenericOutcome Flush();
    }

    /// <summary>
    /// Interface for counter metrics that track incremental values.
    /// </summary>
    public interface ICounter : IMetric
    {
        /// <summary>
        /// Increments the counter by 1.
        /// </summary>
        /// <returns>A GenericOutcome indicating the success or failure of the operation.</returns>
        GenericOutcome Increment();

        /// <summary>
        /// Adds the specified value to the current counter value.
        /// </summary>
        /// <param name="value">The value to add to the counter.</param>
        /// <returns>A GenericOutcome indicating the success or failure of the operation.</returns>
        GenericOutcome Add(int value);
    }

    /// <summary>
    /// Interface for gauge metrics that track a value that can arbitrarily go up and down.
    /// </summary>
    public interface IGauge : IMetric
    {
        /// <summary>
        /// Sets the gauge to the specified value.
        /// </summary>
        /// <param name="value">The value to set the gauge to.</param>
        /// <returns>A GenericOutcome indicating the success or failure of the operation.</returns>
        GenericOutcome Set(double value);

        /// <summary>
        /// Increments the gauge by 1.
        /// </summary>
        /// <returns>A GenericOutcome indicating the success or failure of the operation.</returns>
        GenericOutcome Increment();

        /// <summary>
        /// Decrements the gauge by 1.
        /// </summary>
        /// <returns>A GenericOutcome indicating the success or failure of the operation.</returns>
        GenericOutcome Decrement();

        /// <summary>
        /// Adds the specified value to the current gauge value.
        /// </summary>
        /// <param name="value">The value to add to the gauge.</param>
        /// <returns>A GenericOutcome indicating the success or failure of the operation.</returns>
        GenericOutcome Add(double value);

        /// <summary>
        /// Subtracts the specified value from the current gauge value.
        /// </summary>
        /// <param name="value">The value to subtract from the gauge.</param>
        /// <returns>A GenericOutcome indicating the success or failure of the operation.</returns>
        GenericOutcome Subtract(double value);

        /// <summary>
        /// Resets the gauge value to zero.
        /// </summary>
        /// <returns>A GenericOutcome indicating the success or failure of the operation.</returns>
        GenericOutcome Reset();
    }

    /// <summary>
    /// Interface for timer metrics that track durations.
    /// </summary>
    public interface ITimer : IMetric
    {
        /// <summary>
        /// Records a duration in milliseconds.
        /// </summary>
        /// <param name="milliseconds">The duration in milliseconds to record.</param>
        /// <returns>A GenericOutcome indicating the success or failure of the operation.</returns>
        GenericOutcome Set(double milliseconds);
    }
}
