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
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Aws.GameLift.Server;
using Aws.GameLift.Server.Model;
using Moq;
using NUnit.Framework;
using Polly;
using WebSocketSharp;
using WebSocketSharp.Server;
using static Moq.It;

namespace Aws.GameLift.Tests.Server
{
    [TestFixture]
    public class GameLiftWebSocketTest
    {
        private static readonly string IDEMPOTENCY_TOKEN_PATTERN =
            "^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$";
        private static readonly TimeSpan DEFAULT_WAIT_TIME = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan DEFAULT_DELAY_TIME = TimeSpan.FromMilliseconds(500);
        private static readonly ResponseMessage DEFAULT_SUCCESS_RESPONSE_MESSAGE = new ResponseMessage
        {
            StatusCode = (int)HttpStatusCode.OK,
            Action = "default",
            RequestId = "requestId",
        };

        private Mock<IWebSocketMessageHandler> mockServerState;

        [SetUp]
        public void SetUp()
        {
            mockServerState = new Mock<IWebSocketMessageHandler>();
        }

        [Test]
        public void GIVEN_serverConnected_WHEN_webSocketConnect_THEN_webSocketIsConnected()
        {
            using (var echoHttpServer = new EchoHttpServer())
            {
                using (var webSocket = new GameLiftWebSocket(mockServerState.Object))
                {
                    webSocket.Connect(echoHttpServer.WebSocketUrl);
                    Assert.IsTrue(webSocket.IsConnected());

                    // Verify IdempotencyToken is passed in the URL
                    string idempotencyToken = echoHttpServer.getIdempotencyToken();
                    Assert.IsNotEmpty(idempotencyToken);
                    Assert.IsTrue(Regex.IsMatch(idempotencyToken, IDEMPOTENCY_TOKEN_PATTERN, RegexOptions.IgnoreCase),
                        $"Token '{idempotencyToken}' does not match expected UUID format");
                }
            }
        }

        [Test]
        public void GIVEN_webSocketIsConnected_WHEN_serverDisconnects_THEN_webSocketIsDisconnected()
        {
            using (var webSocket = new GameLiftWebSocket(mockServerState.Object))
            {
                using (var echoHttpServer = new EchoHttpServer())
                {
                    webSocket.Connect(echoHttpServer.WebSocketUrl);
                    Assert.IsTrue(webSocket.IsConnected());
                }

                Assert.IsFalse(webSocket.IsConnected());
            }
        }

        [Test]
        public void GIVEN_errorResponse_WHEN_activateServerProcess_THEN_OnErrorResponseCalled()
        {
            const string requestId = "requestId";
            const string errorMessage = "error";

            using (var echoHttpServer = new EchoHttpServer())
            {
                using (var webSocket = new GameLiftWebSocket(mockServerState.Object))
                {
                    // GIVEN - The test when the request is throttled
                    mockServerState.Setup(state => state.OnErrorResponse(
                        IsAny<string>(), IsAny<int>(), IsAny<string>(), IsAny<string>())).Verifiable();
                    webSocket.Connect(echoHttpServer.WebSocketUrl);
                    webSocket.SendMessage(new ResponseMessage
                    {
                        StatusCode = (int)HttpStatusCode.BadRequest,
                        Action = MessageActions.ActivateServerProcess,
                        RequestId = requestId,
                        ErrorMessage = errorMessage,
                    });

                    // WHEN
                    Thread.Sleep(DEFAULT_WAIT_TIME);

                    // THEN
                    mockServerState.Verify(
                        state => state.OnErrorResponse(
                        requestId, (int)HttpStatusCode.BadRequest, errorMessage, MessageActions.ActivateServerProcess), Times.Once());
                }
            }
        }

        [Test]
        public void GIVEN_successResponse_WHEN_activateServerProcess_THEN_OnActivateServerProcessCalled()
        {
            using (var echoHttpServer = new EchoHttpServer())
            {
                using (var webSocket = new GameLiftWebSocket(mockServerState.Object))
                {
                    // GIVEN - The test when the request succeeds
                    mockServerState.Setup(state => state.OnActivateServerProcessSuccess(IsAny<string>())).Verifiable();
                    webSocket.Connect(echoHttpServer.WebSocketUrl);
                    webSocket.SendMessage(new ResponseMessage
                    {
                        StatusCode = (int)HttpStatusCode.OK,
                        Action = MessageActions.ActivateServerProcess,
                        RequestId = "requestId",
                    });

                    // WHEN
                    Thread.Sleep(DEFAULT_WAIT_TIME);

                    // THEN
                    mockServerState.Verify(state => state.OnActivateServerProcessSuccess("requestId"), Times.Once());
                }
            }
        }

        [Test]
        public void GIVEN_webSocketIsConnected_WHEN_webSocketDisconnects_THEN_webSocketIsDisconnected()
        {
            using (var echoHttpServer = new EchoHttpServer())
            {
                using (var webSocket = new GameLiftWebSocket(mockServerState.Object))
                {
                    webSocket.Connect(echoHttpServer.WebSocketUrl);
                    Assert.IsTrue(webSocket.IsConnected());
                    webSocket.Disconnect();
                    Assert.IsFalse(webSocket.IsConnected());
                }
            }
        }

        [Test]
        public void GIVEN_serverUnavailable_WHEN_webSocketConnect_THEN_ConnectEventuallyReturnsFailure()
        {
            // Attempt to connect will eventually fail once all retry attempts have finished
            // Use a fewer number of retry attempts to prevent this unit test from running for multiple minutes
            const int oneMaxConnectRetries = 1;

            // Get a dynamic URL for the test
            string testUrl;
            using (var tempServer = new EchoHttpServer())
            {
                testUrl = tempServer.WebSocketUrl;
            } // Server is disposed here, making the URL unavailable

            using (var webSocket = new GameLiftWebSocket(mockServerState.Object, oneMaxConnectRetries))
            {
                AssertTimeout(
                    AssertIsFalse,
                    () => webSocket.Connect(testUrl).Success,
                    TimeSpan.FromMinutes(1),
                    "Connect returned a non-success result");
            }
        }

        [Test]
        public void GIVEN_serverStartsAfterWebsocket_WHEN_webSocketConnect_THEN_ConnectEventuallyReturnsSuccessful()
        {
            // Get a dynamic URL for the test
            string testUrl;
            using (var tempServer = new EchoHttpServer())
            {
                testUrl = tempServer.WebSocketUrl;
            } // Server is disposed here

            // Try to connect in a task thread
            var task = Task.Run(() =>
            {
                using (var webSocket = new GameLiftWebSocket(mockServerState.Object))
                {
                    AssertTimeout(
                        AssertIsTrue,
                        () => webSocket.Connect(testUrl).Success,
                        TimeSpan.FromMinutes(1),
                        "Connect returned a success result");
                }
            });

            // Wait for the thread to start, then wait for a few seconds so it is in the reconnect logic
            SpinWait.SpinUntil(() => task.Status == TaskStatus.Running);
            Thread.Sleep(TimeSpan.FromSeconds(3));

            // Start the server and assert that the connect task completed
            using (var echoHttpServer = new EchoHttpServer(testUrl))
            {
                Assert.IsTrue(task.Wait(TimeSpan.FromSeconds(5)), "Task ran to completion");
            }
        }

        [Test]
        public void GIVEN_connectedToServer_WHEN_serverGoesOfflineTemporarily_THEN_AutomaticallyReconnectToServer()
        {
            string testUrl;
            using (var webSocket = new GameLiftWebSocket(mockServerState.Object))
            {
                // Connect to a server
                using (var echoHttpServer = new EchoHttpServer())
                {
                    testUrl = echoHttpServer.WebSocketUrl;
                    webSocket.Connect(testUrl);
                    Assert.IsTrue(webSocket.IsConnected());
                }

                // Once the server goes offline, verify that the websocket is disconnected
                Assert.IsFalse(webSocket.IsConnected());

                // As the server has temporary offline states, confirm that the websocket becomes connected again while the server exists
                for (var i = 0; i < 3; i++)
                {
                    // Have the server be unavailable for some amount of time before it becomes available again
                    Thread.Sleep(TimeSpan.FromMilliseconds(500 * i));

                    // Make the server available again and confirm the websocket connects
                    // Have a longer maximum wait time due to the sleep
                    using (var echoHttpServer = new EchoHttpServer(testUrl))
                    {
                        AssertTimeoutWithRetries(
                            AssertIsTrue,
                            webSocket.IsConnected,
                            TimeSpan.FromSeconds(10),
                            $"WebSocket connected after server {i} created");
                    }

                    // Confirm the websocket is disconnected once the server is unavailable again
                    AssertTimeoutWithRetries(AssertIsFalse, webSocket.IsConnected, $"WebSocket disconnected after server {i} disconnects");
                }
            }
        }

        [Test]
        public void GIVEN_connectedToServer_WHEN_serverGoesOfflineLongerThanAllowedMaximum_THEN_NeverReconnectsToServer()
        {
            // Create a websocket that only tries to reconnect once
            string testUrl;
            using (var webSocket = new GameLiftWebSocket(mockServerState.Object, 1))
            {
                // Connect to a server
                using (var echoHttpServer = new EchoHttpServer())
                {
                    testUrl = echoHttpServer.WebSocketUrl;
                    webSocket.Connect(testUrl);
                    Assert.IsTrue(webSocket.IsConnected());
                }

                // Once the server goes offline, verify that the websocket is disconnected
                Assert.IsFalse(webSocket.IsConnected());

                // Have the server be unavailable for a few seconds before it becomes available again to ensure all retries fail
                Thread.Sleep(TimeSpan.FromSeconds(10));

                // Make the server available again and confirm the websocket never connects because the retries all failed
                using (var echoHttpServer = new EchoHttpServer(testUrl))
                {
                    // Wait a few seconds for any possible (non-existent) re-connect logic to run and verify that it is still disconnected
                    Thread.Sleep(DEFAULT_WAIT_TIME);
                    Assert.IsFalse(webSocket.IsConnected(), "Websocket remains disconnected even after server is available");
                }
            }
        }

        [Test]
        public void GIVEN_neverConnectedToEchoServer_WHEN_defaultSuccessMessageSent_THEN_sendMessageReturnsError()
        {
            var messageReceivedEvent = SetupServerOnSuccessResponseReceived();

            using (var webSocket = new GameLiftWebSocket(mockServerState.Object))
            {
                // The EchoHttpServer sends the same message back to websocket
                Assert.IsFalse(SendMessageSuccess(webSocket));

                // Wait for the message to be received
                Assert.IsFalse(messageReceivedEvent.WaitOne(0));
            }

            VerifyServerResponseReceived(Times.Never());
        }

        [Test]
        public void GIVEN_connectedToEchoServer_WHEN_defaultSuccessMessageSent_THEN_defaultSuccessMessageReceived()
        {
            var messageReceivedEvent = SetupServerOnSuccessResponseReceived();

            using (var echoHttpServer = new EchoHttpServer())
            {
                using (var webSocket = new GameLiftWebSocket(mockServerState.Object))
                {
                    webSocket.Connect(echoHttpServer.WebSocketUrl);
                    Assert.IsTrue(webSocket.IsConnected());

                    // The EchoHttpServer sends the same message back to websocket
                    Assert.IsTrue(SendMessageSuccess(webSocket));

                    // Wait for the message to be received
                    Assert.IsTrue(messageReceivedEvent.WaitOne(TimeSpan.FromSeconds(1)));
                }
            }

            VerifyServerResponseReceived(Times.Once());
        }

        [Test]
        public void GIVEN_notConnectedToEchoServer_WHEN_defaultSuccessMessageSent_THEN_defaultSuccessMessageReceivedOnceReconnected()
        {
            var messageReceivedEvent = SetupServerOnSuccessResponseReceived();

            string testUrl;
            using (var webSocket = new GameLiftWebSocket(mockServerState.Object))
            {
                using (var echoHttpServer = new EchoHttpServer())
                {
                    testUrl = echoHttpServer.WebSocketUrl;
                    webSocket.Connect(testUrl);
                    Assert.IsTrue(webSocket.IsConnected());
                }

                // Try to send a message while offline. This has to be done in a Task because it blocks until it succeeds
                var sendMessageTask = Task.Run(() => Assert.IsTrue(SendMessageSuccess(webSocket), "SendMessage returned success"));
                AssertTimeoutWithRetries(AssertIsTrue, () => sendMessageTask.Status == TaskStatus.Running, "SendMessage task is running");

                using (var echoHttpServer = new EchoHttpServer(testUrl))
                {
                    // Wait for the server to connect, the message to be received, and the task to be complete.
                    AssertTimeoutWithRetries(AssertIsTrue, webSocket.IsConnected, "WebSocket connected after server re-created");
                    Assert.IsTrue(messageReceivedEvent.WaitOne(TimeSpan.FromSeconds(5)), "Message was echoed back");
                    AssertTimeoutWithRetries(AssertIsTrue, () => sendMessageTask.IsCompleted, "SendMessage task was completed");
                }
            }

            VerifyServerResponseReceived(Times.Once());
        }

        [Test]
        public void GIVEN_notConnectedToEchoServer_WHEN_defaultSuccessMessageSent_THEN_defaultSuccessMessageFailedAfterTimeout()
        {
            var messageReceivedEvent = SetupServerOnSuccessResponseReceived();

            string testUrl;
            // Create a GameLiftWebSocket with a lower maxConnectRetries and maxWaitForConnectedRetries to reduce test time
            using (var webSocket = new GameLiftWebSocket(mockServerState.Object, 0, 1))
            {
                using (var echoHttpServer = new EchoHttpServer())
                {
                    testUrl = echoHttpServer.WebSocketUrl;
                    webSocket.Connect(testUrl);
                    Assert.IsTrue(webSocket.IsConnected());
                }

                // Try to send a message while offline. This will eventually time out and return an error.
                AssertTimeout(AssertIsFalse, () => SendMessageSuccess(webSocket), DEFAULT_WAIT_TIME, "SendMessage returned an error");
                Assert.False(messageReceivedEvent.WaitOne(0), "Message was not echoed back");
            }

            VerifyServerResponseReceived(Times.Never());
        }

        private ManualResetEvent SetupServerOnSuccessResponseReceived()
        {
            // Set up a mock response handler and a message that sets an event when the message is received
            var messageReceivedEvent = new ManualResetEvent(false);
            mockServerState.Setup(p => p
                    .OnSuccessResponse(It.IsAny<string>()))
                .Callback(() => messageReceivedEvent.Set());
            return messageReceivedEvent;
        }

        private void VerifyServerResponseReceived(Times times)
        {
            mockServerState.Verify(p => p.OnSuccessResponse(DEFAULT_SUCCESS_RESPONSE_MESSAGE.RequestId), times);
        }

        private static bool SendMessageSuccess(IGameLiftWebSocket webSocket)
        {
            return webSocket.SendMessage(DEFAULT_SUCCESS_RESPONSE_MESSAGE).Success;
        }

        private static void AssertIsTrue(bool condition, string message)
        {
            Assert.IsTrue(condition, message);
        }

        private static void AssertIsFalse(bool condition, string message)
        {
            Assert.IsFalse(condition, message);
        }

        /**
         * Repeatably call a condition function until it returns true or times out.
         * Asserts that the condition returns true before timeout.
         */
        private static void AssertTimeoutWithRetries(Action<bool, string> assert, Func<bool> condition, TimeSpan timeSpan, string message)
        {
            var maxAttempts = Convert.ToInt32(timeSpan.TotalMilliseconds / DEFAULT_DELAY_TIME.TotalMilliseconds);
            var retryPolicy = Policy.HandleResult<bool>(r => !r)
                .WaitAndRetry(maxAttempts, retry => DEFAULT_DELAY_TIME);
            assert(retryPolicy.Execute(condition), message);
        }

        private static void AssertTimeoutWithRetries(Action<bool, string> assert, Func<bool> condition, string message)
        {
            AssertTimeoutWithRetries(assert, condition, DEFAULT_WAIT_TIME, message);
        }

        /**
         * Call the condition function once and assert the output before the timeout.
         * Stops the function execution and assert fail if it runs longer than the timeout.
         */
        private static void AssertTimeout(Action<bool, string> assert, Func<bool> conditionRunOnce, TimeSpan timeout, string message)
        {
            var conditionResult = false;
            var thread = new Thread(() =>
            {
                conditionResult = conditionRunOnce();
            });
            thread.Start();

            var threadCompleted = thread.Join(timeout);
            if (!threadCompleted)
            {
                thread.Interrupt();
                thread.Join();
            }

            Assert.IsTrue(threadCompleted, $"Thread did not complete: {message}");
            assert(conditionResult, $"Condition was false: {message}");
        }

        /**
         * Http server that the websocket can connect to for unit testing.
         * When it receives a message, it echoes it back to the caller.
         * The server starts when it is constructed and stops when it is disposed.
         */
        private class EchoHttpServer : IDisposable
        {
            private readonly HttpServer server;
            private string idempotencyToken;
            public int Port { get; private set; }
            public string WebSocketUrl => $"ws://localhost:{Port}/Echo";

            public EchoHttpServer()
            {
                // Find an available port
                Port = GetAvailablePort();
                server = new HttpServer(Port);
                server.Log.Level = LogLevel.Trace;
                server.AddWebSocketService<Echo>("/Echo", () => new Echo(this));
                server.Start();
                Assert.IsTrue(server.IsListening, "HttpServer is listening");
            }

            // Constructor that uses a specific URL (for reconnection tests)
            public EchoHttpServer(string existingUrl)
            {
                // Extract port from existing URL
                var uri = new Uri(existingUrl);
                Port = uri.Port;
                server = new HttpServer(Port);
                server.Log.Level = LogLevel.Trace;
                server.AddWebSocketService<Echo>("/Echo", () => new Echo(this));
                server.Start();
                Assert.IsTrue(server.IsListening, "HttpServer is listening");
            }

            private static int GetAvailablePort()
            {
                using (var socket = new Socket(AddressFamily.InterNetwork,
                    SocketType.Stream, ProtocolType.Tcp))
                {
                    socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                    return ((IPEndPoint)socket.LocalEndPoint).Port;
                }
            }

            public string getIdempotencyToken()
            {
                return idempotencyToken;
            }

            public void Dispose()
            {
                try
                {
                    server?.Stop();
                    // Give the socket time to fully close
                    Thread.Sleep(100);
                }
                catch (Exception)
                {
                    // Ignore disposal errors
                }
            }

            private class Echo : WebSocketBehavior
            {
                private readonly EchoHttpServer _parent;

                public Echo(EchoHttpServer parent)
                {
                    _parent = parent;
                }

                protected override void OnOpen()
                {
                    _parent.idempotencyToken = Context.QueryString["IdempotencyToken"];
                }

                protected override void OnMessage(MessageEventArgs e)
                {
                    Send(e.Data);
                }
            }
        }
    }

    /**
     * Extension methods to make unit testing more convenient
     */
    public static class Extensions
    {
        /**
         * GameLiftWebSocket::Connect with empty strings for everything besides websocketUrl
         */
        public static GenericOutcome Connect(this GameLiftWebSocket webSocket, string websocketUrl)
        {
            return webSocket.Connect(websocketUrl, string.Empty, string.Empty, string.Empty, string.Empty, String.Empty);
        }
    }
}
