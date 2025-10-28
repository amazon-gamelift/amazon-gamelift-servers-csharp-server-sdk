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
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Aws.GameLift.Server.Common;
using Aws.GameLift.Server.Model;
using Aws.GameLift.Server.Security;
using log4net;
using WebSocketSharp;

namespace Aws.GameLift.Server
{
#pragma warning disable S1200
    public sealed class ServerState : IWebSocketMessageHandler
#pragma warning restore S1200
    {
        // When within 15 minutes of expiration we retrieve new instance role credentials
        public static readonly TimeSpan InstanceRoleCredentialTtlMin = TimeSpan.FromMinutes(15);
        private const double HealthcheckIntervalSeconds = 60;
        private const double ActivateServerProcessRequestTimeoutSeconds = 6;
        private const double HealthcheckMaxJitterSeconds = 10;
        private const double HealthcheckTimeoutSeconds = HealthcheckIntervalSeconds - HealthcheckMaxJitterSeconds;
        private const int HttpStatusCodeSuccessStart = 200;
        private const int HttpStatusCodeSuccessEnd = 299;

        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0);

        private static readonly Random Random = new Random();

        private readonly IGameLiftWebSocket gameLiftWebSocket;
        private readonly GameLiftWebSocketRequestHandler webSocketRequestHandler;
        private readonly IEnvironmentWrapper environmentWrapper;

        // Map of RoleArn -> Credentials for that role
        private readonly IDictionary<string, GetFleetRoleCredentialsResult> instanceRoleResultCache = new Dictionary<string, GetFleetRoleCredentialsResult>();

        private ProcessParameters processParameters;
        private volatile bool processReady;
        private string gameSessionId;
        private DateTime terminationTime = DateTime.MinValue; // init to 1/1/0001 12:00:00 AM
        private string fleetId;
        private string hostId;
        private string processId;
        // Assume we're on managed EC2, if GetFleetRoleCredentials fails we know to set this to false
        private bool onManagedEc2 = true;
        private Metrics metrics;

        // Visible for testing - stores the MetricsParameters used in InitializeMetrics
        internal MetricsParameters _metricsParameters { get; private set; }

        public static ServerState Instance { get; } = new ServerState();

        public static ILog Log { get; } = LogManager.GetLogger(typeof(ServerState));

        public ServerState(IGameLiftWebSocket webSocket, GameLiftWebSocketRequestHandler requestHandler, IEnvironmentWrapper envWrapper)
        {
            gameLiftWebSocket = webSocket;
            webSocketRequestHandler = requestHandler;
            environmentWrapper = envWrapper;
        }

        private ServerState()
        {
            gameLiftWebSocket = new GameLiftWebSocket(this);
            webSocketRequestHandler = new GameLiftWebSocketRequestHandler(gameLiftWebSocket);
            environmentWrapper = new SystemEnvironmentWrapper();
        }

        public GenericOutcome ProcessReady(ProcessParameters procParameters)
        {
            processParameters = procParameters;
            GenericOutcome result = Validation.ValidateProcessParameters(procParameters);
            if (!result.Success)
            {
                return result;
            }

            DetectGameLiftTools();

            string sdkToolName = Environment.GetEnvironmentVariable(GameLiftConstants.EnvironmentVariableSdkToolName);
            string sdkToolVersion = Environment.GetEnvironmentVariable(GameLiftConstants.EnvironmentVariableSdkToolVersion);

            result = webSocketRequestHandler.SendRequest(new ActivateServerProcessRequest(
                GameLiftServerAPI.GetSdkVersion().Result,
                GameLiftConstants.SdkLanguage,
                sdkToolName,
                sdkToolVersion,
                processParameters.Port,
                processParameters.LogParameters.LogPaths));

            bool timedOut = !SpinWait.SpinUntil(() => processReady, TimeSpan.FromSeconds(ActivateServerProcessRequestTimeoutSeconds));

            if (timedOut)
            {
                Log.Warn("ActivateServerProcess() failed. Returning failure from ProcessReady().");
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.INTERNAL_SERVICE_EXCEPTION, "ActivateServerProcess() failed."));
            }

            return result;
        }

        public GenericOutcome ProcessEnding()
        {
            processReady = false;

            GenericOutcome result = webSocketRequestHandler.SendRequest(new TerminateServerProcessRequest());

            return result;
        }

        public GenericOutcome ActivateGameSession()
        {
            if (string.IsNullOrEmpty(gameSessionId))
            {
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.GAMESESSION_ID_NOT_SET));
            }

            return webSocketRequestHandler.SendRequest(new ActivateGameSessionRequest(gameSessionId));
        }

        public AwsStringOutcome GetGameSessionId()
        {
            if (string.IsNullOrEmpty(gameSessionId))
            {
                return new AwsStringOutcome(new GameLiftError(GameLiftErrorType.GAMESESSION_ID_NOT_SET));
            }

            return new AwsStringOutcome(gameSessionId);
        }

        public AwsDateTimeOutcome GetTerminationTime()
        {
            if (terminationTime == DateTime.MinValue)
            {
                return new AwsDateTimeOutcome(new GameLiftError(GameLiftErrorType.TERMINATION_TIME_NOT_SET));
            }

            return new AwsDateTimeOutcome(terminationTime);
        }

        public GenericOutcome UpdatePlayerSessionCreationPolicy(PlayerSessionCreationPolicy playerSessionPolicy)
        {
            if (string.IsNullOrEmpty(gameSessionId))
            {
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.GAMESESSION_ID_NOT_SET));
            }

            GenericOutcome outcome = Validation.ValidatePlayerSessionCreationPolicy(playerSessionPolicy);
            if (!outcome.Success)
            {
                return outcome;
            }

            return webSocketRequestHandler.SendRequest(new UpdatePlayerSessionCreationPolicyRequest(gameSessionId, playerSessionPolicy));
        }

        public GenericOutcome AcceptPlayerSession(string playerSessionId)
        {
            if (string.IsNullOrEmpty(gameSessionId))
            {
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.GAMESESSION_ID_NOT_SET));
            }

            GenericOutcome outcome = Validation.ValidatePlayerSessionId(playerSessionId);
            if (!outcome.Success)
            {
                return outcome;
            }

            return webSocketRequestHandler.SendRequest(new AcceptPlayerSessionRequest(gameSessionId, playerSessionId));
        }

        public GenericOutcome RemovePlayerSession(string playerSessionId)
        {
            if (string.IsNullOrEmpty(gameSessionId))
            {
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.GAMESESSION_ID_NOT_SET));
            }

            GenericOutcome outcome = Validation.ValidatePlayerSessionId(playerSessionId);
            if (!outcome.Success)
            {
                return outcome;
            }

            return webSocketRequestHandler.SendRequest(new RemovePlayerSessionRequest(gameSessionId, playerSessionId));
        }

#pragma warning disable S3242
        public DescribePlayerSessionsOutcome DescribePlayerSessions(DescribePlayerSessionsRequest request)
#pragma warning restore S3242
        {
            if (request == null)
            {
                return new DescribePlayerSessionsOutcome(new GameLiftError(GameLiftErrorType.BAD_REQUEST_EXCEPTION, "DescribePlayerSessionsRequest is required"));
            }
            GenericOutcome outcome = Validation.ValidateDescribePlayerSessionsRequest(request);
            if (!outcome.Success)
            {
                return new DescribePlayerSessionsOutcome(outcome.Error);
            }

            outcome = webSocketRequestHandler.SendRequest(request);
            if (!outcome.Success)
            {
                return new DescribePlayerSessionsOutcome(outcome.Error);
            }

            return (DescribePlayerSessionsOutcome)outcome;
        }

#pragma warning disable S3242
        public StartMatchBackfillOutcome StartMatchBackfill(StartMatchBackfillRequest request)
#pragma warning restore S3242
        {
            if (request == null)
            {
                return new StartMatchBackfillOutcome(new GameLiftError(GameLiftErrorType.VALIDATION_EXCEPTION, "StartMatchBackfillRequest is required"));
            }
  
            GenericOutcome outcome = Validation.ValidateStartMatchBackfillRequest(request);
            if (!outcome.Success)
            {
                return new StartMatchBackfillOutcome(outcome.Error);
            }
 
            outcome = webSocketRequestHandler.SendRequest(request);
            if (!outcome.Success)
            {
                return new StartMatchBackfillOutcome(outcome.Error);
            }

            return (StartMatchBackfillOutcome)outcome;
        }

#pragma warning disable S3242
        public GenericOutcome StopMatchBackfill(StopMatchBackfillRequest request)
#pragma warning restore S3242
        {
            if (request == null)
            {
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.VALIDATION_EXCEPTION, "StopMatchBackfillRequest is required"));
            }

            GenericOutcome outcome = Validation.ValidateStopMatchBackfillRequest(request);
            if (!outcome.Success)
            {
                return outcome;
            }

            return webSocketRequestHandler.SendRequest(request);
        }

        public void StartHealthCheck()
        {
            Log.InfoFormat(
                "Starting HealthCheck thread. Server SDK will report process health status to Amazon GameLift Servers every: {0} seconds (plus random jitter of up to {1} seconds).",
                HealthcheckIntervalSeconds,
                HealthcheckMaxJitterSeconds);
            while (processReady)
            {
                Task.Run(() => HeartbeatServerProcess());
                Thread.Sleep(TimeSpan.FromSeconds(GetNextHealthCheckIntervalSeconds()));
            }
        }

        private static double GetNextHealthCheckIntervalSeconds()
        {
            // Jitter the healthCheck interval +/- a random value between [-MAX_JITTER_SECONDS, MAX_JITTER_SECONDS]
            double jitter = HealthcheckMaxJitterSeconds * (2 * Random.NextDouble() - 1);
            return HealthcheckIntervalSeconds + jitter;
        }

        private async Task HeartbeatServerProcess()
        {
            // duplicate ProcessReady check here right before invoking
            if (!processReady)
            {
                Log.Debug("Reporting Health on an inactive process. Ignoring.");
                return;
            }

            Log.Debug("Reporting health using the OnHealthCheck callback.");

            bool healthCheckResult;
            try
            {
                var healthCheckResultTask = Task.Run(() => processParameters.OnHealthCheck.Invoke());
                var onHealthCheckCompleted = healthCheckResultTask.Wait(TimeSpan.FromSeconds(HealthcheckTimeoutSeconds));
                if (!onHealthCheckCompleted)
                {
                    Log.Warn("Timed out waiting for onHealthCheck callback to respond. Reporting process as unhealthy.");
                    healthCheckResult = false;
                }
                else
                {
                    healthCheckResult = healthCheckResultTask.Result;
                    if (healthCheckResult)
                    {
                        Log.Info("Received TRUE from the onHealthCheck callback. Reporting process as healthy.");
                    }
                    else
                    {
                        Log.Warn("Received FALSE from the onHealthCheck callback. Reporting process as unhealthy.");
                    }
                }
            }
            catch (AggregateException aex)
            {
                if (aex.InnerExceptions.Any(ix => ix is TaskCanceledException))
                {
                    Log.Warn("Healthcheck task cancelled. Reporting process as unhealthy.");
                }
                else
                {
                    Log.Error("Encountered unexpected error when calling onHealthCheck callback. Reporting process as unhealthy.", aex);
                }

                healthCheckResult = false;
            }

            HeartbeatServerProcessRequest request = new HeartbeatServerProcessRequest(healthCheckResult);
            GenericOutcome outcome = webSocketRequestHandler.SendRequest(request);
            if (!outcome.Success)
            {
                Log.WarnFormat("Failed to report health status to Amazon GameLift Servers service. Error: {0}", outcome.Error);
            }
        }

        public GenericOutcome InitializeNetworking(ServerParameters serverParameters)
        {
            serverParameters.WebSocketUrl = System.Environment.GetEnvironmentVariable(GameLiftConstants.EnvironmentVariableWebsocketUrl) ?? serverParameters.WebSocketUrl;
            serverParameters.ProcessId = System.Environment.GetEnvironmentVariable(GameLiftConstants.EnvironmentVariableProcessId) ?? serverParameters.ProcessId;
            serverParameters.HostId = System.Environment.GetEnvironmentVariable(GameLiftConstants.EnvironmentVariableHostId) ?? serverParameters.HostId;
            serverParameters.FleetId = System.Environment.GetEnvironmentVariable(GameLiftConstants.EnvironmentVariableFleetId) ?? serverParameters.FleetId;
            serverParameters.AuthToken = System.Environment.GetEnvironmentVariable(GameLiftConstants.EnvironmentVariableAuthToken) ?? serverParameters.AuthToken;
            serverParameters.AwsRegion = System.Environment.GetEnvironmentVariable(GameLiftConstants.EnvironmentVariableAwsRegion) ?? serverParameters.AwsRegion;
            serverParameters.AccessKey = System.Environment.GetEnvironmentVariable(GameLiftConstants.EnvironmentVariableAccessKey) ?? serverParameters.AccessKey;
            serverParameters.SecretKey = System.Environment.GetEnvironmentVariable(GameLiftConstants.EnvironmentVariableSecretKey) ?? serverParameters.SecretKey;
            serverParameters.SessionToken = System.Environment.GetEnvironmentVariable(GameLiftConstants.EnvironmentVariableSessionToken) ?? serverParameters.SessionToken;

            var computeType = System.Environment.GetEnvironmentVariable(GameLiftConstants.EnvironmentVariableComputeType);
            bool isContainerComputeType = GameLiftConstants.ComputeTypeContainer.Equals(computeType);

            if (GameLiftConstants.AgentlessContainerProcessId.Equals(System.Environment.GetEnvironmentVariable(GameLiftConstants.EnvironmentVariableProcessId)))
            {
                serverParameters.ProcessId = Guid.NewGuid().ToString();
            }

            GenericOutcome validationOutcome = Validation.ValidateServerParameters(serverParameters, isContainerComputeType);
            if (!validationOutcome.Success)
            {
                return validationOutcome;
            }

            processId = serverParameters.ProcessId;
            hostId = serverParameters.HostId;
            fleetId = serverParameters.FleetId;

            if (!string.IsNullOrEmpty(serverParameters.AuthToken))
            {
                return EstablishNetworking(serverParameters.WebSocketUrl, serverParameters.AuthToken, null);
            }
            else
            {
                if (isContainerComputeType)
                {
                    using (var httpClient = new HttpClient())
                    {
                        var containerCredentialsFetcher = new ContainerCredentialsFetcher(httpClient);
                        var awsCredentials = containerCredentialsFetcher.FetchContainerCredentials();
                        serverParameters.AccessKey = awsCredentials.AccessKey;
                        serverParameters.SecretKey = awsCredentials.SecretKey;
                        serverParameters.SessionToken = awsCredentials.SessionToken;

                        var containerMetadataFetcher = new ContainerMetadataFetcher(httpClient);
                        var containerTaskMetadata = containerMetadataFetcher.FetchContainerTaskMetadata();
                        hostId = containerTaskMetadata.TaskId;
                    }
                }

                var sigV4QueryString = GetSigV4QueryString(serverParameters.AwsRegion, serverParameters.AccessKey, serverParameters.SecretKey, serverParameters.SessionToken);
                return EstablishNetworking(serverParameters.WebSocketUrl, serverParameters.AuthToken, sigV4QueryString);
            }
        }

        private GenericOutcome EstablishNetworking(string webSocketUrl, string authToken, string sigV4QueryString)
        {
            return gameLiftWebSocket.Connect(
                webSocketUrl,
                processId,
                hostId,
                fleetId,
                authToken: authToken,
                sigV4QueryString: sigV4QueryString);
        }

        private string GetSigV4QueryString(
            string awsRegion,
            string accessKey,
            string secretKey,
            string sessionToken)
        {
            var awsCredentials = new AwsCredentials()
            {
                AccessKey = accessKey,
                SecretKey = secretKey,
                SessionToken = sessionToken,
            };
            var queryParamsToSign = new Dictionary<string, string>();
            queryParamsToSign.Add(GameLiftWebSocket.ComputeIdKey, hostId);
            queryParamsToSign.Add(GameLiftWebSocket.FleetIdKey, fleetId);
            queryParamsToSign.Add(GameLiftWebSocket.PidKey, processId);
            var signatureParameters = new SigV4Parameters()
            {
                AwsCredentials = awsCredentials,
                AwsRegion = awsRegion,
                QueryParams = queryParamsToSign,
                RequestTime = DateTime.Now,
            };
            return AwsSigV4Utility.GenerateSigV4QueryString(signatureParameters);
        }

        public GetComputeCertificateOutcome GetComputeCertificate()
        {
            Log.DebugFormat("Calling GetComputeCertificate");

            GetComputeCertificateRequest webSocketRequest = new GetComputeCertificateRequest();
            GenericOutcome outcome = webSocketRequestHandler.SendRequest(webSocketRequest);
            if (!outcome.Success)
            {
                return new GetComputeCertificateOutcome(outcome.Error);
            }

            return (GetComputeCertificateOutcome)outcome;
        }

        public GetFleetRoleCredentialsOutcome GetFleetRoleCredentials(GetFleetRoleCredentialsRequest request)
        {
            Log.DebugFormat("Calling GetFleetRoleCredentials: {0}", request);

            // If we've decided we're not on managed EC2, fail without making an APIGW call
            if (!onManagedEc2)
            {
                Log.DebugFormat("SDK is not running on a managed fleet, fast-failing the request");
                return new GetFleetRoleCredentialsOutcome(new GameLiftError(GameLiftErrorType.BAD_REQUEST_EXCEPTION));
            }

            // Check if we're cached credentials recently that still have at least 15 minutes before expiration
            if (!string.IsNullOrEmpty(request.RoleArn) && instanceRoleResultCache.ContainsKey(request.RoleArn))
            {
                var previousResult = instanceRoleResultCache[request.RoleArn];
                if (previousResult.Expiration.Subtract(InstanceRoleCredentialTtlMin) > DateTime.UtcNow)
                {
                    Log.DebugFormat("Returning cached credentials which expire in {0} seconds", (previousResult.Expiration - DateTime.UtcNow).Seconds);
                    return new GetFleetRoleCredentialsOutcome(previousResult);
                }

                instanceRoleResultCache.Remove(request.RoleArn);
            }

            // validate request before autogenerating role session name to validate any user input first
            GenericOutcome validationOutcome = Validation.ValidateGetFleetRoleCredentialsRequest(request);
            if (!validationOutcome.Success)
            {
                return new GetFleetRoleCredentialsOutcome(validationOutcome.Error);
            }

            // If role session name was not provided, default to fleetId-hostId
            if (request.RoleSessionName.IsNullOrEmpty())
            {
                var generatedRoleSessionName = $"{fleetId}-{hostId}";
                if (generatedRoleSessionName.Length > Validation.MaxLengthRoleSessionName) 
                {
                    generatedRoleSessionName = generatedRoleSessionName.Substring(0, Validation.MaxLengthRoleSessionName);
                }

                request.RoleSessionName = generatedRoleSessionName;
            }

            var rawOutcome = webSocketRequestHandler.SendRequest(request);
            if (!rawOutcome.Success)
            {
                return new GetFleetRoleCredentialsOutcome(rawOutcome.Error);
            }

            var outcome = (GetFleetRoleCredentialsOutcome)rawOutcome;
            var result = outcome.Result;

            // If we get a success response from APIGW with empty fields we're not on managed EC2
            if (result.AccessKeyId == string.Empty)
            {
                onManagedEc2 = false;
                Log.DebugFormat("SDK is not running on a managed fleet, fast-failing the request");
                return new GetFleetRoleCredentialsOutcome(new GameLiftError(GameLiftErrorType.BAD_REQUEST_EXCEPTION));
            }

            instanceRoleResultCache[request.RoleArn] = result;
            return outcome;
        }

        public void OnErrorResponse(string requestId, int statusCode, string errorMessage, string action)
        {
            switch (action)
            {
                case MessageActions.ActivateServerProcess:
                {
                    OnActivateServerProcessFailure(requestId, new GenericOutcome(new GameLiftError(statusCode, errorMessage)));
                    break;
                }
            }

            webSocketRequestHandler.HandleResponse(requestId, new GenericOutcome(new GameLiftError(statusCode, errorMessage)));
        }

        public void OnSuccessResponse(string requestId)
        {
            if (requestId != null)
            {
                webSocketRequestHandler.HandleResponse(requestId, new GenericOutcome());
            }
            else
            {
                Log.Info("RequestId was null");
            }
        }

        public void OnStartGameSession(GameSession gameSession)
        {
            // Inject data that already exists on the server
            gameSession.FleetId = fleetId;
            metrics?.OnGameSessionStart(gameSession);

            Log.DebugFormat("ServerState got the startGameSession signal. GameSession : {0}", gameSession);

            if (!processReady)
            {
                Log.Debug("Got a game session on inactive process. Ignoring.");
                return;
            }

            gameSessionId = gameSession.GameSessionId;

            Task.Run(() =>
            {
                processParameters.OnStartGameSession(gameSession);
            });
        }

        public void OnUpdateGameSession(GameSession gameSession, UpdateReason updateReason, string backfillTicketId)
        {
            Log.DebugFormat("ServerState got the updateGameSession signal. GameSession : {0}", gameSession);

            if (!processReady)
            {
                Log.Warn("Got an updated game session on inactive process.");
                return;
            }

            Task.Run(() =>
            {
                processParameters.OnUpdateGameSession(new UpdateGameSession(gameSession, updateReason, backfillTicketId));
            });
        }

        public void OnTerminateProcess(long terminationTime)
        {
            // TerminationTime is milliseconds that have elapsed since Unix epoch time begins (00:00:00 UTC Jan 1 1970).
            this.terminationTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(terminationTime);

            Log.DebugFormat("ServerState got the terminateProcess signal. termination time : {0}", this.terminationTime);

            metrics?.OnProcessTermination();

            if (processParameters.OnProcessTerminate != null)
            {
                Task.Run(() =>
                {
                    processParameters.OnProcessTerminate();
                });
            }
            else
            {
                Log.DebugFormat("OnProcessTerminate handler is not defined. Calling ProcessEnding() and Destroy()");
                GenericOutcome processEndingOutcome = ProcessEnding();
                GenericOutcome destroyOutcome = GameLiftServerAPI.Destroy();

                if (processEndingOutcome.Success && destroyOutcome.Success)
                {
                    environmentWrapper.Exit(0);
                }
                else
                {
                    if (!processEndingOutcome.Success)
                    {
                        Log.ErrorFormat("ProcessEnding() failed. {0}", processEndingOutcome.Error);
                    }

                    if (!destroyOutcome.Success)
                    {
                        Log.ErrorFormat("Destroy() failed. {0}", destroyOutcome.Error);
                    }

                    environmentWrapper.Exit(-1);
                }
            }
        }

        public void OnStartMatchBackfillResponse(string requestId, string ticketId)
        {
            StartMatchBackfillResult result = new StartMatchBackfillResult(ticketId);
            webSocketRequestHandler.HandleResponse(requestId, new StartMatchBackfillOutcome(result));
        }

        public void OnDescribePlayerSessionsResponse(string requestId, IList<PlayerSession> playerSessions, string nextToken)
        {
            DescribePlayerSessionsResult result = new DescribePlayerSessionsResult(playerSessions, nextToken);
            webSocketRequestHandler.HandleResponse(requestId, new DescribePlayerSessionsOutcome(result));
        }

        public void OnGetComputeCertificateResponse(string requestId, string certificatePath, string computeName)
        {
            GetComputeCertificateResult result = new GetComputeCertificateResult(certificatePath, computeName);
            webSocketRequestHandler.HandleResponse(requestId, new GetComputeCertificateOutcome(result));
        }

        public void OnGetFleetRoleCredentialsResponse(
            string requestId,
            string assumedRoleUserArn,
            string assumedRoleId,
            string accessKeyId,
            string secretAccessKey,
            string sessionToken,
            long expiration)
        {
            var result = new GetFleetRoleCredentialsResult(
                assumedRoleUserArn,
                assumedRoleId,
                accessKeyId,
                secretAccessKey,
                sessionToken,
                Epoch.AddMilliseconds(expiration));
            webSocketRequestHandler.HandleResponse(requestId, new GetFleetRoleCredentialsOutcome(result));
        }

        public void OnRefreshConnection(string refreshConnectionEndpoint, string authToken)
        {
            var outcome = EstablishNetworking(refreshConnectionEndpoint, authToken, null);

            if (!outcome.Success)
            {
                Log.ErrorFormat(
                    "Failed to refresh websocket connection. The server SDK will try again each minute until the refresh succeeds, or the websocket is forcibly closed. {0}",
                    outcome.Error);
            }
        }

        public void OnActivateServerProcessSuccess(string requestId)
        {
            Log.Info("Marking processReady as true. Starting the health check task.");
            processReady = true;
            Task.Run(() => StartHealthCheck());
            webSocketRequestHandler.HandleResponse(requestId, new GenericOutcome());
        }

        public void OnActivateServerProcessFailure(string requestId, GenericOutcome result)
        {
            Log.WarnFormat("Marking processReady as false. ActivateServerProcess() failed for error {0} with message {1}", result.Error.ErrorName, result.Error.ErrorMessage);
            processReady = false;
            webSocketRequestHandler.HandleResponse(requestId, result);
        }

        public MetricsOutcome InitializeMetrics()
        {
            return InitializeMetrics(null);
        }

        public MetricsOutcome InitializeMetrics(MetricsParameters metricsParameters)
        {
            try
            {
                MetricsParameters parameters;

                if (metricsParameters != null)
                {
                    // User provided parameters - use them as-is, no environment variables overrides
                    parameters = metricsParameters;
                }
                else
                {
                    // No user parameters - start with defaults and apply environment variables overrides
                    parameters = CreateDefaultParameters();
                    parameters = ApplyEnvironmentVariableOverrides(parameters);
                }

                GenericOutcome validationOutcome = Validation.ValidateMetricsParameters(parameters);
                if (!validationOutcome.Success)
                {
                    return new MetricsOutcome(validationOutcome.Error);
                }

                // Store parameters for testing verification
                _metricsParameters = parameters;

                var builder = Metrics.Create();
                builder.SetStatsdHost(parameters.StatsdHost)
                       .SetStatsdPort(parameters.StatsdPort)
                       .SetCrashReporterHost(parameters.CrashReporterHost)
                       .SetCrashReporterPort(parameters.CrashReporterPort)
                       .SetFlushInterval(parameters.FlushIntervalMs)
                       .SetMaxPacketSize(parameters.MaxPacketSize);
                metrics = builder.Build();

                // If a game session is already active, tag the metrics manager with the session ID
                if (!string.IsNullOrEmpty(gameSessionId))
                {
                    metrics.OnGameSessionStart(gameSessionId);
                }

                return new MetricsOutcome(metrics);
            }
            catch (System.Exception ex)
            {
                return new MetricsOutcome(new GameLiftError(GameLiftErrorType.METRICS_CONFIGURATION_FAILED, ex.Message));
            }
        }

        private static MetricsParameters CreateDefaultParameters()
        {
            return new MetricsParameters(
                GameLiftConstants.DefaultStatsdHost,
                GameLiftConstants.DefaultStatsdPort,
                GameLiftConstants.DefaultCrashReporterHost,
                GameLiftConstants.DefaultCrashReporterPort,
                GameLiftConstants.DefaultFlushIntervalMs,
                GameLiftConstants.DefaultMaxPacketSize);
        }

        private static MetricsParameters ApplyEnvironmentVariableOverrides(MetricsParameters parameters)
        {
            string envStatsdHost = System.Environment.GetEnvironmentVariable(GameLiftConstants.EnvironmentVariableStatsdHost);
            if (!string.IsNullOrEmpty(envStatsdHost))
            {
                parameters.StatsdHost = envStatsdHost;
            }

            if (int.TryParse(System.Environment.GetEnvironmentVariable(GameLiftConstants.EnvironmentVariableStatsdPort), out int envStatsdPort))
            {
                parameters.StatsdPort = envStatsdPort;
            }

            string envCrashReporterHost = System.Environment.GetEnvironmentVariable(GameLiftConstants.EnvironmentVariableCrashReporterHost);
            if (!string.IsNullOrEmpty(envCrashReporterHost))
            {
                parameters.CrashReporterHost = envCrashReporterHost;
            }

            if (int.TryParse(System.Environment.GetEnvironmentVariable(GameLiftConstants.EnvironmentVariableCrashReporterPort), out int envCrashReporterPort))
            {
                parameters.CrashReporterPort = envCrashReporterPort;
            }

            if (int.TryParse(System.Environment.GetEnvironmentVariable(GameLiftConstants.EnvironmentVariableFlushIntervalMs), out int envFlushInterval))
            {
                parameters.FlushIntervalMs = envFlushInterval;
            }

            if (int.TryParse(System.Environment.GetEnvironmentVariable(GameLiftConstants.EnvironmentVariableMaxPacketSize), out int envMaxPacketSize))
            {
                parameters.MaxPacketSize = envMaxPacketSize;
            }

            return parameters;
        }

        public void Shutdown()
        {
            processReady = false;

            // Sleep thread for 1 sec.
            // This is to help deal with race conditions related to processReady flag being turned off (i.e. HeartbeatServerProcess)
            Thread.Sleep(TimeSpan.FromSeconds(1).Milliseconds);

            gameLiftWebSocket.Disconnect();

            metrics?.Dispose();
        }

        private static void DetectGameLiftTools()
        {
            var metricsDetector = new MetricsDetector();
            metricsDetector.SetGameLiftTool();
        }
    }
}
