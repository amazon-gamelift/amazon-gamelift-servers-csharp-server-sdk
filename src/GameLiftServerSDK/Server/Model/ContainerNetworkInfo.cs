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

namespace Aws.GameLift.Server.Model
{
    /// <summary>
    /// Identifies which type of container group a container belongs to within a
    /// GameLift container fleet. The values mirror the <c>containerGroupType</c> field
    /// returned by the Discovery Server on each container instance.
    /// </summary>
#pragma warning disable S2344 // Enum names should not have 'Enum' suffix
    public enum ContainerGroupType
#pragma warning restore S2344
    {
        /// <summary>
        /// Game server replica container group. Multiple copies of this group run per instance.
        /// </summary>
        GAME_SERVER,

        /// <summary>
        /// Per-instance daemon container group. Exactly one copy of this group runs per instance.
        /// </summary>
        PER_INSTANCE,
    }

    /// <summary>
    /// Network information for a single container running on the same instance as the caller.
    /// </summary>
    public class ContainerNetworkInfo
    {
        /// <summary>
        /// The container name as defined in the container group definition.
        /// </summary>
        public string ContainerName { get; set; }

        /// <summary>
        /// The unique identifier of the container.
        /// </summary>
        public string ContainerId { get; set; }

        /// <summary>
        /// The container's IPv4 address on the Docker bridge network.
        /// </summary>
        public string IpAddress { get; set; }

        /// <summary>
        /// The container group type the container belongs to.
        /// </summary>
        public ContainerGroupType ContainerGroupType { get; set; }

        public ContainerNetworkInfo()
        {
            ContainerGroupType = ContainerGroupType.GAME_SERVER;
        }

        public ContainerNetworkInfo(string containerName, string containerId, string ipAddress, ContainerGroupType containerGroupType)
        {
            ContainerName = containerName;
            ContainerId = containerId;
            IpAddress = ipAddress;
            ContainerGroupType = containerGroupType;
        }
    }
}
