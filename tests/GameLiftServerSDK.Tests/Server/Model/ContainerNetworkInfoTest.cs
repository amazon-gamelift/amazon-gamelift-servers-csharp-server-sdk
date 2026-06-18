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

using Aws.GameLift.Server.Model;
using NUnit.Framework;

namespace Aws.GameLift.Tests.Server.Model
{
    [TestFixture]
    public class ContainerNetworkInfoTest
    {
        [Test]
        public void GIVEN_defaultConstructor_WHEN_created_THEN_containerGroupTypeDefaultsToGameServer()
        {
            // Given / When
            var info = new ContainerNetworkInfo();

            // Then
            Assert.AreEqual(ContainerGroupType.GAME_SERVER, info.ContainerGroupType);
            Assert.IsNull(info.ContainerName);
            Assert.IsNull(info.ContainerId);
            Assert.IsNull(info.IpAddress);
        }

        [Test]
        public void GIVEN_values_WHEN_setAndGet_THEN_returnsCorrectValues()
        {
            // Given
            var info = new ContainerNetworkInfo
            {
                ContainerName = "game-server",
                ContainerId = "aaaa1111bbbb",
                IpAddress = "172.17.0.2",
                ContainerGroupType = ContainerGroupType.PER_INSTANCE,
            };

            // When / Then
            Assert.AreEqual("game-server", info.ContainerName);
            Assert.AreEqual("aaaa1111bbbb", info.ContainerId);
            Assert.AreEqual("172.17.0.2", info.IpAddress);
            Assert.AreEqual(ContainerGroupType.PER_INSTANCE, info.ContainerGroupType);
        }

        [Test]
        public void GIVEN_defaultConstructor_WHEN_createResult_THEN_containersNetworkInfoIsEmpty()
        {
            // Given / When
            var result = new ListContainersNetworkInfoResult();

            // Then
            Assert.IsNotNull(result.ContainersNetworkInfo);
            Assert.AreEqual(0, result.ContainersNetworkInfo.Count);
        }

        [Test]
        public void GIVEN_result_WHEN_addContainerNetworkInfo_THEN_containsEntry()
        {
            // Given
            var result = new ListContainersNetworkInfoResult();
            var info = new ContainerNetworkInfo("game-server", "abc", "172.17.0.2", ContainerGroupType.GAME_SERVER);

            // When
            result.AddContainerNetworkInfo(info);

            // Then
            Assert.AreEqual(1, result.ContainersNetworkInfo.Count);
            Assert.AreEqual("game-server", result.ContainersNetworkInfo[0].ContainerName);
        }
    }
}
