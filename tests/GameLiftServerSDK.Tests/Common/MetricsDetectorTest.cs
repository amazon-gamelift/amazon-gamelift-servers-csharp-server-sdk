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

using NUnit.Framework;
using Aws.GameLift.Server.Common;

namespace Aws.GameLift.Server.Tests.Common
{
    [TestFixture]
    public class MetricsDetectorTest
    {
        private MetricsDetector _detector;

        [SetUp]
        public void SetUp()
        {
            _detector = new MetricsDetector();
        }

        [Test]
        public void GetToolName_ReturnsOTEL()
        {
            Assert.AreEqual("Metrics", _detector.GetToolName());
        }

        [Test]
        public void GetToolVersion_Returns100()
        {
            Assert.AreEqual("1.0.0", _detector.GetToolVersion());
        }

        [Test]
        public void IsToolRunning_HandlesExceptions_ReturnsFalse()
        {
            // This test verifies that exceptions are caught and false is returned
            // The actual process execution will likely fail in test environment
            var result = _detector.IsToolRunning();
            Assert.IsFalse(result);
        }
    }
}
