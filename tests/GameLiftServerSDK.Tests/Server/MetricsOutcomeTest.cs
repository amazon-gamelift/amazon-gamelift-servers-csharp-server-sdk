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

namespace Aws.GameLift.Server.Tests
{
    [TestFixture]
    public class MetricsOutcomeTest
    {
        [Test]
        public void GIVEN_successfulMetrics_WHEN_creatingOutcome_THEN_setsSuccessState()
        {
            // Given
            var metrics = Metrics.Create().Build();
            
            // When
            var outcome = new MetricsOutcome(metrics);
            
            // Then
            Assert.IsTrue(outcome.Success);
            Assert.IsNull(outcome.Error);
            Assert.AreEqual(metrics, outcome.Result);
        }

        [Test]
        public void GIVEN_errorCondition_WHEN_creatingOutcome_THEN_setsErrorState()
        {
            // Given
            var error = new GameLiftError(GameLiftErrorType.METRICS_CONFIGURATION_FAILED, "Test error");
            
            // When
            var outcome = new MetricsOutcome(error);
            
            // Then
            Assert.IsFalse(outcome.Success);
            Assert.AreEqual(error, outcome.Error);
            Assert.IsNull(outcome.Result);
        }

        [Test]
        public void GIVEN_metricsOutcome_WHEN_checkingInheritance_THEN_inheritsFromGenericOutcome()
        {
            // Given & When
            var outcome = new MetricsOutcome(new GameLiftError(GameLiftErrorType.VALIDATION_EXCEPTION));
            
            // Then
            Assert.IsInstanceOf<GenericOutcome>(outcome);
        }
    }
}
