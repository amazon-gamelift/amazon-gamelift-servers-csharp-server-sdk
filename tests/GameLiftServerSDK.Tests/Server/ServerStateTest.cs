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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Aws.GameLift;
using Aws.GameLift.Server;
using Aws.GameLift.Server.Model;
using Moq;
using NUnit.Framework;
using WebSocketSharp;
using static Moq.It;
using Capture = Moq.Capture;

namespace Aws.GameLift.Tests.Server
{
    [TestFixture]
    public class ServerStateTest
    {
        private const string SdkVersion = "5.4.0";
        private const string SdkToolName = "testSdkToolName";
        private const string SdkToolVersion = "1.0.0";
        private const string EnvironmentVariableWebsocketUrl = "GAMELIFT_SDK_WEBSOCKET_URL";
        private const string EnvironmentVariableProcessId = "GAMELIFT_SDK_PROCESS_ID";
        private const string EnvironmentVariableHostId = "GAMELIFT_SDK_HOST_ID";
        private const string EnvironmentVariableFleetId = "GAMELIFT_SDK_FLEET_ID";
        private const string EnvironmentVariableAuthToken = "GAMELIFT_SDK_AUTH_TOKEN";
        private const string EnvironmentVariableAwsRegion = "GAMELIFT_REGION";
        private const string EnvironmentVariableAccessKey = "GAMELIFT_ACCESS_KEY";
        private const string EnvironmentVariableSecretKey = "GAMELIFT_SECRET_KEY";
        private const string EnvironmentVariableSessionToken = "GAMELIFT_SESSION_TOKEN";
        private const string WebsocketUrl = "wss://webSocketUrl";
        private const string ProcessId = "processId";
        private const string HostId = "hostId";
        private const string FleetId = "fleetId";
        private const string PlayerSessionId = "playerSessionId";
        private const string PlayerSessionStatusFilter = "ACTIVE";
        private const string PlayerId = "playerId";
        private const string GameSessionId = "gameSessionId";
        private const string AuthToken = "authToken";
        private const string AwsRegion = "awsRegion";
        private const string AccessKey = "accessKey";
        private const string SecretKey = "secretKey";
        private const string SessionToken = "sessionToken";
        private const string ComputeCert = "computeCert";
        private const int PortNumber = 1234;
        private const string NextToken = "nextToken";
        private const string TicketId = "SomeTicket";
        private const string GameSessionArn = "arn:aws:gamelift:us-west-2::gamesession/fleet-test/location-test/gsess-test";
        private const string MatchmakerArn = "arn:aws:gamelift:us-west-2:000000000000:matchmakingconfiguration/test";
        private const string SigV4QueryStringPattern = @"^Authorization=SigV4" +
                                                       @"&X-Amz-Algorithm=AWS4-HMAC-SHA256" +
                                                       @"&X-Amz-Credential=[A-Za-z0-9]+\%2F\d{8}\%2F[A-Za-z0-9]+\%2Fgamelift\%2Faws4_request" +
                                                       @"&X-Amz-Date=\d{8}T\d{6}Z" +
                                                       @"&X-Amz-Security-Token=[A-Za-z0-9]+" +
                                                       @"&X-Amz-Signature=[A-Fa-f0-9]+$";

        private static readonly List<string> LogPaths = new List<string> { "C:\\game\\logs", "C:\\game\\error" };

        private static readonly ProcessParameters GenericProcessParams = new ProcessParameters(
            (gameSession) => { },
            (updateGameSession) => { },
            () => { },
            () => { return false; },
            PortNumber,
            new LogParameters(LogPaths));

        private readonly IDictionary<string, string> oldEnvironmentVariables = new Dictionary<string, string>();

        private Mock<IGameLiftWebSocket> mockWebSocket;
        private Mock<GameLiftWebSocketRequestHandler> mockRequestHandler;
        private Mock<SystemEnvironmentWrapper> mockEnvironment;
        private ServerState state;

        [SetUp]
        public void SetUp()
        {
            mockWebSocket = new Mock<IGameLiftWebSocket>();
            mockRequestHandler = new Mock<GameLiftWebSocketRequestHandler>();
            mockEnvironment = new Mock<SystemEnvironmentWrapper>();
            state = new ServerState(mockWebSocket.Object, mockRequestHandler.Object, mockEnvironment.Object);
        }

        private void UsingEnvironmentVariables(IDictionary<string, string> environmentVariables, Action testCode)
        {
            oldEnvironmentVariables.Clear();
            foreach (var environmentVariable in environmentVariables)
            {
                oldEnvironmentVariables[environmentVariable.Key] = Environment.GetEnvironmentVariable(environmentVariable.Key);
                Environment.SetEnvironmentVariable(environmentVariable.Key, environmentVariable.Value);
            }

            try
            {
                testCode.Invoke();
            }
            finally
            {
                foreach (var environmentVariable in oldEnvironmentVariables)
                {
                    Environment.SetEnvironmentVariable(environmentVariable.Key, environmentVariable.Value);
                }
            }
        }

        [Test]
        public void WhenInstanceThenInstanceIsReturned()
        {
            Assert.IsNotNull(ServerState.Instance);
        }

        [Test]
        public void SameInstanceIsAlwaysReturned()
        {
            ServerState firstInstance = ServerState.Instance;
            ServerState secondInstance = ServerState.Instance;
            Assert.AreSame(firstInstance, secondInstance);
        }

        [Test]
        public void GIVEN_serverParametersWithAuthToken_WHEN_initializeNetworking_THEN_returnsSuccessOutcome()
        {
            // GIVEN
            mockWebSocket.Setup(websocket => websocket.Connect(
                IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>())).Returns(new GenericOutcome());

            // WHEN
            GenericOutcome outcome = state.InitializeNetworking(new ServerParameters(WebsocketUrl, ProcessId, HostId, FleetId, AuthToken));

            // THEN
            mockWebSocket.Verify(websocket => websocket.Connect(WebsocketUrl, ProcessId, HostId, FleetId, AuthToken, null));
            AssertSuccessOutcome(outcome);
        }

        [Test]
        public void GIVEN_serverParametersWithAwsRegionAndCredentials_WHEN_initializeNetworking_THEN_returnsSuccessOutcome()
        {
            // GIVEN
            mockWebSocket.Setup(websocket => websocket.Connect(
                IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>())).Returns(new GenericOutcome());

            // WHEN
            GenericOutcome outcome = state.InitializeNetworking(new ServerParameters(WebsocketUrl, ProcessId, HostId, FleetId, AwsRegion, AccessKey, SecretKey, SessionToken));

            // THEN
            mockWebSocket.Verify(websocket => websocket.Connect(WebsocketUrl, ProcessId, HostId, FleetId, null, Is<string>(sigV4QueryString => Regex.IsMatch(sigV4QueryString, SigV4QueryStringPattern))));
            AssertSuccessOutcome(outcome);
        }

        [Test]
        public void GIVEN_nullWebsocketUrl_WHEN_initializeNetworking_THEN_returnsErrorOutcome()
        {
            // GIVEN
            mockWebSocket.Setup(websocket => websocket.Connect(
                IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>())).Returns(new GenericOutcome());

            // WHEN
            GenericOutcome outcome = state.InitializeNetworking(new ServerParameters(null, ProcessId, HostId, FleetId, AuthToken));

            // THEN
            AssertInitializeNetworkingFailure(outcome);
        }

        [Test]
        public void GIVEN_nullProcessId_WHEN_initializeNetworking_THEN_returnsErrorOutcome()
        {
            // GIVEN
            mockWebSocket.Setup(websocket => websocket.Connect(
                IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>())).Returns(new GenericOutcome());

            // WHEN
            GenericOutcome outcome = state.InitializeNetworking(new ServerParameters(WebsocketUrl, null, HostId, FleetId, AuthToken));

            // THEN
            AssertInitializeNetworkingFailure(outcome);
        }

        [Test]
        public void GIVEN_nullHostId_WHEN_initializeNetworking_THEN_returnsErrorOutcome()
        {
            // GIVEN
            mockWebSocket.Setup(websocket => websocket.Connect(
                IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>())).Returns(new GenericOutcome());

            // WHEN
            GenericOutcome outcome = state.InitializeNetworking(new ServerParameters(WebsocketUrl, ProcessId, null, FleetId, AuthToken));

            // THEN
            AssertInitializeNetworkingFailure(outcome);
        }

        [Test]
        public void GIVEN_nullFleetId_WHEN_initializeNetworking_THEN_returnsErrorOutcome()
        {
            // GIVEN
            mockWebSocket.Setup(websocket => websocket.Connect(
                IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>())).Returns(new GenericOutcome());

            // WHEN
            GenericOutcome outcome = state.InitializeNetworking(new ServerParameters(WebsocketUrl, ProcessId, HostId, null, AuthToken));

            // THEN
            AssertInitializeNetworkingFailure(outcome);
        }

        [Test]
        public void GIVEN_nullAuthToken_WHEN_initializeNetworking_THEN_returnsErrorOutcome()
        {
            // GIVEN
            mockWebSocket.Setup(websocket => websocket.Connect(
                IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>())).Returns(new GenericOutcome());

            // WHEN
            GenericOutcome outcome = state.InitializeNetworking(new ServerParameters(WebsocketUrl, ProcessId, HostId, FleetId, null));

            // THEN
            AssertInitializeNetworkingFailure(outcome);
        }

        [Test]
        public void GIVEN_nullAwsRegionAndCredentials_WHEN_initializeNetworking_THEN_returnsErrorOutcome()
        {
            // GIVEN
            mockWebSocket.Setup(websocket => websocket.Connect(
                IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>())).Returns(new GenericOutcome());

            // WHEN
            GenericOutcome outcome = state.InitializeNetworking(new ServerParameters(WebsocketUrl, ProcessId, HostId, FleetId, null, null, null, null));

            // THEN
            AssertInitializeNetworkingFailure(outcome);
        }

        private void AssertInitializeNetworkingFailure(GenericOutcome outcome)
        {
            mockWebSocket.Verify(websocket => websocket.Connect(
                IsAny<String>(), IsAny<String>(), IsAny<String>(), IsAny<String>(), IsAny<String>(), IsAny<string>()), Times.Never);
            Assert.IsFalse(outcome.Success);
            Assert.AreEqual(outcome.GetType(), typeof(GenericOutcome));
            Assert.AreEqual(GameLiftErrorType.VALIDATION_EXCEPTION, outcome.Error.ErrorType);
        }

        [Test]
        public void GIVEN_defaultServerParametersWithAuthToken_WHEN_initializeNetworking_THEN_returnsSuccessOutcome()
        {
            const string websocketUrlOverride = "wss://newWebsocketUrl";
            const string processIdOverride = "newProcessId";
            const string hostIdOverride = "newHostId";
            const string fleetIdOverride = "newFleetId";
            const string authTokenOverride = "newAuthToken";

            UsingEnvironmentVariables(
                new Dictionary<string, string>
                {
                    [EnvironmentVariableWebsocketUrl] = websocketUrlOverride,
                    [EnvironmentVariableProcessId] = processIdOverride,
                    [EnvironmentVariableHostId] = hostIdOverride,
                    [EnvironmentVariableFleetId] = fleetIdOverride,
                    [EnvironmentVariableAuthToken] = authTokenOverride,
                },
                () =>
                {
                    // GIVEN
                    mockWebSocket.Setup(websocket => websocket.Connect(
                        IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>())).Returns(new GenericOutcome());

                    // WHEN
                    GenericOutcome outcome = state.InitializeNetworking(default);

                    // THEN
                    mockWebSocket.Verify(websocket => websocket.Connect(
                        websocketUrlOverride,
                        processIdOverride,
                        hostIdOverride,
                        fleetIdOverride,
                        authTokenOverride,
                        null));
                    AssertSuccessOutcome(outcome);
                });
        }

        [Test]
        public void GIVEN_defaultServerParametersWithAwsRegionAndCredentials_WHEN_initializeNetworking_THEN_returnsSuccessOutcome()
        {
            const string websocketUrlOverride = "wss://newWebsocketUrl";
            const string processIdOverride = "newProcessId";
            const string hostIdOverride = "newHostId";
            const string fleetIdOverride = "newFleetId";
            const string awsRegionOverride = "newAwsRegion";
            const string accessKeyOverride = "newAccessKey";
            const string secretKeyOverride = "newSecretKey";
            const string sessionTokenOverride = "newSessionToken";

            UsingEnvironmentVariables(
                new Dictionary<string, string>
                {
                    [EnvironmentVariableWebsocketUrl] = websocketUrlOverride,
                    [EnvironmentVariableProcessId] = processIdOverride,
                    [EnvironmentVariableHostId] = hostIdOverride,
                    [EnvironmentVariableFleetId] = fleetIdOverride,
                    [EnvironmentVariableAwsRegion] = awsRegionOverride,
                    [EnvironmentVariableAccessKey] = accessKeyOverride,
                    [EnvironmentVariableSecretKey] = secretKeyOverride,
                    [EnvironmentVariableSessionToken] = sessionTokenOverride,
                },
                () =>
                {
                    // GIVEN
                    mockWebSocket.Setup(websocket => websocket.Connect(
                        IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>())).Returns(new GenericOutcome());

                    // WHEN
                    GenericOutcome outcome = state.InitializeNetworking(default);

                    // THEN
                    mockWebSocket.Verify(websocket => websocket.Connect(
                        websocketUrlOverride,
                        processIdOverride,
                        hostIdOverride,
                        fleetIdOverride,
                        null,
                        Is<string>(sigV4QueryString => Regex.IsMatch(sigV4QueryString, SigV4QueryStringPattern) &&
                                                       sigV4QueryString.Contains(awsRegionOverride) &&
                                                       sigV4QueryString.Contains(accessKeyOverride) &&
                                                       sigV4QueryString.Contains(sessionTokenOverride))));
                    AssertSuccessOutcome(outcome);
                });
        }

        [Test]
        public void GIVEN_defaultServerParametersWithManagedResourceProcessId_WHEN_initializeNetworking_THEN_returnsSuccessOutcome()
        {
            const string websocketUrlOverride = "wss://newWebsocketUrl";
            const string processIdOverride = "ManagedResource";
            const string hostIdOverride = "newHostId";
            const string fleetIdOverride = "newFleetId";
            const string awsRegionOverride = "newAwsRegion";
            const string accessKeyOverride = "newAccessKey";
            const string secretKeyOverride = "newSecretKey";
            const string sessionTokenOverride = "newSessionToken";

            UsingEnvironmentVariables(
                new Dictionary<string, string>
                {
                    [EnvironmentVariableWebsocketUrl] = websocketUrlOverride,
                    [EnvironmentVariableProcessId] = processIdOverride,
                    [EnvironmentVariableHostId] = hostIdOverride,
                    [EnvironmentVariableFleetId] = fleetIdOverride,
                    [EnvironmentVariableAwsRegion] = awsRegionOverride,
                    [EnvironmentVariableAccessKey] = accessKeyOverride,
                    [EnvironmentVariableSecretKey] = secretKeyOverride,
                    [EnvironmentVariableSessionToken] = sessionTokenOverride,
                },
                () =>
                {
                    // GIVEN
                    mockWebSocket.Setup(websocket => websocket.Connect(
                        IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>())).Returns(new GenericOutcome());

                    // WHEN
                    GenericOutcome outcome = state.InitializeNetworking(default);

                    // THEN
                    Guid dummyOutGuid;
                    mockWebSocket.Verify(websocket => websocket.Connect(
                        websocketUrlOverride,
                        Is<string>(processId => !processIdOverride.Equals(processId) && Guid.TryParse(processId, out dummyOutGuid)),
                        hostIdOverride,
                        fleetIdOverride,
                        null,
                        Is<string>(sigV4QueryString => Regex.IsMatch(sigV4QueryString, SigV4QueryStringPattern) &&
                                                       sigV4QueryString.Contains(awsRegionOverride) &&
                                                       sigV4QueryString.Contains(accessKeyOverride) &&
                                                       sigV4QueryString.Contains(sessionTokenOverride))));
                    AssertSuccessOutcome(outcome);
                });
        }

        [Test]
        public void GIVEN_serverParametersWithAuthToken_WHEN_initializeNetworkingWithEnvVars_THEN_returnsSuccessOutcome()
        {
            const string websocketUrlOverride = "wss://newWebsocketUrl";
            const string processIdOverride = "newProcessId";
            const string hostIdOverride = "newHostId";
            const string fleetIdOverride = "newFleetId";
            const string authTokenOverride = "newAuthToken";

            UsingEnvironmentVariables(
                new Dictionary<string, string>
                {
                    [EnvironmentVariableWebsocketUrl] = websocketUrlOverride,
                    [EnvironmentVariableProcessId] = processIdOverride,
                    [EnvironmentVariableHostId] = hostIdOverride,
                    [EnvironmentVariableFleetId] = fleetIdOverride,
                    [EnvironmentVariableAuthToken] = authTokenOverride,
                },
                () =>
                {
                    // GIVEN
                    mockWebSocket.Setup(websocket => websocket.Connect(
                        IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>())).Returns(new GenericOutcome());

                    // WHEN
                    GenericOutcome outcome = state.InitializeNetworking(new ServerParameters(WebsocketUrl, ProcessId, HostId, FleetId, AuthToken));

                    // THEN
                    mockWebSocket.Verify(websocket => websocket.Connect(
                        websocketUrlOverride,
                        processIdOverride,
                        hostIdOverride,
                        fleetIdOverride,
                        authTokenOverride,
                        null));
                    AssertSuccessOutcome(outcome);
                });
        }

        [Test]
        public void GIVEN_serverParametersWithAwsRegionAndCredentials_WHEN_initializeNetworkingWithEnvVars_THEN_returnsSuccessOutcome()
        {
            const string websocketUrlOverride = "wss://newWebsocketUrl";
            const string processIdOverride = "newProcessId";
            const string hostIdOverride = "newHostId";
            const string fleetIdOverride = "newFleetId";
            const string awsRegionOverride = "newAwsRegion";
            const string accessKeyOverride = "newAccessKey";
            const string secretKeyOverride = "newSecretKey";
            const string sessionTokenOverride = "newSessionToken";

            UsingEnvironmentVariables(
                new Dictionary<string, string>
                {
                    [EnvironmentVariableWebsocketUrl] = websocketUrlOverride,
                    [EnvironmentVariableProcessId] = processIdOverride,
                    [EnvironmentVariableHostId] = hostIdOverride,
                    [EnvironmentVariableFleetId] = fleetIdOverride,
                    [EnvironmentVariableAwsRegion] = awsRegionOverride,
                    [EnvironmentVariableAccessKey] = accessKeyOverride,
                    [EnvironmentVariableSecretKey] = secretKeyOverride,
                    [EnvironmentVariableSessionToken] = sessionTokenOverride,
                },
                () =>
                {
                    // GIVEN
                    mockWebSocket.Setup(websocket => websocket.Connect(
                        IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>())).Returns(new GenericOutcome());

                    // WHEN
                    GenericOutcome outcome = state.InitializeNetworking(new ServerParameters(WebsocketUrl, ProcessId, HostId, FleetId, AwsRegion, AccessKey, SecretKey, SessionToken));

                    // THEN
                    mockWebSocket.Verify(websocket => websocket.Connect(
                        websocketUrlOverride,
                        processIdOverride,
                        hostIdOverride,
                        fleetIdOverride,
                        null,
                        Is<string>(sigV4QueryString => Regex.IsMatch(sigV4QueryString, SigV4QueryStringPattern) &&
                                                       sigV4QueryString.Contains(awsRegionOverride) &&
                                                       sigV4QueryString.Contains(accessKeyOverride) &&
                                                       sigV4QueryString.Contains(sessionTokenOverride))));
                    AssertSuccessOutcome(outcome);
                });
        }

        [Test]
        public void GIVEN_refreshParameters_WHEN_onRefreshConnection_THEN_callsRefreshCallback()
        {
            // GIVEN
            const string newAuthToken = "newAuthToken";
            const string newWebSocketUrl = "newWebsocketUrl";
            mockWebSocket.Setup(websocket => websocket.Connect(
                IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>())).Returns(new GenericOutcome());
            state.InitializeNetworking(new ServerParameters(WebsocketUrl, ProcessId, HostId, FleetId, AuthToken));
            state.ProcessReady(GenericProcessParams);

            // WHEN
            state.OnRefreshConnection(newWebSocketUrl, newAuthToken);

            // THEN
            mockWebSocket.Verify(websocket => websocket.Connect(WebsocketUrl, ProcessId, HostId, FleetId, AuthToken, null));
            mockWebSocket.Verify(websocket => websocket.Connect(newWebSocketUrl, ProcessId, HostId, FleetId, newAuthToken, null));
        }

        [Test]
        public void GIVEN_activateServerProcessSuccessResponse_WHEN_processReady_THEN_successOutcome()
        {
            // GIVEN - Test for the happy path
            mockWebSocket.Setup(websocket => websocket.Connect(
                IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>())).Returns(new GenericOutcome());
            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(IsAny<ActivateServerProcessRequest>()))
                .Returns(new GenericOutcome());
            state.InitializeNetworking(new ServerParameters(WebsocketUrl, ProcessId, HostId, FleetId, AuthToken));

            // WHEN
            GenericOutcome outcome = callProcessReady(GenericProcessParams);

            // THEN
            AssertSuccessOutcome(outcome);
        }

        [Test]
        public void GIVEN_activateServerProcessNoResponse_WHEN_processReady_THEN_failedOutcome()
        {
            // GIVEN - Test for when ActivateServerProcess API times out
            mockWebSocket.Setup(websocket => websocket.Connect(
                IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>())).Returns(new GenericOutcome());
            state.InitializeNetworking(new ServerParameters(WebsocketUrl, ProcessId, HostId, FleetId, AuthToken));

            // WHEN
            GenericOutcome outcome = state.ProcessReady(GenericProcessParams);

            // THEN
            Assert.IsFalse(outcome.Success);
            Assert.IsNotNull(outcome.Error);
        }

        [Test]
        public void GIVEN_activateServerProcessErrorResponse_WHEN_processReady_THEN_failedOutcome()
        {
            // GIVEN - Test for when ActivateServerProcess API times out
            mockWebSocket.Setup(websocket => websocket.Connect(
                IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>(), IsAny<string>())).Returns(new GenericOutcome());
            state.InitializeNetworking(new ServerParameters(WebsocketUrl, ProcessId, HostId, FleetId, AuthToken));

            // WHEN
            Task<GenericOutcome> task = Task.Run(() => state.ProcessReady(GenericProcessParams));
            state.OnActivateServerProcessFailure("requestId", new GenericOutcome(new GameLiftError()));
            GenericOutcome outcome = task.Result;

            // THEN
            Assert.IsFalse(outcome.Success);
            Assert.IsNotNull(outcome.Error);
        }

        [Test]
        public void GIVEN_activateServerProcessSuccessResponse_WHEN_OnActivateServerProcess_THEN_markProcessReadyAndBeginHealthCheck()
        {
            // GIVEN
            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(IsAny<Message>()))
                .Returns(new GenericOutcome(new GameLiftError()));
            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(IsAny<HeartbeatServerProcessRequest>()))
                .Returns(new GenericOutcome());
            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(IsAny<ActivateServerProcessRequest>()))
                .Returns(new GenericOutcome());
            ProcessParameters processParams = new ProcessParameters(
                (gameSession) => { },
                (updateGameSession) => { },
                () => { },
                () => true,
                1234,
                new LogParameters(new List<string> { "C:\\game\\logs", "C:\\game\\error" }));

            // WHEN
            GenericOutcome outcome = callProcessReady(processParams);
            FieldInfo processReadyField = typeof(ServerState).GetField("processReady", BindingFlags.Instance | BindingFlags.NonPublic);
            bool processReadyFieldValue = (bool)processReadyField.GetValue(state);

            // ProcessReady sends heartbeat messages asynchronously, in a separate thread. Need to delay before verifying mocks.
            Thread.Sleep(5000);

            // THEN
            Assert.IsTrue(processReadyFieldValue);
            mockRequestHandler.Verify(
                requestHandler => requestHandler.SendRequest(
                    Is<HeartbeatServerProcessRequest>(req => req.HealthStatus && req.Action == MessageActions.HeartbeatServerProcess)), Times.Once);
            AssertSuccessOutcome(outcome);
        }

        [Test]
        public void GIVEN_activateServerProcessErrorResponse_WHEN_OnActivateServerProcess_THEN_markProcessReadyAndNoHealthCheck()
        {
            // GIVEN
            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(IsAny<Message>()))
                .Returns(new GenericOutcome(new GameLiftError()));
            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(IsAny<HeartbeatServerProcessRequest>()))
                .Returns(new GenericOutcome());
            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(IsAny<ActivateServerProcessRequest>()))
                .Returns(new GenericOutcome());
            ProcessParameters processParams = new ProcessParameters(
                (gameSession) => { },
                (updateGameSession) => { },
                () => { },
                () => true,
                1234,
                new LogParameters(new List<string> { "C:\\game\\logs", "C:\\game\\error" }));

            // WHEN
            Task<GenericOutcome> task = Task.Run(() => state.ProcessReady(processParams));
            state.OnActivateServerProcessFailure("requestId", new GenericOutcome(new GameLiftError()));
            GenericOutcome outcome = task.Result;
            FieldInfo processReadyField = typeof(ServerState).GetField("processReady", BindingFlags.Instance | BindingFlags.NonPublic);
            bool processReadyFieldValue = (bool)processReadyField.GetValue(state);

            // ProcessReady sends heartbeat messages asynchronously, in a separate thread. Need to delay before verifying mocks.
            Thread.Sleep(5000);

            // THEN
            Assert.IsFalse(processReadyFieldValue);
            mockRequestHandler.Verify(
                requestHandler => requestHandler.SendRequest(
                    Is<HeartbeatServerProcessRequest>(req => req.HealthStatus && req.Action == MessageActions.HeartbeatServerProcess)), Times.Never);
            Assert.IsFalse(outcome.Success);
            Assert.IsNotNull(outcome.Error);
        }
        
        [Test]
        public void GIVEN_noHandlerForOnProcessTerminate_WHEN_OnTerminateProcess_THEN_ExitSuccessfully()
        {
            // GIVEN
            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(IsAny<Message>()))
                .Returns(new GenericOutcome());
            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(IsAny<HeartbeatServerProcessRequest>()))
                .Returns(new GenericOutcome());
            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(IsAny<ActivateServerProcessRequest>()))
                .Returns(new GenericOutcome());
            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(IsAny<TerminateServerProcessRequest>()))
                .Returns(new GenericOutcome());
            mockWebSocket.Setup(websocket => websocket.Disconnect()).Returns(new GenericOutcome()); // avoid actually disconnecting
            mockEnvironment.Setup(env => env.ExitSystem(IsAny<int>()));
            mockEnvironment.Setup(env => env.ExitUnity(IsAny<int>()));
            ProcessParameters processParamsNoTerminate = new ProcessParameters(
                (gameSession) => { },
                (updateGameSession) => { },
                null,
                () => { return false; },
                PortNumber,
                new LogParameters(LogPaths));
            
            // WHEN
            callProcessReady(processParamsNoTerminate);
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            state.OnTerminateProcess(now);
            
            // THEN
#if UNITY_SERVER || UNITY_STANDALONE || UNITY_EDITOR
            mockEnvironment.Verify(env => env.ExitUnity(0), Times.Once);
            mockEnvironment.Verify(env => env.ExitSystem(IsAny<int>()), Times.Never);
#else
            mockEnvironment.Verify(env => env.ExitSystem(0), Times.Once);
            mockEnvironment.Verify(env => env.ExitUnity(IsAny<int>()), Times.Never);
#endif
        }

        [Test]
        public void GIVEN_noHandlerForOnProcessTerminate_WHEN_OnTerminateProcessFails_THEN_ExitWithFailure()
        {
            // GIVEN
            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(IsAny<Message>()))
                .Returns(new GenericOutcome());
            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(IsAny<HeartbeatServerProcessRequest>()))
                .Returns(new GenericOutcome());
            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(IsAny<ActivateServerProcessRequest>()))
                .Returns(new GenericOutcome());
            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(IsAny<TerminateServerProcessRequest>()))
                .Returns(new GenericOutcome(new GameLiftError()));
            mockWebSocket.Setup(websocket => websocket.Disconnect()).Returns(new GenericOutcome()); // avoid actually disconnecting
            mockEnvironment.Setup(env => env.ExitSystem(IsAny<int>()));
            mockEnvironment.Setup(env => env.ExitUnity(IsAny<int>()));
            ProcessParameters processParamsNoTerminate = new ProcessParameters(
                (gameSession) => { },
                (updateGameSession) => { },
                null,
                () => { return false; },
                PortNumber,
                new LogParameters(LogPaths));
            
            // WHEN
            callProcessReady(processParamsNoTerminate);
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            state.OnTerminateProcess(now);
            
            // THEN
#if UNITY_SERVER || UNITY_STANDALONE || UNITY_EDITOR
            mockEnvironment.Verify(env => env.ExitUnity(-1), Times.Once);
            mockEnvironment.Verify(env => env.ExitSystem(IsAny<int>()), Times.Never);
#else
            mockEnvironment.Verify(env => env.ExitUnity(IsAny<int>()), Times.Never);
            mockEnvironment.Verify(env => env.ExitSystem(-1), Times.Once);
#endif
        }

        [Test]
        public void GIVEN_refreshParametersAndConnectionFails_WHEN_onRefreshConnection_THEN_doesNotRefreshConnection()
        {
            // GIVEN
            const string newAuthToken = "newAuthToken";
            const string newWebSocketUrl = "newWebsocketUrl";
            mockWebSocket.Setup(websocket => websocket.Connect(
                IsAny<string>(),
                IsAny<string>(),
                IsAny<string>(),
                IsAny<string>(),
                IsAny<string>(),
                IsAny<string>()))
                .Returns(new GenericOutcome());
            mockWebSocket.Setup(websocket => websocket.Connect(
                Is<string>(it => it == newWebSocketUrl),
                IsAny<string>(),
                IsAny<string>(),
                IsAny<string>(),
                Is<string>(it => it == newAuthToken),
                IsAny<string>()))
                .Returns(new GenericOutcome(new GameLiftError(GameLiftErrorType.BAD_REQUEST_EXCEPTION)));
            state.InitializeNetworking(new ServerParameters(WebsocketUrl, ProcessId, HostId, FleetId, AuthToken));
            state.ProcessReady(GenericProcessParams);

            // WHEN
            state.OnRefreshConnection(newWebSocketUrl, newAuthToken);

            // THEN
            mockWebSocket.Verify(websocket => websocket.Connect(WebsocketUrl, ProcessId, HostId, FleetId, AuthToken, null));
            mockWebSocket.Verify(websocket => websocket.Connect(newWebSocketUrl, ProcessId, HostId, FleetId, newAuthToken, null));
        }

        [Test]
        public void GIVEN_processParameters_WHEN_processReady_THEN_returnsSuccessOutcome()
        {
            // GIVEN
            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(IsAny<Message>()))
                .Returns(new GenericOutcome(new GameLiftError()));
            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(IsAny<ActivateServerProcessRequest>()))
                .Returns(new GenericOutcome());

            // WHEN
            GenericOutcome outcome = callProcessReady(GenericProcessParams);

            // THEN
            mockRequestHandler.Verify(
                requestHandler => requestHandler.SendRequest(Is<ActivateServerProcessRequest>(
                    req => req.SdkVersion == SdkVersion && req.SdkLanguage == "CSharp" && req.Port == PortNumber && req.LogPaths == LogPaths)), Times.Once);
            AssertSuccessOutcome(outcome);
        }

        [Test]
        public void GIVEN_serverStateInstance_WHEN_processEnding_THEN_outcomeSucceeds()
        {
            // GIVEN
            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(IsAny<Message>()))
                .Returns(new GenericOutcome(new GameLiftError()));
            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(IsAny<TerminateServerProcessRequest>()))
                .Returns(new GenericOutcome());

            // WHEN
            GenericOutcome outcome = state.ProcessEnding();

            // THEN
            mockRequestHandler.Verify(requestHandler => requestHandler.SendRequest(IsAny<TerminateServerProcessRequest>()), Times.Once);
            AssertSuccessOutcome(outcome);
        }

        [Test]
        public void GIVEN_gameSessionStarted_WHEN_activateGameSession_THEN_returnsSuccessOutcome()
        {
            // GIVEN
            StartGameSession();

            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(IsAny<ActivateGameSessionRequest>()))
                .Returns(new GenericOutcome());

            // WHEN
            GenericOutcome outcome = state.ActivateGameSession();

            // THEN
            mockRequestHandler.Verify(
                requestHandler => requestHandler.SendRequest(Is<ActivateGameSessionRequest>(
                    req => req.GameSessionId == GameSessionId)), Times.Once);
            AssertSuccessOutcome(outcome);
        }

        [Test]
        public void GIVEN_gameSessionStarted_WHEN_updatePlayerSessionCreationPolicy_THEN_returnsSuccessOutcome()
        {
            // GIVEN
            StartGameSession();
            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(IsAny<UpdatePlayerSessionCreationPolicyRequest>()))
                .Returns(new GenericOutcome());

            // WHEN
            GenericOutcome outcome = state.UpdatePlayerSessionCreationPolicy(PlayerSessionCreationPolicy.ACCEPT_ALL);

            // THEN
            mockRequestHandler.Verify(
                requestHandler => requestHandler.SendRequest(Is<UpdatePlayerSessionCreationPolicyRequest>(
                    req => req.PlayerSessionPolicy == PlayerSessionCreationPolicy.ACCEPT_ALL.ToString() && req.GameSessionId == GameSessionId)), Times.Once);
            AssertSuccessOutcome(outcome);
        }

        [Test]
        public void GIVEN_gameSessionStarted_WHEN_acceptPlayerSession_THEN_returnsSuccessOutcome()
        {
            // GIVEN
            StartGameSession();

            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(IsAny<AcceptPlayerSessionRequest>()))
                .Returns(new GenericOutcome());

            // WHEN
            GenericOutcome outcome = state.AcceptPlayerSession(PlayerSessionId);

            // THEN
            mockRequestHandler.Verify(
                requestHandler => requestHandler.SendRequest(Is<AcceptPlayerSessionRequest>(
                    req => req.GameSessionId == GameSessionId && req.PlayerSessionId == PlayerSessionId)),
                Times.Once);
            AssertSuccessOutcome(outcome);
        }

        [Test]
        public void GIVEN_gameSessionStarted_WHEN_removePlayerSession_THEN_returnsSuccessOutcome()
        {
            // GIVEN
            StartGameSession();

            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(IsAny<RemovePlayerSessionRequest>()))
                .Returns(new GenericOutcome());

            // WHEN
            GenericOutcome outcome = state.RemovePlayerSession(PlayerSessionId);

            // THEN
            mockRequestHandler.Verify(
                requestHandler => requestHandler.SendRequest(Is<RemovePlayerSessionRequest>(
                    req => req.GameSessionId == GameSessionId && req.PlayerSessionId == PlayerSessionId)),
                Times.Once);
            AssertSuccessOutcome(outcome);
        }

        [Test]
        public void GIVEN_gameSessionNotStarted_WHEN_activateGameSession_THEN_returnsFailedOutcome()
        {
            // GIVEN/WHEN
            GenericOutcome outcome = state.ActivateGameSession();

            // THEN
            mockRequestHandler.Verify(requestHandler => requestHandler.SendRequest(IsAny<Message>()), Times.Never);
            Assert.IsFalse(outcome.Success);
            Assert.AreEqual(outcome.GetType(), typeof(GenericOutcome));
            Assert.AreEqual(GameLiftErrorType.GAMESESSION_ID_NOT_SET, outcome.Error.ErrorType);
        }

        [Test]
        public void GIVEN_gameSessionNotStarted_WHEN_getGameSession_THEN_returnsFailedOutcome()
        {
            // GIVEN/WHEN
            GenericOutcome outcome = state.GetGameSessionId();

            // THEN
            mockRequestHandler.Verify(requestHandler => requestHandler.SendRequest(IsAny<Message>()), Times.Never);
            Assert.IsFalse(outcome.Success);
            Assert.AreEqual(outcome.GetType(), typeof(AwsStringOutcome));
            Assert.AreEqual(GameLiftErrorType.GAMESESSION_ID_NOT_SET, outcome.Error.ErrorType);
        }

        [Test]
        public void GIVEN_gameSessionNotStarted_WHEN_updatePlayerSessionCreationPolicy_THEN_returnsFailedOutcome()
        {
            // GIVEN/WHEN
            GenericOutcome outcome = state.UpdatePlayerSessionCreationPolicy(PlayerSessionCreationPolicy.ACCEPT_ALL);

            // THEN
            mockRequestHandler.Verify(requestHandler => requestHandler.SendRequest(IsAny<Message>()), Times.Never);
            Assert.IsFalse(outcome.Success);
            Assert.AreEqual(outcome.GetType(), typeof(GenericOutcome));
            Assert.AreEqual(GameLiftErrorType.GAMESESSION_ID_NOT_SET, outcome.Error.ErrorType);
        }

        [Test]
        public void GIVEN_gameSessionNotStarted_WHEN_acceptPlayerSession_THEN_returnsFailedOutcome()
        {
            // GIVEN/WHEN
            GenericOutcome outcome = state.AcceptPlayerSession(PlayerSessionId);

            // THEN
            mockRequestHandler.Verify(requestHandler => requestHandler.SendRequest(IsAny<Message>()), Times.Never);
            Assert.IsFalse(outcome.Success);
            Assert.AreEqual(outcome.GetType(), typeof(GenericOutcome));
            Assert.AreEqual(GameLiftErrorType.GAMESESSION_ID_NOT_SET, outcome.Error.ErrorType);
        }

        [Test]
        public void GIVEN_gameSessionNotStarted_WHEN_removePlayerSession_THEN_returnsFailedOutcome()
        {
            // GIVEN/WHEN
            GenericOutcome outcome = state.RemovePlayerSession(PlayerSessionId);

            // THEN
            mockRequestHandler.Verify(requestHandler => requestHandler.SendRequest(IsAny<Message>()), Times.Never);
            Assert.IsFalse(outcome.Success);
            Assert.AreEqual(outcome.GetType(), typeof(GenericOutcome));
            Assert.AreEqual(GameLiftErrorType.GAMESESSION_ID_NOT_SET, outcome.Error.ErrorType);
        }

        private GenericOutcome callProcessReady(ProcessParameters processParams)
        {
            Task<GenericOutcome> task = Task.Run(() => state.ProcessReady(processParams));
            state.OnActivateServerProcessSuccess("requestId");
            return task.Result;
        }

        private void StartGameSession()
        {
            // GIVEN
            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(IsAny<ActivateServerProcessRequest>()))
                .Returns(new GenericOutcome());
            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(IsAny<Message>()))
                .Returns(new GenericOutcome(new GameLiftError()));
            GameSession gameSession = new GameSession { GameSessionId = GameSessionId };
            callProcessReady(GenericProcessParams);
            state.OnStartGameSession(gameSession);
        }

        [Test]
        public void GIVEN_validRequest_WHEN_describePlayerSessions_THEN_returnsSuccessOutcome()
        {
            // GIVEN
            var initialNextToken = "initialNextToken";
            var initialLimit = 10;
            var capturedRequests = new List<DescribePlayerSessionsRequest>();
            PlayerSession playerSession = new PlayerSession();
            playerSession.PlayerId = PlayerId;
            List<PlayerSession> playerSessions = new List<PlayerSession> { playerSession };

            mockRequestHandler.Setup(requestHandler =>
                    requestHandler.SendRequest(Capture.In(capturedRequests)))
                .Returns(
                    new DescribePlayerSessionsOutcome(new DescribePlayerSessionsResult(playerSessions, NextToken)));
            DescribePlayerSessionsRequest request = new DescribePlayerSessionsRequest
            {
                PlayerSessionId = PlayerSessionId,
                PlayerSessionStatusFilter = PlayerSessionStatusFilter,
                NextToken = initialNextToken,
                Limit = initialLimit,
            };

            // WHEN
            DescribePlayerSessionsOutcome outcome = state.DescribePlayerSessions(request);

            // THEN
            AssertSuccessOutcome(outcome);
            Assert.AreEqual(NextToken, outcome.Result.NextToken);
            Assert.AreEqual(playerSession, outcome.Result.PlayerSessions[0]);
            Assert.That(capturedRequests, Has.Count.EqualTo(1));
            Assert.AreEqual(MessageActions.DescribePlayerSessions, capturedRequests.First().Action);
            Assert.Null(capturedRequests.First().GameSessionId);
            Assert.Null(capturedRequests.First().PlayerId);
            Assert.AreEqual(PlayerSessionId, capturedRequests.First().PlayerSessionId);
            Assert.AreEqual(PlayerSessionStatusFilter, capturedRequests.First().PlayerSessionStatusFilter);
            Assert.AreEqual(initialLimit, capturedRequests.First().Limit);
            Assert.AreEqual(initialNextToken, capturedRequests.First().NextToken);
        }

        [Test]
        public void GIVEN_validRequest_AND_noLimitSpecified_WHEN_describePlayerSessions_THEN_defaultLimitUsed()
        {
            // GIVEN
            var capturedRequests = new List<DescribePlayerSessionsRequest>();
            PlayerSession playerSession = new PlayerSession();
            playerSession.PlayerId = PlayerId;
            List<PlayerSession> playerSessions = new List<PlayerSession> { playerSession };

            mockRequestHandler.Setup(requestHandler =>
                    requestHandler.SendRequest(Capture.In(capturedRequests)))
                .Returns(
                    new DescribePlayerSessionsOutcome(new DescribePlayerSessionsResult(playerSessions, NextToken)));
            DescribePlayerSessionsRequest request = new DescribePlayerSessionsRequest
            {
                GameSessionId = GameSessionId,
            };

            // WHEN
            DescribePlayerSessionsOutcome outcome = state.DescribePlayerSessions(request);

            // THEN
            AssertSuccessOutcome(outcome);
            Assert.AreEqual(NextToken, outcome.Result.NextToken);
            Assert.AreEqual(playerSession, outcome.Result.PlayerSessions[0]);
            Assert.That(capturedRequests, Has.Count.EqualTo(1));
            Assert.AreEqual(MessageActions.DescribePlayerSessions, capturedRequests.First().Action);
            Assert.AreEqual(GameSessionId, capturedRequests.First().GameSessionId);
            Assert.Null(capturedRequests.First().PlayerId);
            Assert.Null(capturedRequests.First().PlayerSessionId);
            Assert.AreEqual(50, capturedRequests.First().Limit);
            Assert.Null(capturedRequests.First().PlayerSessionStatusFilter);
            Assert.Null(capturedRequests.First().NextToken);
        }

        [Test]
        public void GIVEN_validRequestFails_WHEN_describePlayerSessions_THEN_returnsErrorOutcome()
        {
            // GIVEN
            mockRequestHandler.Setup(requestHandler =>
                    requestHandler.SendRequest(IsAny<Message>()))
                .Returns(new GenericOutcome(new GameLiftError(GameLiftErrorType.BAD_REQUEST_EXCEPTION)));
            DescribePlayerSessionsRequest request = new DescribePlayerSessionsRequest
            {
                GameSessionId = GameSessionId,
            };

            // WHEN
            DescribePlayerSessionsOutcome outcome = state.DescribePlayerSessions(request);

            // THEN
            Assert.IsFalse(outcome.Success);
            Assert.AreEqual(outcome.GetType(), typeof(DescribePlayerSessionsOutcome));
            Assert.AreEqual(GameLiftErrorType.BAD_REQUEST_EXCEPTION, outcome.Error.ErrorType);
        }

        [Test]
        public void GIVEN_validRequest_WHEN_startMatchBackfill_THEN_returnsSuccessOutcome()
        {
            // GIVEN
            var capturedRequests = new List<StartMatchBackfillRequest>();
            mockRequestHandler.Setup(requestHandler =>
                    requestHandler.SendRequest(Capture.In(capturedRequests)))
                .Returns(new StartMatchBackfillOutcome(new StartMatchBackfillResult(TicketId)));
            var players = GetPlayers(3);
            StartMatchBackfillRequest request = new StartMatchBackfillRequest(GameSessionArn, MatchmakerArn, players)
            {
                TicketId = TicketId,
            };

            // WHEN
            StartMatchBackfillOutcome outcome = state.StartMatchBackfill(request);

            // THEN
            AssertSuccessOutcome(outcome);
            Assert.AreEqual(TicketId, outcome.Result.TicketId);
            Assert.That(capturedRequests, Has.Count.EqualTo(1));
            Assert.AreEqual(MessageActions.StartMatchBackfill, capturedRequests.First().Action);
            Assert.AreEqual(TicketId, capturedRequests.First().TicketId);
            Assert.AreEqual(GameSessionArn, capturedRequests.First().GameSessionArn);
            Assert.AreEqual(MatchmakerArn, capturedRequests.First().MatchmakingConfigurationArn);
            Assert.AreEqual(players, capturedRequests.First().Players);
        }

        [Test]
        public void GIVEN_nullRequest_WHEN_startMatchBackfill_THEN_returnsErrorOutcome()
        {
            // GIVEN
            var capturedRequests = new List<StartMatchBackfillRequest>();
            mockRequestHandler.Setup(requestHandler =>
                    requestHandler.SendRequest(Capture.In(capturedRequests)))
                .Returns(new StartMatchBackfillOutcome(new StartMatchBackfillResult(TicketId)));

            // WHEN
            StartMatchBackfillOutcome outcome = state.StartMatchBackfill(null);

            // THEN
            Assert.IsFalse(outcome.Success);
            Assert.AreEqual(outcome.GetType(), typeof(StartMatchBackfillOutcome));
            Assert.AreEqual(GameLiftErrorType.VALIDATION_EXCEPTION, outcome.Error.ErrorType);
            mockRequestHandler.Verify(requestHandler => requestHandler.SendRequest(It.IsAny<Message>()), Times.Never());
            Assert.That(capturedRequests, Has.Count.EqualTo(0));
        }

        [Test]
        public void GIVEN_request_nullGameSessionArn_WHEN_startMatchBackfill_THEN_returnsErrorOutcome()
        {
            // GIVEN
            var capturedRequests = new List<StartMatchBackfillRequest>();
            mockRequestHandler.Setup(requestHandler =>
                    requestHandler.SendRequest(Capture.In(capturedRequests)))
                .Returns(new StartMatchBackfillOutcome(new StartMatchBackfillResult(TicketId)));

            var players = GetPlayers(3);

            StartMatchBackfillRequest request = new StartMatchBackfillRequest(null, MatchmakerArn, players)
            {
                TicketId = TicketId,
            };

            // WHEN
            StartMatchBackfillOutcome outcome = state.StartMatchBackfill(request);

            // THEN
            Assert.IsFalse(outcome.Success);
            Assert.AreEqual(outcome.GetType(), typeof(StartMatchBackfillOutcome));
            Assert.AreEqual(GameLiftErrorType.VALIDATION_EXCEPTION, outcome.Error.ErrorType);
            mockRequestHandler.Verify(requestHandler => requestHandler.SendRequest(It.IsAny<Message>()), Times.Never());
            Assert.That(capturedRequests, Has.Count.EqualTo(0));
        }

        [Test]
        public void GIVEN_request_nullMatchmakingConfigurationArn_WHEN_startMatchBackfill_THEN_returnsErrorOutcome()
        {
            // GIVEN
            var capturedRequests = new List<StartMatchBackfillRequest>();
            mockRequestHandler.Setup(requestHandler =>
                    requestHandler.SendRequest(Capture.In(capturedRequests)))
                .Returns(new StartMatchBackfillOutcome(new StartMatchBackfillResult(TicketId)));

            var players = GetPlayers(3);

            StartMatchBackfillRequest request = new StartMatchBackfillRequest(GameSessionArn, null, players)
            {
                TicketId = TicketId,
            };

            // WHEN
            StartMatchBackfillOutcome outcome = state.StartMatchBackfill(request);

            // THEN
            Assert.IsFalse(outcome.Success);
            Assert.AreEqual(outcome.GetType(), typeof(StartMatchBackfillOutcome));
            Assert.AreEqual(GameLiftErrorType.VALIDATION_EXCEPTION, outcome.Error.ErrorType);
            mockRequestHandler.Verify(requestHandler => requestHandler.SendRequest(It.IsAny<Message>()), Times.Never());
            Assert.That(capturedRequests, Has.Count.EqualTo(0));
        }

        [Test]
        public void GIVEN_request_nullTicketId_WHEN_startMatchBackfill_THEN_returnsSuccessOutcome()
        {
            // GIVEN
            var capturedRequests = new List<StartMatchBackfillRequest>();
            mockRequestHandler.Setup(requestHandler =>
                    requestHandler.SendRequest(Capture.In(capturedRequests)))
                .Returns(new StartMatchBackfillOutcome(new StartMatchBackfillResult(TicketId)));

            var players = GetPlayers(3);

            StartMatchBackfillRequest request = new StartMatchBackfillRequest(GameSessionArn, MatchmakerArn, players)
            {
                TicketId = null,
            };

            // WHEN
            StartMatchBackfillOutcome outcome = state.StartMatchBackfill(request);

            // THEN
            AssertSuccessOutcome(outcome);
            Assert.AreEqual(TicketId, outcome.Result.TicketId);
            Assert.That(capturedRequests, Has.Count.EqualTo(1));
            Assert.AreEqual(MessageActions.StartMatchBackfill, capturedRequests.First().Action);
            Assert.IsNull(capturedRequests.First().TicketId);
            Assert.AreEqual(GameSessionArn, capturedRequests.First().GameSessionArn);
            Assert.AreEqual(MatchmakerArn, capturedRequests.First().MatchmakingConfigurationArn);
            Assert.AreEqual(players, capturedRequests.First().Players);
        }

        [Test]
        public void GIVEN_validRequestFails_WHEN_startMatchBackfill_THEN_returnsErrorOutcome()
        {
            // GIVEN
            mockRequestHandler.Setup(requestHandler =>
                    requestHandler.SendRequest(IsAny<Message>()))
                .Returns(new GenericOutcome(new GameLiftError(GameLiftErrorType.BAD_REQUEST_EXCEPTION)));
            var players = GetPlayers(3);
            StartMatchBackfillRequest request = new StartMatchBackfillRequest(GameSessionArn, MatchmakerArn, players)
            {
                TicketId = TicketId,
            };

            // WHEN
            StartMatchBackfillOutcome outcome = state.StartMatchBackfill(request);

            // THEN
            Assert.IsFalse(outcome.Success);
            Assert.AreEqual(outcome.GetType(), typeof(StartMatchBackfillOutcome));
            Assert.AreEqual(GameLiftErrorType.BAD_REQUEST_EXCEPTION, outcome.Error.ErrorType);
        }

        [Test]
        public void GIVEN_validRequest_WHEN_stopMatchBackfill_THEN_returnsSuccessOutcome()
        {
            // GIVEN
            var capturedRequests = new List<StopMatchBackfillRequest>();
            mockRequestHandler.Setup(requestHandler =>
                    requestHandler.SendRequest(Capture.In(capturedRequests)))
                .Returns(new GenericOutcome());
            StopMatchBackfillRequest request = new StopMatchBackfillRequest(GameSessionArn, MatchmakerArn, TicketId);

            // WHEN
            GenericOutcome outcome = state.StopMatchBackfill(request);

            // THEN
            AssertSuccessOutcome(outcome);
            Assert.That(capturedRequests, Has.Count.EqualTo(1));
            Assert.AreEqual(MessageActions.StopMatchBackfill, capturedRequests.First().Action);
            Assert.AreEqual(TicketId, capturedRequests.First().TicketId);
            Assert.AreEqual(GameSessionArn, capturedRequests.First().GameSessionArn);
            Assert.AreEqual(MatchmakerArn, capturedRequests.First().MatchmakingConfigurationArn);
        }

        [Test]
        public void GIVEN_validRequestFails_WHEN_stopMatchBackfill_THEN_returnsErrorOutcome()
        {
            // GIVEN
            mockRequestHandler.Setup(requestHandler =>
                    requestHandler.SendRequest(IsAny<Message>()))
                .Returns(new GenericOutcome(new GameLiftError(GameLiftErrorType.BAD_REQUEST_EXCEPTION)));
            StopMatchBackfillRequest request = new StopMatchBackfillRequest(GameSessionArn, MatchmakerArn, TicketId);

            // WHEN
            GenericOutcome outcome = state.StopMatchBackfill(request);

            // THEN
            Assert.IsFalse(outcome.Success);
            Assert.AreEqual(outcome.GetType(), typeof(GenericOutcome));
            Assert.AreEqual(GameLiftErrorType.BAD_REQUEST_EXCEPTION, outcome.Error.ErrorType);
        }

        [Test]
        public void GIVEN_nullRequest_WHEN_stopMatchBackfill_THEN_returnsErrorOutcome()
        {
            // GIVEN
            var capturedRequests = new List<StopMatchBackfillRequest>();
            mockRequestHandler.Setup(requestHandler =>
                    requestHandler.SendRequest(Capture.In(capturedRequests)))
                .Returns(new GenericOutcome());

            // WHEN
            GenericOutcome outcome = state.StopMatchBackfill(null);

            // THEN
            Assert.IsFalse(outcome.Success);
            Assert.AreEqual(outcome.GetType(), typeof(GenericOutcome));
            Assert.AreEqual(GameLiftErrorType.VALIDATION_EXCEPTION, outcome.Error.ErrorType);
            mockRequestHandler.Verify(requestHandler => requestHandler.SendRequest(It.IsAny<Message>()), Times.Never());
            Assert.That(capturedRequests, Has.Count.EqualTo(0));
        }

        [Test]
        public void GIVEN_request_nullGameSessionArn_WHEN_stopMatchBackfill_THEN_returnsErrorOutcome()
        {
            // GIVEN
            var capturedRequests = new List<StopMatchBackfillRequest>();
            mockRequestHandler.Setup(requestHandler =>
                    requestHandler.SendRequest(Capture.In(capturedRequests)))
                .Returns(new GenericOutcome());
            StopMatchBackfillRequest request = new StopMatchBackfillRequest(null, MatchmakerArn, TicketId);

            // WHEN
            GenericOutcome outcome = state.StopMatchBackfill(request);

            // THEN
            Assert.IsFalse(outcome.Success);
            Assert.AreEqual(outcome.GetType(), typeof(GenericOutcome));
            Assert.AreEqual(GameLiftErrorType.VALIDATION_EXCEPTION, outcome.Error.ErrorType);
            mockRequestHandler.Verify(requestHandler => requestHandler.SendRequest(It.IsAny<Message>()), Times.Never());
            Assert.That(capturedRequests, Has.Count.EqualTo(0));
        }

        [Test]
        public void GIVEN_request_nullMatchmakingConfigurationArn_WHEN_stopMatchBackfill_THEN_returnsErrorOutcome()
        {
            // GIVEN
            var capturedRequests = new List<StopMatchBackfillRequest>();
            mockRequestHandler.Setup(requestHandler =>
                    requestHandler.SendRequest(Capture.In(capturedRequests)))
                .Returns(new GenericOutcome());
            StopMatchBackfillRequest request = new StopMatchBackfillRequest(GameSessionArn, null, TicketId);

            // WHEN
            GenericOutcome outcome = state.StopMatchBackfill(request);

            // THEN
            Assert.IsFalse(outcome.Success);
            Assert.AreEqual(outcome.GetType(), typeof(GenericOutcome));
            Assert.AreEqual(GameLiftErrorType.VALIDATION_EXCEPTION, outcome.Error.ErrorType);
            mockRequestHandler.Verify(requestHandler => requestHandler.SendRequest(It.IsAny<Message>()), Times.Never());
            Assert.That(capturedRequests, Has.Count.EqualTo(0));
        }

        [Test]
        public void GIVEN_request_nullTicketId_WHEN_stopMatchBackfill_THEN_returnsErrorOutcome()
        {
            // GIVEN
            var capturedRequests = new List<StopMatchBackfillRequest>();
            mockRequestHandler.Setup(requestHandler =>
                    requestHandler.SendRequest(Capture.In(capturedRequests)))
                .Returns(new GenericOutcome());
            StopMatchBackfillRequest request = new StopMatchBackfillRequest(GameSessionArn, MatchmakerArn, null);

            // WHEN
            GenericOutcome outcome = state.StopMatchBackfill(request);

            // THEN
            Assert.IsFalse(outcome.Success);
            Assert.AreEqual(outcome.GetType(), typeof(GenericOutcome));
            Assert.AreEqual(GameLiftErrorType.VALIDATION_EXCEPTION, outcome.Error.ErrorType);
            mockRequestHandler.Verify(requestHandler => requestHandler.SendRequest(It.IsAny<Message>()), Times.Never());
            Assert.That(capturedRequests, Has.Count.EqualTo(0));
        }

        [Test]
        public void GIVEN_onHealthCheckReturnsTrue_WHEN_ProcessReady_THEN_reportAsHealthy()
        {
            // GIVEN
            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(IsAny<Message>()))
                .Returns(new GenericOutcome(new GameLiftError()));
            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(IsAny<HeartbeatServerProcessRequest>()))
                .Returns(new GenericOutcome());
            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(IsAny<ActivateServerProcessRequest>()))
                .Returns(new GenericOutcome());
            ProcessParameters processParams = new ProcessParameters(
                (gameSession) => { },
                (updateGameSession) => { },
                () => { },
                () => true,
                1234,
                new LogParameters(new List<string> { "C:\\game\\logs", "C:\\game\\error" }));

            // WHEN
            GenericOutcome outcome = callProcessReady(processParams);

            // ProcessReady sends heartbeat messages asynchronously, in a separate thread. Need to delay before verifying mocks.
            Thread.Sleep(5000);

            // THEN
            mockRequestHandler.Verify(
                requestHandler => requestHandler.SendRequest(
                    Is<ActivateServerProcessRequest>(req => req.SdkVersion == SdkVersion && req.SdkLanguage == "CSharp" && req.Port == PortNumber && req.LogPaths.SequenceEqual(LogPaths))), Times.Once);
            mockRequestHandler.Verify(
                requestHandler => requestHandler.SendRequest(
                    Is<HeartbeatServerProcessRequest>(req => req.HealthStatus && req.Action == MessageActions.HeartbeatServerProcess)), Times.Once);
            AssertSuccessOutcome(outcome);
        }

        [Test]
        public void GIVEN_onHealthCheckReturnsFalse_WHEN_ProcessReady_THEN_reportAsUnhealthy()
        {
            // GIVEN
            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(IsAny<Message>()))
                .Returns(new GenericOutcome(new GameLiftError()));
            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(IsAny<HeartbeatServerProcessRequest>()))
                .Returns(new GenericOutcome());
            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(IsAny<ActivateServerProcessRequest>()))
                .Returns(new GenericOutcome());
            ProcessParameters processParams = new ProcessParameters(
                (gameSession) => { },
                (updateGameSession) => { },
                () => { },
                () => false, // OnHealthCheck
                1234,
                new LogParameters(new List<string> { "C:\\game\\logs", "C:\\game\\error" }));

            // WHEN
            GenericOutcome outcome = callProcessReady(processParams);
            // ProcessReady sends heartbeat messages asynchronously, in a separate thread. Need to delay before verifying mocks.
            Thread.Sleep(5000);

            // THEN
            mockRequestHandler.Verify(
                requestHandler => requestHandler.SendRequest(
                    Is<ActivateServerProcessRequest>(req => req.SdkVersion == SdkVersion && req.SdkLanguage == "CSharp" && req.Port == PortNumber && req.LogPaths.SequenceEqual(LogPaths))), Times.Once);
            mockRequestHandler.Verify(
                requestHandler => requestHandler.SendRequest(
                    Is<HeartbeatServerProcessRequest>(req => !req.HealthStatus && req.Action == MessageActions.HeartbeatServerProcess)), Times.Once);
            AssertSuccessOutcome(outcome);
        }

        [Test]
        public void GIVEN_onHealthCheckTimesOut_WHEN_ProcessReady_THEN_reportAsUnhealthy()
        {
            // GIVEN
            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(IsAny<Message>()))
                .Returns(new GenericOutcome(new GameLiftError()));
            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(IsAny<HeartbeatServerProcessRequest>()))
                .Returns(new GenericOutcome());
            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(IsAny<ActivateServerProcessRequest>()))
                .Returns(new GenericOutcome());
            ProcessParameters processParams = new ProcessParameters(
                (gameSession) => { },
                (updateGameSession) => { },
                () => { },
                () =>
                {
                    Thread.Sleep(70000);
                    return true;
                },
                1234,
                new LogParameters(new List<string> { "C:\\game\\logs", "C:\\game\\error" }));

            // WHEN
            GenericOutcome outcome = callProcessReady(processParams);
            Console.WriteLine("Sleeping for 60 seconds to ensure onHealthCheck() times out...");
            Thread.Sleep(60000);

            // THEN
            mockRequestHandler.Verify(
                requestHandler => requestHandler.SendRequest(
                    Is<ActivateServerProcessRequest>(req => req.SdkVersion == SdkVersion && req.SdkLanguage == "CSharp" && req.Port == PortNumber && req.LogPaths.SequenceEqual(LogPaths))), Times.Once);
            mockRequestHandler.Verify(
                requestHandler => requestHandler.SendRequest(
                    Is<HeartbeatServerProcessRequest>(req => !req.HealthStatus && req.Action == MessageActions.HeartbeatServerProcess)), Times.Once);
            Assert.IsTrue(outcome.Success);
        }

        [Test]
        public void GIVEN_sdkToolEnvironmentVariable_WHEN_processReady_THEN_activateServerReportsSdkToolName()
        {
            // GIVEN
            Environment.SetEnvironmentVariable("GAMELIFT_SDK_TOOL_NAME", SdkToolName, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("GAMELIFT_SDK_TOOL_VERSION", SdkToolVersion, EnvironmentVariableTarget.Process);

            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(IsAny<Message>()))
                .Returns(new GenericOutcome(new GameLiftError()));
            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(IsAny<HeartbeatServerProcessRequest>()))
                .Returns(new GenericOutcome());
            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(IsAny<ActivateServerProcessRequest>()))
                .Returns(new GenericOutcome());
            ProcessParameters processParams = new ProcessParameters(
                (gameSession) => { },
                (updateGameSession) => { },
                () => { },
                () => true, // OnHealthCheck
                1234,
                new LogParameters(new List<string> { "C:\\game\\logs", "C:\\game\\error" }));

            // WHEN
            GenericOutcome outcome = callProcessReady(processParams);

            // THEN
            mockRequestHandler.Verify(
                requestHandler => requestHandler.SendRequest(
                    Is<ActivateServerProcessRequest>(req => req.SdkToolName == SdkToolName && req.SdkToolVersion == SdkToolVersion)), Times.Once);
            AssertSuccessOutcome(outcome);

            Environment.SetEnvironmentVariable("GAMELIFT_SDK_TOOL_NAME", null, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("GAMELIFT_SDK_TOOL_VERSION", null, EnvironmentVariableTarget.Process);
        }

        [Test]
        public void GIVEN_noSdkToolEnvironmentVariable_WHEN_processReady_THEN_activateServerDoesNotReportsSdkToolName()
        {
            // GIVEN
            Environment.SetEnvironmentVariable("GAMELIFT_SDK_TOOL_NAME", null, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("GAMELIFT_SDK_TOOL_VERSION", null, EnvironmentVariableTarget.Process);

            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(IsAny<Message>()))
                .Returns(new GenericOutcome(new GameLiftError()));
            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(IsAny<HeartbeatServerProcessRequest>()))
                .Returns(new GenericOutcome());
            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(IsAny<ActivateServerProcessRequest>()))
                .Returns(new GenericOutcome());
            ProcessParameters processParams = new ProcessParameters(
                (gameSession) => { },
                (updateGameSession) => { },
                () => { },
                () => true, // OnHealthCheck
                1234,
                new LogParameters(new List<string> { "C:\\game\\logs", "C:\\game\\error" }));

            // WHEN
            GenericOutcome outcome = callProcessReady(processParams);

            // THEN
            mockRequestHandler.Verify(
                requestHandler => requestHandler.SendRequest(
                    Is<ActivateServerProcessRequest>(req => string.IsNullOrEmpty(req.SdkToolName) && string.IsNullOrEmpty(req.SdkToolVersion))), Times.Once);
            AssertSuccessOutcome(outcome);

            Environment.SetEnvironmentVariable("GAMELIFT_SDK_TOOL_NAME", null, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("GAMELIFT_SDK_TOOL_VERSION", null, EnvironmentVariableTarget.Process);
        }

        [Test]
        public void GIVEN_wait71Seconds_WHEN_ProcessReady_THEN_heartbeatTwice()
        {
            // GIVEN
            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(IsAny<Message>()))
                .Returns(new GenericOutcome(new GameLiftError()));
            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(IsAny<HeartbeatServerProcessRequest>()))
                .Returns(new GenericOutcome());
            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(IsAny<ActivateServerProcessRequest>()))
                .Returns(new GenericOutcome());
            ProcessParameters processParams = new ProcessParameters(
                (gameSession) => { },
                (updateGameSession) => { },
                () => { },
                () => true, // OnHealthCheck
                1234,
                new LogParameters(new List<string> { "C:\\game\\logs", "C:\\game\\error" }));

            // WHEN
            GenericOutcome outcome = callProcessReady(processParams);
            Console.WriteLine("Sleeping for 71 seconds to ensure 2 heartbeats occur...");
            Thread.Sleep(71000);

            // THEN
            mockRequestHandler.Verify(
                requestHandler => requestHandler.SendRequest(
                    Is<ActivateServerProcessRequest>(req => req.SdkVersion == SdkVersion && req.SdkLanguage == "CSharp" && req.Port == PortNumber && req.LogPaths.SequenceEqual(LogPaths))), Times.Once);
            mockRequestHandler.Verify(
                requestHandler => requestHandler.SendRequest(
                    Is<HeartbeatServerProcessRequest>(req => req.HealthStatus && req.Action == MessageActions.HeartbeatServerProcess)), Times.Exactly(2));
            AssertSuccessOutcome(outcome);
        }

        [Test]
        public void GIVEN_validRequest_WHEN_getComputeCertificate_THEN_returnsSuccessOutcome()
        {
            // GIVEN
            mockRequestHandler.Setup(requestHandler =>
                    requestHandler.SendRequest(IsAny<Message>()))
                .Returns(new GetComputeCertificateOutcome(new GetComputeCertificateResult(ComputeCert, HostId)));

            // WHEN
            GenericOutcome outcome = state.GetComputeCertificate();

            // THEN
            AssertSuccessOutcome(outcome);
        }

        [Test]
        public void GIVEN_validRequestFails_WHEN_getComputeCertificate_THEN_returnsErrorOutcome()
        {
            // GIVEN
            mockRequestHandler.Setup(requestHandler =>
                    requestHandler.SendRequest(IsAny<Message>()))
                .Returns(new GenericOutcome(new GameLiftError(GameLiftErrorType.BAD_REQUEST_EXCEPTION)));

            // WHEN
            GenericOutcome outcome = state.GetComputeCertificate();

            // THEN
            Assert.IsFalse(outcome.Success);
            Assert.AreEqual(outcome.GetType(), typeof(GetComputeCertificateOutcome));
            Assert.AreEqual(GameLiftErrorType.BAD_REQUEST_EXCEPTION, outcome.Error.ErrorType);
        }

        [Test]
        public void GIVEN_validRequest_WHEN_GetFleetRoleCredentials_THEN_returnsSuccessOutcome()
        {
            // GIVEN
            var result = new GetFleetRoleCredentialsResult("a", "b", "c", "d", "e", DateTime.UtcNow);
            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(
                Is<Message>(it => ((GetFleetRoleCredentialsRequest)it).RoleSessionName == $"{FleetId}-{HostId}")))
                .Returns(new GetFleetRoleCredentialsOutcome(result));
            var request = new GetFleetRoleCredentialsRequest("Arn");
            var serverParams = new ServerParameters
            {
                FleetId = FleetId,
                HostId = HostId,
                WebSocketUrl = "WebSocketUrl",
                ProcessId = "ProcessId",
                AuthToken = "AuthToken"
            };
            mockWebSocket.Setup(it => it.Connect(
                    IsAny<string>(),
                    IsAny<string>(),
                    IsAny<string>(),
                    IsAny<string>(),
                    IsAny<string>(),
                    IsAny<string>()))
                .Returns(new GenericOutcome());
            // Initialize networking sets the fleetId and hostId which are used to make the role session name
            state.InitializeNetworking(serverParams);

            // WHEN
            GenericOutcome outcome = state.GetFleetRoleCredentials(request);

            // THEN
            AssertSuccessOutcome(outcome);
        }

        [Test]
        public void GIVEN_validRequest_WHEN_GetFleetRoleCredentialsFails_THEN_returnsErrorOutcome()
        {
            // GIVEN
            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(IsAny<Message>()))
                .Returns(new GenericOutcome(new GameLiftError(GameLiftErrorType.BAD_REQUEST_EXCEPTION)));
            var request = new GetFleetRoleCredentialsRequest("Arn");
            request.RoleSessionName = "test-role-session-name";

            // WHEN
            GenericOutcome outcome = state.GetFleetRoleCredentials(request);

            // THEN
            Assert.IsFalse(outcome.Success);
            Assert.AreEqual(outcome.GetType(), typeof(GetFleetRoleCredentialsOutcome));
            Assert.AreEqual(GameLiftErrorType.BAD_REQUEST_EXCEPTION, outcome.Error.ErrorType);
        }

        [Test]
        public void GIVEN_validRequestWithSessionName_WHEN_GetFleetRoleCredentials_THEN_returnsSuccessOutcome()
        {
            // GIVEN
            var result = new GetFleetRoleCredentialsResult("a", "b", "c", "d", "e", DateTime.UtcNow);
            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(
                    Is<Message>(it => ((GetFleetRoleCredentialsRequest)it).RoleSessionName == "CustomRoleSessionName")))
                .Returns(new GetFleetRoleCredentialsOutcome(result));
            var request = new GetFleetRoleCredentialsRequest("Arn")
            {
                RoleSessionName = "CustomRoleSessionName",
            };

            // WHEN
            GenericOutcome outcome = state.GetFleetRoleCredentials(request);

            // THEN
            AssertSuccessOutcome(outcome);
        }

        [Test]
        public void GIVEN_requestWithLongSessionName_WHEN_GetFleetRoleCredentials_THEN_returnsFailureOutcome()
        {
            // GIVEN
            var request = new GetFleetRoleCredentialsRequest("Arn")
            {
                RoleSessionName = "AVeryLongRoleSessionNameThatWouldIamWouldntAssumeSinceItsOverSixtyFourCharacters",
            };

            // WHEN
            GenericOutcome outcome = state.GetFleetRoleCredentials(request);

            // THEN
            Assert.IsFalse(outcome.Success);
            Assert.AreEqual(outcome.GetType(), typeof(GetFleetRoleCredentialsOutcome));
            Assert.AreEqual(GameLiftErrorType.VALIDATION_EXCEPTION, outcome.Error.ErrorType);
        }

        [Test]
        public void GIVEN_validRequestWithLongFleetName_WHEN_GetFleetRoleCredentials_THEN_returnsSuccessOutcome()
        {
            // GIVEN
            var result = new GetFleetRoleCredentialsResult("a", "b", "c", "d", "e", DateTime.UtcNow);
            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(
                Is<Message>(it => ((GetFleetRoleCredentialsRequest)it).RoleSessionName.Length == 64)))
                .Returns(new GetFleetRoleCredentialsOutcome(result));
            var request = new GetFleetRoleCredentialsRequest("Arn");
            var serverParams = new ServerParameters
            {
                WebSocketUrl = "WebSocketUrl",
                FleetId = "AVeryLongRoleSessionNameThatWouldIamWouldntAssumeSinceItsOverSixtyFourCharacters",
                HostId = HostId,
                ProcessId = "ProcessId",
                AuthToken = "AuthToken"
            };
            mockWebSocket.Setup(it => it.Connect(
                    IsAny<string>(),
                    IsAny<string>(),
                    IsAny<string>(),
                    IsAny<string>(),
                    IsAny<string>(),
                    IsAny<string>()))
                .Returns(new GenericOutcome());
            // Initialize networking sets the fleetId and hostId which are used to make the role session name
            state.InitializeNetworking(serverParams);

            // WHEN
            GenericOutcome outcome = state.GetFleetRoleCredentials(request);

            // THEN
            AssertSuccessOutcome(outcome);
        }

        [Test]
        public void GIVEN_validOnPremRequest_WHEN_GetFleetRoleCredentials_THEN_returnsCachedErrorOutcomes()
        {
            // GIVEN
            var result = new GetFleetRoleCredentialsResult(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, DateTime.UtcNow);
            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(IsAny<Message>()))
                .Returns(new GetFleetRoleCredentialsOutcome(result));
            var request = new GetFleetRoleCredentialsRequest("Arn");
            request.RoleSessionName = "test-role-session-name";

            // WHEN
            GenericOutcome outcome1 = state.GetFleetRoleCredentials(request);
            GenericOutcome outcome2 = state.GetFleetRoleCredentials(request);

            // THEN
            Assert.IsFalse(outcome1.Success);
            Assert.IsFalse(outcome2.Success);
            mockRequestHandler.Verify(it => it.SendRequest(IsAny<Message>()), Times.Once);
        }

        [Test]
        public void GIVEN_validRequests_WHEN_GetFleetRoleCredentials_THEN_cachesAndReturnsSuccessOutcomes()
        {
            // GIVEN
            var expiration = DateTime.UtcNow
                .Add(ServerState.InstanceRoleCredentialTtlMin)
                .Add(TimeSpan.FromMinutes(1));
            var result = new GetFleetRoleCredentialsResult("a", "b", "c", "d", "e", expiration);
            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(
                Is<Message>(it => ((GetFleetRoleCredentialsRequest)it).RoleSessionName == "CustomRoleSessionName")))
                .Returns(new GetFleetRoleCredentialsOutcome(result));
            var request = new GetFleetRoleCredentialsRequest("Arn")
            {
                RoleSessionName = "CustomRoleSessionName",
            };

            // WHEN
            GenericOutcome outcome1 = state.GetFleetRoleCredentials(request);
            GenericOutcome outcome2 = state.GetFleetRoleCredentials(request);

            // THEN
            Assert.IsTrue(outcome1.Success);
            Assert.IsTrue(outcome2.Success);
            mockRequestHandler.Verify(it => it.SendRequest(IsAny<Message>()), Times.Once);
        }

        [Test]
        public void GIVEN_validRequestsAfterTimeout_WHEN_GetFleetRoleCredentials_THEN_doesNotCacheAndReturnsSuccessOutcomes()
        {
            // GIVEN
            var result = new GetFleetRoleCredentialsResult("a", "b", "c", "d", "e", DateTime.UtcNow);
            mockRequestHandler.Setup(requestHandler => requestHandler.SendRequest(
                Is<Message>(it => ((GetFleetRoleCredentialsRequest)it).RoleSessionName == "CustomRoleSessionName")))
                .Returns(new GetFleetRoleCredentialsOutcome(result));
            var request = new GetFleetRoleCredentialsRequest("Arn")
            {
                RoleSessionName = "CustomRoleSessionName",
            };

            // WHEN
            GenericOutcome outcome1 = state.GetFleetRoleCredentials(request);
            GenericOutcome outcome2 = state.GetFleetRoleCredentials(request);

            // THEN
            Assert.IsTrue(outcome1.Success);
            Assert.IsTrue(outcome2.Success);
            mockRequestHandler.Verify(it => it.SendRequest(IsAny<Message>()), Times.Exactly(2));
        }

        [Test]
        public void GIVEN_validRequest_WHEN_onSuccessResponse_THEN_doesNothing()
        {
            // GIVEN
            var requestId = "requestId";

            // WHEN
            state.OnSuccessResponse(requestId);
            // THEN
            mockRequestHandler.Verify(it => it.HandleResponse(
                            Is<string>(reqId => reqId == requestId),
                            Is<GenericOutcome>(outcome => outcome.Success)));
        }

        [Test]
        public void GIVEN_nullRequestId_WHEN_onSuccessResponse_THEN_LogsWithoutCall()
        {
            // GIVEN
            string requestId = null;

            // WHEN
            state.OnSuccessResponse(requestId);

            // THEN
            mockRequestHandler.Verify(it => it.HandleResponse(requestId, new GenericOutcome()), Times.Never());
        }

        [Test]
        public void GIVEN_error_WHEN_onErrorResponse_THEN_returnsErrorOutcome()
        {
            // Given
            var requestId = "requestId";
            var statusCode = (int)HttpStatusCode.BadRequest;
            var message = "Failure";
            var action = "action";

            // When
            state.OnErrorResponse(requestId, statusCode, message, action);

            // Then
            mockRequestHandler.Verify(it => it.HandleResponse(
                Is<string>(reqId => reqId == requestId),
                Is<GenericOutcome>(outcome => !outcome.Success)));
        }

        [Test]
        public void GIVEN_error_WHEN_onErrorResponseWithAction_THEN_returnsErrorOutcome()
        {
            // Given
            var requestId = "requestId";
            var statusCode = (int)HttpStatusCode.BadRequest;
            var message = "Failure";
            var action = MessageActions.ActivateServerProcess;

            // When
            state.OnErrorResponse(requestId, statusCode, message, action);

            // Then
            mockRequestHandler.Verify(it => it.HandleResponse(
                Is<string>(reqId => reqId == requestId),
                Is<GenericOutcome>(outcome => !outcome.Success)));
        }

        private static Player[] GetPlayers(int n)
        {
            Player[] players = new Player[n];

            for (int x = 0; x < n; x++)
            {
                players[x] = GetPlayer(x);
            }

            return players;
        }

        private static Player GetPlayer(int playerNum)
        {
            Player player = new Player();
            player.PlayerId = GetPlayerId(playerNum);
            player.Team = GetTeam(playerNum);

            player.LatencyInMS = new Dictionary<string, int>();
            player.LatencyInMS.Add("region1", 100 + playerNum);
            player.LatencyInMS.Add("region2", 120 + playerNum);

            player.PlayerAttributes = new Dictionary<string, AttributeValue>();
            player.PlayerAttributes.Add("string", new AttributeValue("abc" + playerNum));
            player.PlayerAttributes.Add("number", new AttributeValue(200 + playerNum));
            player.PlayerAttributes.Add("stringlist", new AttributeValue(new[] { "string1-" + playerNum, "string2-" + playerNum }));

            Dictionary<string, double> sdm = new Dictionary<string, double>();
            sdm.Add("string1-" + playerNum, 100.0 + playerNum);
            sdm.Add("string2-" + playerNum, 101.0 + playerNum);
            player.PlayerAttributes.Add("stringdoublemap", new AttributeValue(sdm));

            return player;
        }

        private static string GetTeam(int playerNum)
        {
            return "Team" + playerNum;
        }

        private static string GetPlayerId(int playerNum)
        {
            return "Player" + playerNum;
        }

        private static void AssertSuccessOutcome(GenericOutcome outcome)
        {
            Assert.IsTrue(outcome.Success);
            Assert.IsNull(outcome.Error);
        }

        [Test]
        public void GIVEN_validMetricsParameters_WHEN_initializeMetrics_THEN_returnsSuccess()
        {
            // Given
            var statsdHost = "localhost";
            var statsdPort = 8125;
            var crashHost = "crash-host";
            var crashPort = 8126;
            var flushInterval = 5000;
            var maxPacketSize = 1024;

            var parameters = new MetricsParameters(statsdHost, statsdPort, crashHost, crashPort, flushInterval, maxPacketSize);

            // When
            var outcome = ServerState.Instance.InitializeMetrics(parameters);

            // Then
            Assert.IsTrue(outcome.Success);
            Assert.IsNotNull(outcome.Result);
            Assert.IsNull(outcome.Error);

            // Verify that the provided parameters were used
            var usedParameters = ServerState.Instance._metricsParameters;
            Assert.IsNotNull(usedParameters, "_metricsParameters should be set");
            Assert.AreEqual(statsdHost, usedParameters.StatsdHost);
            Assert.AreEqual(statsdPort, usedParameters.StatsdPort);
            Assert.AreEqual(crashHost, usedParameters.CrashReporterHost);
            Assert.AreEqual(crashPort, usedParameters.CrashReporterPort);
            Assert.AreEqual(flushInterval, usedParameters.FlushIntervalMs);
            Assert.AreEqual(maxPacketSize, usedParameters.MaxPacketSize);
        }

        [Test]
        public void GIVEN_nullMetricsParameters_WHEN_initializeMetrics_THEN_usesDefaults()
        {
            // Given
            // No parameters provided - should use defaults

            // When
            var outcome = ServerState.Instance.InitializeMetrics();

            // Then
            Assert.IsTrue(outcome.Success);
            Assert.IsNotNull(outcome.Result);
            Assert.IsNull(outcome.Error);

            // Verify that default values were used via the testing field
            var usedParameters = ServerState.Instance._metricsParameters;
            Assert.IsNotNull(usedParameters, "_metricsParameters should be set");
            Assert.AreEqual(GameLiftConstants.DefaultStatsdHost, usedParameters.StatsdHost);
            Assert.AreEqual(GameLiftConstants.DefaultStatsdPort, usedParameters.StatsdPort);
            Assert.AreEqual(GameLiftConstants.DefaultMaxPacketSize, usedParameters.MaxPacketSize);
        }

        [Test]
        public void GIVEN_nullMetricsParametersWithEnvVars_WHEN_initializeMetrics_THEN_usesEnvOverrides()
        {
            // Given
            var envHost = "127.0.0.1"; // Different from default but still resolvable
            var envCrashPort = 9126;

            UsingEnvironmentVariables(
                new Dictionary<string, string>
                {
                    [GameLiftConstants.EnvironmentVariableStatsdHost] = envHost,
                    [GameLiftConstants.EnvironmentVariableCrashReporterPort] = envCrashPort.ToString(),
                },
                () =>
                {
                    // When
                    var outcome = ServerState.Instance.InitializeMetrics();

                    // Then
                    if (!outcome.Success)
                    {
                        Assert.Fail($"InitializeMetrics failed: {outcome.Error?.ErrorMessage}");
                    }

                    Assert.IsTrue(outcome.Success);
                    Assert.IsNotNull(outcome.Result);
                    Assert.IsNull(outcome.Error);

                    // Verify that environment variables overrode defaults
                    var usedParameters = ServerState.Instance._metricsParameters;
                    Assert.IsNotNull(usedParameters, "_metricsParameters should be set");
                    Assert.AreEqual(envHost, usedParameters.StatsdHost);
                    Assert.AreEqual(GameLiftConstants.DefaultStatsdPort, usedParameters.StatsdPort);
                    Assert.AreEqual(GameLiftConstants.DefaultCrashReporterHost, usedParameters.CrashReporterHost);
                    Assert.AreEqual(envCrashPort, usedParameters.CrashReporterPort);
                    // Non-overridden values should still be defaults
                    Assert.AreEqual(GameLiftConstants.DefaultMaxPacketSize, usedParameters.MaxPacketSize);
                });
        }

        [Test]
        public void GIVEN_invalidMetricsParameters_WHEN_initializeMetrics_THEN_returnsError()
        {
            // Given
            var parameters = new MetricsParameters(null, 8125, "crash-host", 8126, 5000, 1024);

            // When
            var outcome = ServerState.Instance.InitializeMetrics(parameters);

            // Then
            Assert.IsFalse(outcome.Success);
            Assert.IsNull(outcome.Result);
            Assert.IsNotNull(outcome.Error);
            Assert.AreEqual(GameLiftErrorType.VALIDATION_EXCEPTION, outcome.Error.ErrorType);
        }

        [Test]
        public void GIVEN_metricsInitialized_WHEN_shutdown_THEN_logsMetricsDisposed()
        {
            // Given
            var serverState = new ServerState(mockWebSocket.Object, mockRequestHandler.Object, mockEnvironment.Object);

            // Initialize metrics to create a metrics manager
            var parameters = new MetricsParameters("localhost", 8125, "localhost", 8126, 5000, 1024);
            var initOutcome = serverState.InitializeMetrics(parameters);
            Assert.IsTrue(initOutcome.Success, "Metrics initialization should succeed");

            // Set up log4net memory appender to capture log messages
            var memoryAppender = new log4net.Appender.MemoryAppender();
            var repository = log4net.LogManager.GetRepository();

            // Configure the root logger to DEBUG level to capture the disposal message
            var rootLogger = ((log4net.Repository.Hierarchy.Hierarchy)repository).Root;
            rootLogger.Level = log4net.Core.Level.Debug;
            rootLogger.AddAppender(memoryAppender);
            memoryAppender.ActivateOptions();
            repository.Configured = true;

            // When
            serverState.Shutdown();

            // Then - Check that the log contains the disposal message
            var logEvents = memoryAppender.GetEvents();
            bool disposalLogFound = false;

            foreach (var logEvent in logEvents)
            {
                if (logEvent.RenderedMessage.Contains("Metrics manager disposed."))
                {
                    disposalLogFound = true;
                    break;
                }
            }

            Assert.IsTrue(disposalLogFound, "Expected log message was not found");
        }
    }
}
