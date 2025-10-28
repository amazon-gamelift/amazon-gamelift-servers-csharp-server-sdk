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
    public class BaseDerivedMetric
    {
        /// <summary>
        /// Gets the name of the derived metric.
        /// </summary>
        public string Name { get; protected set; }

        protected BaseDerivedMetric()
        {
            Name = GetType().Name;
        }

        public override bool Equals(object obj)
        {
            return obj != null && obj.GetType() == GetType();
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }
    }
}
