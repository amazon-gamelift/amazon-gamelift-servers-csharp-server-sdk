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

using System.Collections.Generic;

namespace Aws.GameLift.Server.Model
{
    /// <summary>
    /// Result of a <c>ListContainersNetworkInfo</c> call. Carries network information for
    /// every container currently running on the same instance as the caller.
    /// </summary>
    public class ListContainersNetworkInfoResult
    {
        /// <summary>
        /// Flat list of container network records.
        /// </summary>
        public IList<ContainerNetworkInfo> ContainersNetworkInfo { get; set; }

        public ListContainersNetworkInfoResult()
        {
            ContainersNetworkInfo = new List<ContainerNetworkInfo>();
        }

        public ListContainersNetworkInfoResult(IList<ContainerNetworkInfo> containersNetworkInfo)
        {
            ContainersNetworkInfo = containersNetworkInfo ?? new List<ContainerNetworkInfo>();
        }

        /// <summary>
        /// Adds a single container network record to the result.
        /// </summary>
        public void AddContainerNetworkInfo(ContainerNetworkInfo value)
        {
            ContainersNetworkInfo.Add(value);
        }
    }
}
