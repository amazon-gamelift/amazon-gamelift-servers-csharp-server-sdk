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

using Aws.GameLift.Server;
using NUnit.Framework;

namespace Aws.GameLift.Tests.Server
{
    [TestFixture]
    public class GameLiftServerApiTest
    {
        [Test]
        public void GIVEN_validSdkVersion_WHEN_GetSdkVersion_THEN_returnsVersion()
        {
            // Given
            // When
            AwsStringOutcome outcome = GameLiftServerAPI.GetSdkVersion();

            // Then
            Assert.IsTrue(outcome.Success);
            Assert.AreEqual("5.4.0", outcome.Result);
        }
    }
}
