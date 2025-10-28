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
using Aws.GameLift.Server.Model;
using NUnit.Framework;

namespace Aws.GameLift.Tests.Server
{
    [TestFixture]
    public class ValidationTest
    {
        private const string TestGameSessionArn =
            "arn:aws:gamelift:us-west-2::gamesession/fleet-test/location-test/gsess-test";

        private const string TestMatchmakingConfigurationArn =
            "arn:aws:gamelift:us-west-2:000000000000:matchmakingconfiguration/test";

        [Test]
        public void GIVEN_validParams_WHEN_ValidateServerParameters_THEN_success()
        {
            // Given
            var input = new ServerParameters(
                "wss://test.url",
                "test-process-id",
                "test-host-id",
                "test-fleet-id",
                "test-auth-token"
            );

            // When
            var outcome = Validation.ValidateServerParameters(input);

            // Then
            Assert.IsTrue(outcome.Success);
        }

        [Test]
        public void GIVEN_validParamsOtherCombinations_WHEN_ValidateServerParameters_THEN_success()
        {
            // Given
            var input = new ServerParameters(
                "wss://test.url",
                "test-process-id",
                "test-host-id",
                "test-fleet-id",
                ""
            );
            var computeType = "";

            // When - compute type is container
            // either aws region or authtoken required
            input.AwsRegion = "test-region";
            var containerRegionGivenOutcome = Validation.ValidateServerParameters(input,isContainerComputeType: true);
            input.AwsRegion = "";
            input.AuthToken = "test-auth-token";
            var containerAuthTokenGivenOutcome = Validation.ValidateServerParameters(input, isContainerComputeType: true);
            // host id not required
            input.HostId = "";
            var containerHostIdNotRequiredOutcome = Validation.ValidateServerParameters(input, isContainerComputeType: true);
            input.AuthToken = "";
            computeType = "";
            input.HostId = "test-host-id";

            // When - AWS credentials given
            input.AwsRegion = "test-region";
            input.AccessKey = "test-access-key";
            input.SecretKey = "test-secret-key";
            var credentialsGivenOutcome = Validation.ValidateServerParameters(input, isContainerComputeType: true);

            // Then
            Assert.IsTrue(containerRegionGivenOutcome.Success);
            Assert.IsTrue(containerAuthTokenGivenOutcome.Success);
            Assert.IsTrue(credentialsGivenOutcome.Success);
        }

        [Test]
        public void GIVEN_invalidParams_WHEN_ValidateServerParameters_THEN_failure()
        {
            // Given
            var input = new ServerParameters(
                "wss://test.url",
                "test-process-id",
                "test-host-id",
                "test-fleet-id",
                "test-auth-token"
            );

            // When - websocket empty
            input.WebSocketUrl = "";
            var websocketEmptyOutcome = Validation.ValidateServerParameters(input);
            input.WebSocketUrl = "wss://test.url";

            // When - process id empty
            input.ProcessId = "";
            var processIdEmptyOutcome = Validation.ValidateServerParameters(input);
            input.ProcessId = "test-process-id";

            // When - host id invalid
            // Empty
            input.HostId = "";
            var hostIdEmptyOutcome = Validation.ValidateServerParameters(input);

            // Too long
            input.HostId = new string('a', Validation.MaxStringLengthId + 1);
            var hostIdTooLongOutcome = Validation.ValidateServerParameters(input);

            // Invalid characters
            input.HostId = "test-host-id!";
            var hostIdInvalidCharactersOutcome = Validation.ValidateServerParameters(input);
            input.HostId = "test-host-id";

            // When - fleet id invalid
            // Empty
            input.FleetId = "";
            var fleetIdEmptyOutcome = Validation.ValidateServerParameters(input);
            // Too long
            input.FleetId = new string('a', Validation.MaxStringLengthId + 1);
            var fleetIdTooLongOutcome = Validation.ValidateServerParameters(input);
            // Invalid characters
            input.FleetId = "test-fleet-id!";
            var fleetIdInvalidCharactersOutcome = Validation.ValidateServerParameters(input);
            input.FleetId = "test-fleet-id";

            // When - invalid credentials
            // Empty
            input.AuthToken = "";
            var noAuthTokenOutcome = Validation.ValidateServerParameters(input);
            // Missing all credentials
            input.AwsRegion = "";
            input.AccessKey = "test-access-key";
            input.SecretKey = "test-secret-key";
            var missingAwsCredentialsOutcome = Validation.ValidateServerParameters(input);

            // Then
            // Websocket assertion
            Assert.IsFalse(websocketEmptyOutcome.Success);
            Assert.AreEqual("WebSocketUrl is required.", websocketEmptyOutcome.Error.ErrorMessage);

            // ProcessId assertion
            Assert.IsFalse(processIdEmptyOutcome.Success);
            Assert.AreEqual("ProcessId is required.", processIdEmptyOutcome.Error.ErrorMessage);

            // HostId assertions
            Assert.IsFalse(hostIdEmptyOutcome.Success);
            Assert.AreEqual("HostId is required.", hostIdEmptyOutcome.Error.ErrorMessage);
            Assert.IsFalse(hostIdTooLongOutcome.Success);
            Assert.AreEqual(
                string.Format("HostId is invalid. Length must be between 1 and {0} characters.",
                    Validation.MaxStringLengthId), hostIdTooLongOutcome.Error.ErrorMessage);
            Assert.IsFalse(hostIdInvalidCharactersOutcome.Success);
            Assert.AreEqual(string.Format("HostId is invalid. Must match the pattern: {0}.", Validation.ComputeIdRegex),
                hostIdInvalidCharactersOutcome.Error.ErrorMessage);

            // FleetId assertions
            Assert.IsFalse(fleetIdEmptyOutcome.Success);
            Assert.AreEqual("FleetId is required.", fleetIdEmptyOutcome.Error.ErrorMessage);
            Assert.IsFalse(fleetIdTooLongOutcome.Success);
            Assert.AreEqual(
                string.Format("FleetId is invalid. Length must be between 1 and {0} characters.",
                    Validation.MaxStringLengthId), fleetIdTooLongOutcome.Error.ErrorMessage);
            Assert.IsFalse(fleetIdInvalidCharactersOutcome.Success);
            Assert.AreEqual(string.Format("FleetId is invalid. Must match the pattern: {0}.", Validation.GuidRegex),
                fleetIdInvalidCharactersOutcome.Error.ErrorMessage);

            // Invalid credential assertions
            Assert.IsFalse(noAuthTokenOutcome.Success);
            Assert.AreEqual(Validation.AwsCredentialsError, noAuthTokenOutcome.Error.ErrorMessage);
            Assert.IsFalse(missingAwsCredentialsOutcome.Success);
            Assert.AreEqual(Validation.AwsCredentialsError, missingAwsCredentialsOutcome.Error.ErrorMessage);
        }

        [Test]
        public void GIVEN_validParams_WHEN_ValidateProcessParameters_THEN_success()
        {
            // Given
            var input = new ProcessParameters();
            input.Port = Validation.PortMin + 100;

            // When
            var outcome = Validation.ValidateProcessParameters(input);

            // Then
            Assert.IsTrue(outcome.Success);
        }

        [Test]
        public void GIVEN_invalidParams_WHEN_ValidateProcessParameters_THEN_failure()
        {
            // Given
            var input = new ProcessParameters();

            // When - invalid port
            // Too low
            input.Port = Validation.PortMin - 1;
            var portTooLowOutcome = Validation.ValidateProcessParameters(input);
            // Too high
            input.Port = Validation.PortMax + 1;
            var portTooHighOutcome = Validation.ValidateProcessParameters(input);

            // Then
            Assert.IsFalse(portTooLowOutcome.Success);
            Assert.AreEqual(
                string.Format("Port must be between {0} and {1}", Validation.PortMin, Validation.PortMax),
                portTooLowOutcome.Error.ErrorMessage);
            Assert.IsFalse(portTooHighOutcome.Success);
            Assert.AreEqual(
                string.Format("Port must be between {0} and {1}", Validation.PortMin, Validation.PortMax),
                portTooHighOutcome.Error.ErrorMessage);
        }

        [Test]
        public void GIVEN_validParams_WHEN_ValidatePlayerSessionCreationPolicy_THEN_success()
        {
            // Given
            var acceptAll = PlayerSessionCreationPolicy.ACCEPT_ALL;
            var denyAll = PlayerSessionCreationPolicy.DENY_ALL;

            // When
            var acceptAllOutcome = Validation.ValidatePlayerSessionCreationPolicy(acceptAll);
            var denyAllOutcome = Validation.ValidatePlayerSessionCreationPolicy(denyAll);

            // Then
            Assert.IsTrue(acceptAllOutcome.Success);
            Assert.IsTrue(denyAllOutcome.Success);
        }

        [Test]
        public void GIVEN_invalidParams_WHEN_ValidatePlayerSessionCreationPolicy_THEN_failure()
        {
            // Given
            var invalid = PlayerSessionCreationPolicy.NOT_SET;

            // When
            var outcome = Validation.ValidatePlayerSessionCreationPolicy(invalid);

            // Then
            Assert.IsFalse(outcome.Success);
            Assert.AreEqual(Validation.PlayerSessionCreationPolicyError, outcome.Error.ErrorMessage);
        }

        [Test]
        public void GIVEN_validParams_WHEN_ValidateDescribePlayerSessionsRequest_THEN_success()
        {
            // Given
            var input = new DescribePlayerSessionsRequest();
            input.PlayerSessionId = "test-player-session-id";
            input.PlayerSessionStatusFilter = "ACTIVE";

            // When - player session id is given
            var playerSessionIdGivenOutcome = Validation.ValidateDescribePlayerSessionsRequest(input);
            input.PlayerSessionId = "";

            // When - game session id is given
            input.GameSessionId = "test-game-session-id";
            var gameSessionIdGivenOutcome = Validation.ValidateDescribePlayerSessionsRequest(input);
            input.GameSessionId = "";

            // When - player id is given
            input.PlayerId = "player-id";
            var playerIdGivenOutcome = Validation.ValidateDescribePlayerSessionsRequest(input);
            input.PlayerId = "";

            // Then
            Assert.IsTrue(playerSessionIdGivenOutcome.Success);
            Assert.IsTrue(gameSessionIdGivenOutcome.Success);
            Assert.IsTrue(playerIdGivenOutcome.Success);
        }

        [Test]
        public void GIVEN_invalidParams_WHEN_ValidateDescribePlayerSessionsRequest_THEN_failure()
        {
            // Given
            var input = new DescribePlayerSessionsRequest();

            // When - too many params
            input.PlayerSessionId = "test-player-session-id";
            input.PlayerId = "test-player-id";
            var tooManyParamsOutcome = Validation.ValidateDescribePlayerSessionsRequest(input);

            // When - no params
            input.PlayerSessionId = "";
            input.PlayerId = "";
            var noParamsOutcome = Validation.ValidateDescribePlayerSessionsRequest(input);
            input.PlayerSessionId = "test-player-session-id";

            // When - invalid filter
            input.PlayerSessionStatusFilter = "INVALID";
            var invalidFilterOutcome = Validation.ValidateDescribePlayerSessionsRequest(input);
            input.PlayerSessionStatusFilter = "ACTIVE";

            // When - player session id is invalid
            // Too long
            input.PlayerSessionId = new string('a', Validation.MaxStringLengthLong + 1);
            var playerSessionIdTooLongOutcome = Validation.ValidateDescribePlayerSessionsRequest(input);
            input.PlayerSessionId = "";

            // When - game session id is invalid
            // Too long
            input.GameSessionId = new string('a', Validation.MaxStringLengthArn + 1);
            var gameSessionIdTooLongOutcome = Validation.ValidateDescribePlayerSessionsRequest(input);
            // Invalid characters
            input.GameSessionId = "test-game-session-id!";
            var gameSessionIdInvalidOutcome = Validation.ValidateDescribePlayerSessionsRequest(input);
            input.GameSessionId = "";

            // When - player id is invalid
            // Too long
            input.PlayerId = new string('a', Validation.MaxStringLengthLong + 1);
            var playerIdTooLongOutcome = Validation.ValidateDescribePlayerSessionsRequest(input);

            // Then
            Assert.IsFalse(tooManyParamsOutcome.Success);
            Assert.AreEqual(Validation.DescribePlayerSessionsOneFieldError, tooManyParamsOutcome.Error.ErrorMessage);
            Assert.IsFalse(noParamsOutcome.Success);
            Assert.AreEqual(Validation.DescribePlayerSessionsOneFieldError, noParamsOutcome.Error.ErrorMessage);
            Assert.IsFalse(invalidFilterOutcome.Success);
            Assert.AreEqual(Validation.PlayerSessionStatusFilterError, invalidFilterOutcome.Error.ErrorMessage);
            Assert.IsFalse(playerSessionIdTooLongOutcome.Success);
            Assert.AreEqual(
                string.Format("PlayerSessionId is invalid. Length must be between 1 and {0} characters.",
                    Validation.MaxStringLengthLong), playerSessionIdTooLongOutcome.Error.ErrorMessage);
            Assert.IsFalse(gameSessionIdTooLongOutcome.Success);
            Assert.AreEqual(
                string.Format("GameSessionId is invalid. Length must be between 1 and {0} characters.",
                    Validation.MaxStringLengthArn), gameSessionIdTooLongOutcome.Error.ErrorMessage);
            Assert.IsFalse(gameSessionIdInvalidOutcome.Success);
            Assert.AreEqual(string.Format("GameSessionId is invalid. {0}", Validation.InvalidArnError),
                gameSessionIdInvalidOutcome.Error.ErrorMessage);
            Assert.IsFalse(playerIdTooLongOutcome.Success);
            Assert.AreEqual(
                string.Format("PlayerId is invalid. Length must be between 1 and {0} characters.",
                    Validation.MaxStringLengthLong), playerIdTooLongOutcome.Error.ErrorMessage);
        }

        [Test]
        public void GIVEN_validParams_WHEN_ValidateStartMatchBackfillRequest_THEN_success()
        {
            // Given
            var input = new StartMatchBackfillRequest(
                TestGameSessionArn,
                TestMatchmakingConfigurationArn,
                new[] { new Player() }
            );
            input.TicketId = "test-ticket-id";

            // When
            var outcome = Validation.ValidateStartMatchBackfillRequest(input);

            // Then
            Assert.IsTrue(outcome.Success);
        }

        [Test]
        public void GIVEN_invalidParams_WHEN_ValidateStartMatchBackfillRequest_THEN_failure()
        {
            // Given
            var input = new StartMatchBackfillRequest(
                TestGameSessionArn,
                TestMatchmakingConfigurationArn,
                new[] { new Player() }
            );
            input.TicketId = "test-ticket-id";

            // When - invalid GameSessionArn
            // Empty
            input.GameSessionArn = "";
            var gameSessionArnEmptyOutcome = Validation.ValidateStartMatchBackfillRequest(input);
            // Too long
            input.GameSessionArn = new string('a', Validation.MaxStringLengthArn + 1);
            var gameSessionArnTooLongOutcome = Validation.ValidateStartMatchBackfillRequest(input);
            // Invalid characters
            input.GameSessionArn = "test-game-session-arn!";
            var gameSessionArnInvalidOutcome = Validation.ValidateStartMatchBackfillRequest(input);
            input.GameSessionArn = TestGameSessionArn;

            // When - invalid MatchmakingConfigurationArn
            // Empty
            input.MatchmakingConfigurationArn = "";
            var matchmakingConfigurationArnEmptyOutcome = Validation.ValidateStartMatchBackfillRequest(input);
            // Too long
            input.MatchmakingConfigurationArn = new string('a', Validation.MaxStringLengthArn + 1);
            var matchmakingConfigurationArnTooLongOutcome = Validation.ValidateStartMatchBackfillRequest(input);
            // Invalid characters
            input.MatchmakingConfigurationArn = "test-matchmaking-configuration-arn!";
            var matchmakingConfigurationArnInvalidOutcome = Validation.ValidateStartMatchBackfillRequest(input);
            input.MatchmakingConfigurationArn = TestMatchmakingConfigurationArn;

            // When - invalid TicketId
            // Too long
            input.TicketId = new string('a', Validation.MaxStringLengthMatchmakingId + 1);
            var ticketIdTooLongOutcome = Validation.ValidateStartMatchBackfillRequest(input);
            // Invalid characters
            input.TicketId = "test-ticket-id!";
            var ticketIdInvalidOutcome = Validation.ValidateStartMatchBackfillRequest(input);
            input.TicketId = "test-ticket-id";

            // When - empty players
            input.Players = null;
            var emptyPlayersOutcome = Validation.ValidateStartMatchBackfillRequest(input);

            // Then
            Assert.IsFalse(gameSessionArnEmptyOutcome.Success);
            Assert.AreEqual("GameSessionArn is required.", gameSessionArnEmptyOutcome.Error.ErrorMessage);
            Assert.IsFalse(gameSessionArnTooLongOutcome.Success);
            Assert.AreEqual(
                string.Format("GameSessionArn is invalid. Length must be between 1 and {0} characters.",
                    Validation.MaxStringLengthArn), gameSessionArnTooLongOutcome.Error.ErrorMessage);
            Assert.IsFalse(gameSessionArnInvalidOutcome.Success);
            Assert.AreEqual(string.Format("GameSessionArn is invalid. {0}", Validation.InvalidArnError),
                gameSessionArnInvalidOutcome.Error.ErrorMessage);
            Assert.IsFalse(matchmakingConfigurationArnEmptyOutcome.Success);
            Assert.AreEqual("MatchmakingConfigurationArn is required.",
                matchmakingConfigurationArnEmptyOutcome.Error.ErrorMessage);
            Assert.IsFalse(matchmakingConfigurationArnTooLongOutcome.Success);
            Assert.AreEqual(
                string.Format("MatchmakingConfigurationArn is invalid. Length must be between 1 and {0} characters.",
                    Validation.MaxStringLengthArn), matchmakingConfigurationArnTooLongOutcome.Error.ErrorMessage);
            Assert.IsFalse(matchmakingConfigurationArnInvalidOutcome.Success);
            Assert.AreEqual(
                string.Format("MatchmakingConfigurationArn is invalid. {0}", Validation.InvalidGameLiftArnError),
                matchmakingConfigurationArnInvalidOutcome.Error.ErrorMessage);
            Assert.IsFalse(ticketIdTooLongOutcome.Success);
            Assert.AreEqual(
                string.Format("TicketId is invalid. Length must be between 1 and {0} characters.",
                    Validation.MaxStringLengthMatchmakingId), ticketIdTooLongOutcome.Error.ErrorMessage);
            Assert.IsFalse(ticketIdInvalidOutcome.Success);
            Assert.AreEqual(
                string.Format("TicketId is invalid. Must match the pattern: {0}.", Validation.MatchmakingIdRegex),
                ticketIdInvalidOutcome.Error.ErrorMessage);
            Assert.IsFalse(emptyPlayersOutcome.Success);
            Assert.AreEqual(Validation.EmptyPlayersError, emptyPlayersOutcome.Error.ErrorMessage);
        }

        [Test]
        public void GIVEN_validParams_WHEN_ValidateStopMatchBackfillRequest_THEN_success()
        {
            // Given
            var input = new StopMatchBackfillRequest(
                TestGameSessionArn,
                TestMatchmakingConfigurationArn,
                "test-ticket-id"
            );

            // When
            var outcome = Validation.ValidateStopMatchBackfillRequest(input);

            // Then
            Assert.IsTrue(outcome.Success);
        }

        [Test]
        public void GIVEN_invalidParams_WHEN_ValidateStopMatchBackfillRequest_THEN_failure()
        {
            // Given
            var input = new StopMatchBackfillRequest(
                TestGameSessionArn,
                TestMatchmakingConfigurationArn,
                "test-ticket-id"
            );

            // When - invalid GameSessionArn
            // Empty
            input.GameSessionArn = "";
            var gameSessionArnEmptyOutcome = Validation.ValidateStopMatchBackfillRequest(input);
            // Too long
            input.GameSessionArn = new string('a', Validation.MaxStringLengthArn + 1);
            var gameSessionArnTooLongOutcome = Validation.ValidateStopMatchBackfillRequest(input);
            // Invalid characters
            input.GameSessionArn = "test-game-session-arn!";
            var gameSessionArnInvalidOutcome = Validation.ValidateStopMatchBackfillRequest(input);
            input.GameSessionArn = TestGameSessionArn;

            // When - invalid MatchmakingConfigurationArn
            // Empty
            input.MatchmakingConfigurationArn = "";
            var matchmakingConfigurationArnEmptyOutcome = Validation.ValidateStopMatchBackfillRequest(input);
            // Invalid characters
            input.MatchmakingConfigurationArn = "test-matchmaking-configuration-arn!";
            var matchmakingConfigurationArnInvalidOutcome = Validation.ValidateStopMatchBackfillRequest(input);
            input.MatchmakingConfigurationArn = TestMatchmakingConfigurationArn;

            // When - invalid TicketId
            // Empty
            input.TicketId = "";
            var ticketIdEmptyOutcome = Validation.ValidateStopMatchBackfillRequest(input);
            // Too long
            input.TicketId = new string('a', Validation.MaxStringLengthMatchmakingId + 1);
            var ticketIdTooLongOutcome = Validation.ValidateStopMatchBackfillRequest(input);
            // Invalid characters
            input.TicketId = "test-ticket-id!";
            var ticketIdInvalidOutcome = Validation.ValidateStopMatchBackfillRequest(input);
            input.TicketId = "test-ticket-id";

            // Then
            Assert.IsFalse(gameSessionArnEmptyOutcome.Success);
            Assert.AreEqual("GameSessionArn is required.", gameSessionArnEmptyOutcome.Error.ErrorMessage);
            Assert.IsFalse(gameSessionArnTooLongOutcome.Success);
            Assert.AreEqual(
                string.Format("GameSessionArn is invalid. Length must be between 1 and {0} characters.",
                    Validation.MaxStringLengthArn), gameSessionArnTooLongOutcome.Error.ErrorMessage);
            Assert.IsFalse(gameSessionArnInvalidOutcome.Success);
            Assert.AreEqual(string.Format("GameSessionArn is invalid. {0}", Validation.InvalidGameLiftArnError),
                gameSessionArnInvalidOutcome.Error.ErrorMessage);
            Assert.IsFalse(matchmakingConfigurationArnEmptyOutcome.Success);
            Assert.AreEqual("MatchmakingConfigurationArn is required.",
                matchmakingConfigurationArnEmptyOutcome.Error.ErrorMessage);
            Assert.IsFalse(matchmakingConfigurationArnInvalidOutcome.Success);
            Assert.AreEqual(
                string.Format("MatchmakingConfigurationArn is invalid. {0}", Validation.InvalidGameLiftArnError),
                matchmakingConfigurationArnInvalidOutcome.Error.ErrorMessage);
            Assert.IsFalse(ticketIdEmptyOutcome.Success);
            Assert.AreEqual("TicketId is required.", ticketIdEmptyOutcome.Error.ErrorMessage);
            Assert.IsFalse(ticketIdTooLongOutcome.Success);
            Assert.AreEqual(
                string.Format("TicketId is invalid. Length must be between 1 and {0} characters.",
                    Validation.MaxStringLengthMatchmakingId), ticketIdTooLongOutcome.Error.ErrorMessage);
            Assert.IsFalse(ticketIdInvalidOutcome.Success);
            Assert.AreEqual(
                string.Format("TicketId is invalid. Must match the pattern: {0}.", Validation.MatchmakingIdRegex),
                ticketIdInvalidOutcome.Error.ErrorMessage);
        }

        [Test]
        public void GIVEN_validParams_WHEN_ValidateGetFleetRoleCredentialsRequest_THEN_success()
        {
            // Given
            var input = new GetFleetRoleCredentialsRequest(
                "test-role-arn"
            );
            input.RoleSessionName = "test-role-session-name";

            // When
            var outcome = Validation.ValidateGetFleetRoleCredentialsRequest(input);

            // Then
            Assert.IsTrue(outcome.Success);
        }

        [Test]
        public void GIVEN_invalidParams_WHEN_ValidateGetFleetRoleCredentialsRequest_THEN_failure()
        {
            // Given
            var input = new GetFleetRoleCredentialsRequest(
                "test-role-arn"
            );
            input.RoleSessionName = "test-role-session-name";

            // When - invalid RoleArn
            // Empty
            input.RoleArn = "";
            var roleArnEmptyOutcome = Validation.ValidateGetFleetRoleCredentialsRequest(input);
            // Too long
            input.RoleArn = new string('a', Validation.MaxStringLengthArn + 1);
            var roleArnTooLongOutcome = Validation.ValidateGetFleetRoleCredentialsRequest(input);
            // Invalid characters
            input.RoleArn = "test-role-arn!";
            var roleArnInvalidOutcome = Validation.ValidateGetFleetRoleCredentialsRequest(input);
            input.RoleArn = "test-role-arn";

            // When - invalid RoleSessionName
            // Too long
            input.RoleSessionName = new string('a', Validation.MaxLengthRoleSessionName + 1);
            var roleSessionNameTooLongOutcome = Validation.ValidateGetFleetRoleCredentialsRequest(input);
            // Too short
            input.RoleSessionName = new string('a', Validation.MinLengthRoleSessionName - 1);
            var roleSessionNameTooShortOutcome = Validation.ValidateGetFleetRoleCredentialsRequest(input);
            // Invalid characters
            input.RoleSessionName = "test-role-session-name!";
            var roleSessionNameInvalidOutcome = Validation.ValidateGetFleetRoleCredentialsRequest(input);

            // Then
            Assert.IsFalse(roleArnEmptyOutcome.Success);
            Assert.AreEqual("RoleArn is required.", roleArnEmptyOutcome.Error.ErrorMessage);
            Assert.IsFalse(roleArnTooLongOutcome.Success);
            Assert.AreEqual(
                string.Format("RoleArn is invalid. Length must be between 1 and {0} characters.",
                    Validation.MaxStringLengthArn), roleArnTooLongOutcome.Error.ErrorMessage);
            Assert.IsFalse(roleArnInvalidOutcome.Success);
            Assert.AreEqual(string.Format("RoleArn is invalid. {0}", Validation.InvalidArnError),
                roleArnInvalidOutcome.Error.ErrorMessage);
            Assert.IsFalse(roleSessionNameTooLongOutcome.Success);
            Assert.AreEqual(
                string.Format("RoleSessionName is invalid. Length must be between {0} and {1} characters.",
                    Validation.MinLengthRoleSessionName, Validation.MaxLengthRoleSessionName),
                roleSessionNameTooLongOutcome.Error.ErrorMessage);
            Assert.IsFalse(roleSessionNameTooShortOutcome.Success);
            Assert.AreEqual(
                string.Format("RoleSessionName is invalid. Length must be between {0} and {1} characters.",
                    Validation.MinLengthRoleSessionName, Validation.MaxLengthRoleSessionName),
                roleSessionNameTooShortOutcome.Error.ErrorMessage);
            Assert.IsFalse(roleSessionNameInvalidOutcome.Success);
            Assert.AreEqual(
                string.Format("RoleSessionName is invalid. Must match the pattern: {0}.",
                    Validation.RoleSessionNameRegex),
                roleSessionNameInvalidOutcome.Error.ErrorMessage);
        }

        [Test]
        public void GIVEN_validMetricsParameters_WHEN_validating_THEN_returnsSuccess()
        {
            // Given
            var parameters = new MetricsParameters("localhost", 8125, "crash-host", 8126, 5000, 1024);

            // When
            var outcome = Validation.ValidateMetricsParameters(parameters);

            // Then
            Assert.IsTrue(outcome.Success);
            Assert.IsNull(outcome.Error);
        }

        [Test]
        public void GIVEN_invalidMetricsParameters_WHEN_validating_THEN_returnsError()
        {
            // Given - invalid StatsdPort (too high)
            var invalidStatsdPort = new MetricsParameters("localhost", 70000, "crash-host", 8126, 5000, 1024);

            // Given - negative StatsdPort
            var negativeStatsdPort = new MetricsParameters("localhost", -1, "crash-host", 8126, 5000, 1024);

            // Given - invalid CrashReporterPort (too high)
            var invalidCrashPort = new MetricsParameters("localhost", 8125, "crash-host", 99999, 5000, 1024);

            // Given - negative FlushInterval
            var negativeFlushInterval = new MetricsParameters("localhost", 8125, "crash-host", 8126, -100, 1024);

            // Given - negative MaxPacketSize
            var negativeMaxPacketSize = new MetricsParameters("localhost", 8125, "crash-host", 8126, 5000, -512);

            // Given - empty StatsdHost
            var emptyStatsdHost = new MetricsParameters("", 8125, "crash-host", 8126, 5000, 1024);

            // Given - empty CrashReporterHost
            var emptyCrashHost = new MetricsParameters("localhost", 8125, "", 8126, 5000, 1024);

            // Given - null StatsdHost
            var nullStatsdHost = new MetricsParameters(null, 8125, "crash-host", 8126, 5000, 1024);

            // Given - null CrashReporterHost
            var nullCrashHost = new MetricsParameters("localhost", 8125, null, 8126, 5000, 1024);

            // When
            var outcomes = new[]
            {
                Validation.ValidateMetricsParameters(invalidStatsdPort),
                Validation.ValidateMetricsParameters(negativeStatsdPort),
                Validation.ValidateMetricsParameters(invalidCrashPort),
                Validation.ValidateMetricsParameters(negativeFlushInterval),
                Validation.ValidateMetricsParameters(negativeMaxPacketSize),
                Validation.ValidateMetricsParameters(emptyStatsdHost),
                Validation.ValidateMetricsParameters(emptyCrashHost),
                Validation.ValidateMetricsParameters(nullStatsdHost),
                Validation.ValidateMetricsParameters(nullCrashHost),
            };

            // Then
            foreach (var outcome in outcomes)
            {
                Assert.IsFalse(outcome.Success);
                Assert.AreEqual(GameLiftErrorType.VALIDATION_EXCEPTION, outcome.Error.ErrorType);
            }
        }
    }
}