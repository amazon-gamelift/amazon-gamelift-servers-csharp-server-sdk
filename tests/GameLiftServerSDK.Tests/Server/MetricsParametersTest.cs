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

namespace Aws.GameLift.Server.Tests
{
    [TestFixture]
    public class MetricsParametersTest
    {
        private readonly string statsdHost = "localhost";
        private readonly int statsdPort = 8125;
        private readonly string crashReporterHost = "crash-host";
        private readonly int crashReporterPort = 8126;
        private readonly int flushIntervalMs = 5000;
        private readonly int maxPacketSize = 1024;

        [Test]
        public void GIVEN_fullConstructorParams_WHEN_creatingMetricsParameters_THEN_setsAllValues()
        {
            // When
            var parameters = new MetricsParameters(
                statsdHost, statsdPort, crashReporterHost, crashReporterPort, flushIntervalMs, maxPacketSize);

            // Then
            Assert.AreEqual(statsdHost, parameters.StatsdHost);
            Assert.AreEqual(statsdPort, parameters.StatsdPort);
            Assert.AreEqual(crashReporterHost, parameters.CrashReporterHost);
            Assert.AreEqual(crashReporterPort, parameters.CrashReporterPort);
            Assert.AreEqual(flushIntervalMs, parameters.FlushIntervalMs);
            Assert.AreEqual(maxPacketSize, parameters.MaxPacketSize);
        }

        [Test]
        public void GIVEN_twoIdenticalParameters_WHEN_comparing_THEN_areEqual()
        {
            // Given
            var params1 = new MetricsParameters(statsdHost, statsdPort, crashReporterHost, crashReporterPort, flushIntervalMs, maxPacketSize);
            var params2 = new MetricsParameters(statsdHost, statsdPort, crashReporterHost, crashReporterPort, flushIntervalMs, maxPacketSize);

            // When & Then
            Assert.IsTrue(params1.Equals(params2));
            Assert.IsTrue(params2.Equals(params1));
        }

        [Test]
        public void GIVEN_twoDifferentParameters_WHEN_comparing_THEN_areNotEqual()
        {
            // Given
            var params1 = new MetricsParameters(statsdHost, statsdPort, crashReporterHost, crashReporterPort, flushIntervalMs, maxPacketSize);
            var params2 = new MetricsParameters("different", statsdPort, crashReporterHost, crashReporterPort, flushIntervalMs, maxPacketSize);

            // When & Then
            Assert.IsFalse(params1.Equals(params2));
            Assert.IsFalse(params2.Equals(params1));
        }

        [Test]
        public void GIVEN_nullParameter_WHEN_comparing_THEN_isNotEqual()
        {
            // Given
            var params1 = new MetricsParameters(statsdHost, statsdPort, crashReporterHost, crashReporterPort, flushIntervalMs, maxPacketSize);

            // When & Then
            Assert.IsFalse(params1.Equals(null));
        }
    }
}
