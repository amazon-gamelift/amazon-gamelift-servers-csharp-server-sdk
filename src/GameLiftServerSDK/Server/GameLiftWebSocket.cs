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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Aws.GameLift.Server.Model;
using log4net;
using Newtonsoft.Json;
using Polly;
using WebSocketSharp;
using WebSocket = WebSocketSharp.WebSocket;
using WebSocketState = WebSocketSharp.WebSocketState;

namespace Aws.GameLift.Server
{
    /// <summary>
    /// Methods and classes to handle the connection between your game servers and Amazon GameLift Servers.
    /// </summary>
#pragma warning disable S1200
    public class GameLiftWebSocket : IGameLiftWebSocket
#pragma warning restore S1200
    {
        internal const string PidKey = "pID";
        internal const string ComputeIdKey = "ComputeId";
        internal const string FleetIdKey = "FleetId";
        private const int MaxConnectRetriesDefault = 7;
        private const int ConnectRetryBaseExponentSeconds = 2;
        private const int ConnectRetryInitialPower = 2;
        private const int MaxConnectRetryDelaySeconds = 32;
        private const int MaxDisconnectWaitRetries = 5;
        private const int DisconnectWaitStepMillis = 200;
        private const int ConnectSocketTimeoutMilliseconds = 2000;
        private const int WaitForConnectedMaxRetrySeconds = 310;
        private const int WaitForConnectedRetryDelaySeconds = 2;
        private const int MaxWaitForConnectedRetriesDefault = WaitForConnectedMaxRetrySeconds / WaitForConnectedRetryDelaySeconds;
        private const string SdkVersionKey = "sdkVersion";
        private const string FlavorKey = "sdkLanguage";
        private const string Flavor = "CSharp";
        private const string AuthTokenKey = "Authorization";
        private const string IdempotencyTokenKey = "IdempotencyToken";
        private const string SocketClosingErrorMessage = "An error has occurred in closing the connection";

#if ENABLE_IL2CPP
        private static readonly ILog Log = new UnityDebugLog();
#else
        private static readonly ILog Log = LogManager.GetLogger(typeof(GameLiftWebSocket));
#endif

        private readonly int maxConnectRetries;
        private readonly int maxWaitForConnectedRetries;
        private readonly object performConnectWithRetriesLock = new object(); // ONLY PerformConnectWithRetries should lock this object
        private readonly IWebSocketMessageHandler handler;
        private string websocketUrl;
        private string processId;
        private string hostId;
        private string fleetId;
        private string authToken;
        private string sigV4QueryString;

        private readonly object socketLock = new object();
        private WebSocket socket;
        private CancellationTokenSource socketConnectCancellationSource;

        public GameLiftWebSocket(IWebSocketMessageHandler handler, int maxConnectRetries = MaxConnectRetriesDefault, int maxWaitForConnectedRetries = MaxWaitForConnectedRetriesDefault)
        {
            this.handler = handler;
            this.maxConnectRetries = maxConnectRetries;
            this.maxWaitForConnectedRetries = maxWaitForConnectedRetries;
        }

        public GenericOutcome Connect(string websocketUrl, string processId, string hostId, string fleetId, string authToken = null, string sigV4QueryString = null)
        {
            Log.InfoFormat("Connecting to Amazon GameLift Servers WebSocket. Websocket URL: {0}, processId: {1}, hostId: {2}, fleetId: {3}", websocketUrl, processId, hostId, fleetId);
            this.websocketUrl = websocketUrl;
            this.processId = processId;
            this.hostId = hostId;
            this.fleetId = fleetId;
            this.authToken = authToken;
            this.sigV4QueryString = sigV4QueryString;

            lock (socketLock)
            {
                socket = null;
                socketConnectCancellationSource = new CancellationTokenSource();
            }

            return PerformConnectWithRetries();
        }

        public GenericOutcome Disconnect()
        {
            // Stop ongoing reconnect attempts
            socketConnectCancellationSource?.Cancel();

            WebSocket oldSocket;
            lock (socketLock)
            {
                oldSocket = socket;
                socket = null;
            }

            PerformDisconnect(oldSocket);
            return new GenericOutcome();
        }

        /**
         * Calls Disconnect.
         */
        public void Dispose()
        {
            Disconnect();
            Log.Debug("GameLiftWebSocket disposed");
        }

        /**
          * Develop utility method for simple local testing of sending a message across the websocket.
          * Update the "action" and message/additional fields to test an API Gateway route/response
          */
        public GenericOutcome SendMessage(Message message)
        {
            // Get the currently connected socket
            var sendSocket = WaitForConnectedSocket();
            if (sendSocket == null)
            {
                Log.Error("Failed to send message to Amazon GameLift Servers because socket is not connected");
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.SERVICE_CALL_FAILED));
            }

            string json = JsonConvert.SerializeObject(message);
            try
            {
                Log.Info($"Sending message to Amazon GameLift Servers: {json}");
                sendSocket.Send(json);
            }
            catch (Exception e)
            {
                Log.Error($"Failed to send message to Amazon GameLift Servers. Error: {e.Data}", e);
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.SERVICE_CALL_FAILED));
            }

            return new GenericOutcome();
        }

        /**
         * Returns whether the socket exists and is currently connected.
         */
        public bool IsConnected()
        {
            lock (socketLock)
            {
                return socket != null && socket.IsAlive;
            }
        }

        /**
         * Returns the socket if it is connected, null otherwise.
         */
        private WebSocket SocketIfConnected()
        {
            lock (socketLock)
            {
                return IsConnected() ? socket : null;
            }
        }

        /**
         * Waits for the socket to be connected. Periodically checks whether it is currently connected until it returns
         * success or times out.
         * If the socket is not connected, it schedules a reconnection.
         * Returns the connected socket if it exists and is currently connected by the maximum wait time.
         * Returns null if no socket exists within maximum wait time.
         */
        private WebSocket WaitForConnectedSocket()
        {
            // Short-circuit if already connected or socket was never established in the first place
            lock (socketLock)
            {
                if (IsConnected() || socket == null)
                {
                    return socket;
                }
            }

            // Do not bother waiting for connection if future connection attempts are cancelled
            if (socketConnectCancellationSource.IsCancellationRequested)
            {
                return null;
            }

            // If it is not already connected, schedule a reconnect attempt and wait for the connection to establish.
            Log.Debug("Attempting to re-connect before waiting for socket to be connected");
            Task.Run(PerformConnectWithRetries);

            // Policy that retries if function returns a null socket with with constant interval
            var retryPolicy = Policy
                .HandleResult<WebSocket>(r => r == null)
                .WaitAndRetry(maxWaitForConnectedRetries, retry => TimeSpan.FromSeconds(WaitForConnectedRetryDelaySeconds));

            // Check whether it is connected via the retry policy, and return the socket if it is connected at the end
            Log.Debug("Waiting for re-connect before continuing");
            return retryPolicy.Execute(SocketIfConnected);
        }

        /**
         * Calls PerformConnectWithRetriesWhileLocked while holding a lock.
         * This lock synchronization is required in order to prevent multiple instances of
         * PerformConnectWithRetriesWhileLocked from running simultaneously.
         */
        private GenericOutcome PerformConnectWithRetries()
        {
            lock (performConnectWithRetriesLock)
            {
                return PerformConnectWithRetriesWhileLocked();
            }
        }

        /**
         * Attempts to connect a limited number of times with exponential backoff retries on a failure
         */
        private GenericOutcome PerformConnectWithRetriesWhileLocked()
        {
            GenericOutcome outcome = new GenericOutcome(new GameLiftError(GameLiftErrorType.WEBSOCKET_CONNECT_FAILURE));
            string idempotencyToken = Guid.NewGuid().ToString();

            // Loop for an initial attempt plus MaxConnectRetries attempts
            var maxConnectAttempts = 1 + maxConnectRetries;
            for (var attempt = 0; attempt < maxConnectAttempts; attempt++)
            {
                if (socketConnectCancellationSource.IsCancellationRequested)
                {
                    Log.Debug("Cancelling connection attempt because Disconnect was called");
                    return new GenericOutcome(new GameLiftError(GameLiftErrorType.WEBSOCKET_CONNECT_FAILURE));
                }

                // Must hold the socketLock during the time interacting with the socket in order to prevent
                // the socket OnClose handlers running asynchronously in threads from scheduling additional threads
                // running PerformConnectWithRetries.
                // The OnClose handler only schedules PerformConnectWithRetries if it is a closure for the currently
                // stored socket.
                lock (socketLock)
                {
                    if (IsConnected())
                    {
                        return new GenericOutcome();
                    }

                    // Attempt to connect
                    Log.Debug($"Attempt {attempt} to connect the websocket");
                    outcome = PerformConnect(out var newSocket, idempotencyToken);

                    // On success, "flip" traffic from our old websocket to our new websocket and close the old one if necessary
                    if (outcome.Success)
                    {
                        Log.Debug($"Attempt {attempt} to connect the websocket was successful");
                        var oldSocket = socket;
                        socket = newSocket;
                        CloseSocketAsync(oldSocket);
                        return outcome;
                    }
                }

                // On a failure, sleep with exponential backoff before the loop continues
                // The first attempt should start after a 4 second delay, and there should be no sleep on the final failure
                if (attempt != maxConnectAttempts - 1)
                {
                    var power = ConnectRetryInitialPower + attempt;
                    var sleepSeconds = Math.Min(MaxConnectRetryDelaySeconds, Math.Pow(ConnectRetryBaseExponentSeconds, power));
                    Log.Debug($"Attempt {attempt} to connect the websocket was unsuccessful. Sleeping {sleepSeconds} seconds before retrying");
                    Thread.Sleep(TimeSpan.FromSeconds(sleepSeconds));
                }
            }

            // If the execution reached here, it was not able to connect to the server.
            // Mark it as Disconnected since it will never be able to connect.
            Log.Error("Connecting websocket to server was unsuccessful");
            Disconnect();
            return outcome;
        }

#pragma warning disable S3776,S1541,S138
        private GenericOutcome PerformConnect(out WebSocket outSocket, string idempotencyToken)
#pragma warning restore S3776,S1541,S138
        {
            outSocket = null;

            var newSocket = new WebSocket(CreateUri(idempotencyToken));
            Log.Debug($"Socket {newSocket.GetHashCode():X} created");

            // re-route websocket-sharp logs to use the SDK logger
            newSocket.Log.Output = LogWithGameLiftServerSdk;

            // modify websocket-sharp logging-level to match the SDK's logging level
            // Note: Override if you would like to log websocket library at a different level from the rest of the SDK.
            newSocket.Log.Level = GetLogLevelForWebsockets();

            // Socket connection failed during handshake for TLS errors without this protocol enabled
            newSocket.SslConfiguration.EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12;

            // Countdown latch to ensure that InitSDK() failure response waits for onClose() in order to return an error type 
            CountdownEvent onCloseCountdownEvent = new CountdownEvent(1);
            GameLiftErrorType connectionErrorType = GameLiftErrorType.WEBSOCKET_CONNECT_FAILURE;

            newSocket.OnOpen += (sender, e) =>
            {
                Log.Info($"Socket {sender.GetHashCode():X} connected to Amazon GameLift Servers WebSocket.");
            };

            newSocket.OnClose += (sender, e) =>
            {
                Log.InfoFormat($"Socket {sender.GetHashCode():X} disconnected. Status Code: {e.Code}. Reason: {e.Reason}");

                if (e.Code == (ushort) CloseStatusCode.ProtocolError)
                {
                    connectionErrorType = GameLiftErrorType.WEBSOCKET_CONNECT_FAILURE_FORBIDDEN;
                    Log.Error("Handshake with Amazon GameLift Servers WebSocket failed. Please verify that values of ServerParameters " +
                              "in InitSDK() are correct. For example, process ID needs to be unique between executions, and " +
                              "the authentication token needs to be correct and unexpired.");
                }
                else if (e.Code == (ushort) CloseStatusCode.Abnormal)
                {
                    connectionErrorType = GameLiftErrorType.WEBSOCKET_CONNECT_FAILURE_TIMEOUT;
                    Log.Error("Failed to connect to Amazon GameLift Servers WebSocket. Please verify that the websocket url in " +
                              "InitSDK() is correct and network status is normal.");
                }

                // Resolve countdown latch to unblock InitSDK() from returning a result
                if (!onCloseCountdownEvent.IsSet)
                {
                    onCloseCountdownEvent.Signal();
                }

                // Connect a new socket if possible to improve reliability on temporary network blips.
                // Do not do this in a retry loop because the new socket's OnClose callback will automatically
                // try again if it fails to connect.
                switch ((CloseStatusCode)e.Code)
                {
                    case CloseStatusCode.Normal:
                        Log.Debug("Do not attempt to re-connect due to normal closure likely due to an explicit call to Disconnect.");
                        break;
                    case CloseStatusCode.Away:
                        Log.Debug("Do not attempt to re-connect due to away closure likely due to the server shutting down.");
                        break;
                    default:
                        // Only attempt to schedule a reconnect if a reconnect is not already running and this OnClose
                        // handler was for the current socket
                        // This has to be run in a Task thread because OnClose runs in the PerformConnect thread when
                        // the thread is interrupted which can cause a deadlock.
                        Task.Run(() =>
                        {
                            lock (socketLock)
                            {
                                if (socket == (WebSocket)sender)
                                {
                                    Log.Debug("Attempting to re-connect due to current socket closing");

                                    // Run PerformConnectWithRetries in a Task thread to avoid holding the OnClose handler
                                    // open while another connection attempt is made.
                                    Task.Run(PerformConnectWithRetries);
                                }
                                else
                                {
                                    Log.Debug("Not scheduling re-connect logic for non-current socket");
                                }
                            }
                        });

                        break;
                }
            };

            newSocket.OnError += (sender, e) =>
            {
                if (e.Message != null && e.Message.Contains(SocketClosingErrorMessage))
                {
                    Log.Warn("WebSocket reported error on closing connection. This may be because the connection is already closed");
                }
                else
                {
                    Log.ErrorFormat("Error received from Amazon GameLift Servers WebSocket. Error Message: '{0}'. Exception: '{1}'", e.Message, e.Exception);
                }
            };

            newSocket.OnMessage += (sender, e) =>
            {
                if (e.IsPing)
                {
                    Log.Debug("Received ping from Amazon GameLift Servers WebSocket.");
                    return;
                }

                if (!e.IsText)
                {
                    Log.WarnFormat("Unknown Data received. Data: {0}", e.Data);
                    return;
                }

                try
                {
                    // Parse message as a response message. This has error fields in it which will be null for a
                    // successful response or generic message not associated with a request.
                    ResponseMessage message = JsonConvert.DeserializeObject<ResponseMessage>(e.Data);
                    if (message == null)
                    {
                        Log.Error($"could not parse message. Data: {e.Data}");
                        return;
                    }

                    Log.InfoFormat("Received {0} for Amazon GameLift Servers with status {1}", message.Action, message.StatusCode);

                    // It's safe to cast enums to ints in C#. Each HttpStatusCode enum is associated with its numerical
                    // status code. RequestId will be null when we get a message not associated with a request.
                    if (message.StatusCode != (int)HttpStatusCode.OK && message.RequestId != null)
                    {
                        Log.WarnFormat("Received unsuccessful status code {0} for request {1} with message '{2}'", message.StatusCode, message.RequestId, message.ErrorMessage);
                        handler.OnErrorResponse(message.RequestId, message.StatusCode, message.ErrorMessage, message.Action);
                        return;
                    }

                    switch (message.Action)
                    {
                        case MessageActions.CreateGameSession:
                        {
                            CreateGameSessionMessage createGameSessionMessage = JsonConvert.DeserializeObject<CreateGameSessionMessage>(e.Data);
                            GameSession gameSession = new GameSession(createGameSessionMessage);
                            handler.OnStartGameSession(gameSession);
                            break;
                        }

                        case MessageActions.UpdateGameSession:
                        {
                            UpdateGameSessionMessage updateGameSessionMessage = JsonConvert.DeserializeObject<UpdateGameSessionMessage>(e.Data);
                            handler.OnUpdateGameSession(
                                updateGameSessionMessage.GameSession, UpdateReasonMapper.GetUpdateReasonForName(updateGameSessionMessage.UpdateReason), updateGameSessionMessage.BackfillTicketId);
                            break;
                        }

                        case MessageActions.TerminateProcess:
                        {
                            TerminateProcessMessage terminateProcessMessage = JsonConvert.DeserializeObject<TerminateProcessMessage>(e.Data);
                            handler.OnTerminateProcess(terminateProcessMessage.TerminationTime);
                            break;
                        }

                        case MessageActions.StartMatchBackfill:
                        {
                            StartMatchBackfillResponse startMatchBackfillResponse = JsonConvert.DeserializeObject<StartMatchBackfillResponse>(e.Data);
                            handler.OnStartMatchBackfillResponse(startMatchBackfillResponse.RequestId, startMatchBackfillResponse.TicketId);
                            break;
                        }

                        case MessageActions.DescribePlayerSessions:
                        {
                            DescribePlayerSessionsResponse describePlayerSessionsResponse = JsonConvert.DeserializeObject<DescribePlayerSessionsResponse>(
                                e.Data, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                            handler.OnDescribePlayerSessionsResponse(describePlayerSessionsResponse.RequestId, describePlayerSessionsResponse.PlayerSessions, describePlayerSessionsResponse.NextToken);
                            break;
                        }

                        case MessageActions.GetComputeCertificate:
                        {
                            GetComputeCertificateResponse getComputeCertificateResponse = JsonConvert.DeserializeObject<GetComputeCertificateResponse>(e.Data);
                            handler.OnGetComputeCertificateResponse(getComputeCertificateResponse.RequestId, getComputeCertificateResponse.CertificatePath, getComputeCertificateResponse.ComputeName);
                            break;
                        }

                        case MessageActions.GetFleetRoleCredentials:
                        {
                            var response = JsonConvert.DeserializeObject<GetFleetRoleCredentialsResponse>(e.Data);
                            handler.OnGetFleetRoleCredentialsResponse(
                                response.RequestId,
                                response.AssumedRoleUserArn,
                                response.AssumedRoleId,
                                response.AccessKeyId,
                                response.SecretAccessKey,
                                response.SessionToken,
                                response.Expiration);
                            break;
                        }

                        case MessageActions.RefreshConnection:
                        {
                            var refreshConnectionMessage = JsonConvert.DeserializeObject<RefreshConnectionMessage>(e.Data);
                            authToken = refreshConnectionMessage.AuthToken;
                            handler.OnRefreshConnection(refreshConnectionMessage.RefreshConnectionEndpoint, refreshConnectionMessage.AuthToken);
                            break;
                        }

                        case MessageActions.ActivateServerProcess:
                        {
                            handler.OnActivateServerProcessSuccess(message.RequestId);
                            break;
                        }

                        default:
                            handler.OnSuccessResponse(message.RequestId);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"could not parse message. Data: {e.Data}", ex);
                }
            };

            // Attempt to connect the socket
            var wasSuccessful = SocketConnectWithTimeout(newSocket);
            if (!wasSuccessful)
            {
                // Wait for countdown latch to be resolved in OnClose() in order to know the connection error type
                onCloseCountdownEvent.Wait();

                try
                {
                    if (newSocket.ReadyState != WebSocketState.Closed)
                    {
                        newSocket.CloseAsync(CloseStatusCode.Normal);
                    }
                }
                catch (Exception e)
                {
                    Log.Warn("Failed to close new websocket after a connection failure, ignoring", e);
                }

                return new GenericOutcome(new GameLiftError(connectionErrorType));
            }

            outSocket = newSocket;

            return new GenericOutcome();
        }

        /**
         * The socket library Connect() call does not implement any cancellation or timeout logic and blocks
         * indefinitely when a connection is not immediately successful. This function uses thread interrupts to
         * effectively implement timeout logic.
         *
         * Returns whether the socket is connected
         */
        private bool SocketConnectWithTimeout(WebSocket newSocket)
        {
            // Create and start a new thread that runs Connect
            var thread = new Thread(newSocket.Connect);
            thread.Start();

            // Wait for timeout for the Connect call in the thread to complete
            var timeout = TimeSpan.FromMilliseconds(ConnectSocketTimeoutMilliseconds);
            var threadCompleted = thread.Join(timeout);
            if (!threadCompleted)
            {
                // If the thread did not complete within the timeout, interrupt the thread and wait for it to complete
                Log.Warn($"Socket {newSocket.GetHashCode():X} failed to connect within {timeout.TotalMilliseconds} ms");
                thread.Interrupt();
                thread.Join();
            }

            return newSocket.IsAlive;
        }

        private string CreateUri(string idempotencyToken)
        {
            var uri = new StringBuilder($"{websocketUrl}?");
            uri.Append(string.Format(
                "{0}={1}&{2}={3}&{4}={5}&{6}={7}&{8}={9}&{10}={11}",
                ComputeIdKey,
                hostId,
                FleetIdKey,
                fleetId,
                PidKey,
                processId,
                FlavorKey,
                Flavor,
                SdkVersionKey,
                GameLiftServerAPI.GetSdkVersion().Result,
                IdempotencyTokenKey,
                idempotencyToken));
            if (!string.IsNullOrEmpty(authToken))
            {
                uri.Append($"&{AuthTokenKey}={authToken}");
            }
            else if (!string.IsNullOrEmpty(sigV4QueryString))
            {
                uri.Append($"&{sigV4QueryString}");
            }

            return uri.ToString();
        }

        private static void PerformDisconnect(WebSocket oldSocket)
        {
            if (oldSocket == null)
            {
                Log.Debug("Completed Disconnect with no action taken because socket is uninitialized");
                return;
            }

            Log.DebugFormat("Disconnecting. Socket state is: {0}", oldSocket.ReadyState);

            // If the websocket is already closing (potentially initiated by Amazon GameLift Servers from a ProcessEnding call earlier)
            // Attempt to wait for it to close.
            if (oldSocket.ReadyState == WebSocketState.Closing)
            {
                Log.Info("WebSocket is in Closing state. Attempting to wait for socket to close");
                if (!WaitForSocketToClose(oldSocket))
                {
                    Log.Warn("Timed out waiting for the socket to close. Will retry closing.");
                }
            }

            if (oldSocket.ReadyState != WebSocketState.Closed)
            {
                Log.Debug("Socket is not yet closed. Closing.");
                oldSocket.Close();
            }

            Log.DebugFormat("Completed Disconnect. Socket state is: {0}", oldSocket.ReadyState);
        }

        private static bool WaitForSocketToClose(WebSocket socketToClose)
        {
            // Policy that retries if function returns false with with constant interval
            var retryPolicy = Policy
                .HandleResult<bool>(r => !r)
                .WaitAndRetry(MaxDisconnectWaitRetries, retry => TimeSpan.FromMilliseconds(DisconnectWaitStepMillis));

            return retryPolicy.Execute(() => { return socketToClose.ReadyState == WebSocketState.Closed; });
        }

        private static void CloseSocketAsync(WebSocket socketToClose)
        {
            try
            {
                socketToClose?.CloseAsync(CloseStatusCode.Normal);
            }
            catch (Exception e)
            {
                Log.Warn("Failed to close old websocket after a connection refresh, ignoring", e);
            }
        }

        // Helper method to link WebsocketSharp logger with the server SDK logger
        private static void LogWithGameLiftServerSdk(LogData data, string path)
        {
            string socketLogData = data.ToString();
            switch (data.Level)
            {
                case LogLevel.Info:
                    Log.Info(socketLogData);
                    break;
                case LogLevel.Warn:
                    Log.Warn(socketLogData);
                    break;
                case LogLevel.Error:
                    Log.Error(socketLogData);
                    break;
                case LogLevel.Fatal:
                    Log.Fatal(socketLogData);
                    break;
                default:
                    Log.Debug(socketLogData);
                    break;
            }
        }

        // Helper method to get the logging level the websocket (websocketsharp library) should use.
        // Uses the same logging level as used for server SDK.
        private static LogLevel GetLogLevelForWebsockets()
        {
            if (Log.IsDebugEnabled)
            {
                return LogLevel.Trace;
            }

            if (Log.IsInfoEnabled)
            {
                return LogLevel.Info;
            }

            if (Log.IsWarnEnabled)
            {
                return LogLevel.Warn;
            }

            if (Log.IsErrorEnabled)
            {
                return LogLevel.Error;
            }

            // otherwise, only log fatal by default
            return LogLevel.Fatal;
        }
    }
}
