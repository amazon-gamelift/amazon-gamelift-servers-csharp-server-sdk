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

using System.Collections.Concurrent;
using System.Threading.Tasks;
using Aws.GameLift.Server.Model;
using log4net;

namespace Aws.GameLift.Server
{
    public class GameLiftWebSocketRequestHandler
    {
        private const int ServiceCallTimeoutMillis = 20000;

        private readonly ConcurrentDictionary<string, TaskCompletionSource<GenericOutcome>> requestIdToPromise =
                new ConcurrentDictionary<string, TaskCompletionSource<GenericOutcome>>();

        private readonly IGameLiftWebSocket gameLiftWebSocket;

#if ENABLE_IL2CPP
        public static ILog Log { get; } = new UnityDebugLog();
#else
        public static ILog Log { get; } = LogManager.GetLogger(typeof(GameLiftWebSocketRequestHandler));
#endif

        // Empty constructor needed for testing.
        public GameLiftWebSocketRequestHandler()
        {
            gameLiftWebSocket = null;
        }

        public GameLiftWebSocketRequestHandler(IGameLiftWebSocket webSocket)
        {
            gameLiftWebSocket = webSocket;
        }

        /**
         * Takes in a request and sends the request to our APIG via Amazon GameLift Servers WebSocket.
         */
        public virtual GenericOutcome SendRequest(Message request)
        {
            return SendRequest(request, ServiceCallTimeoutMillis);
        }

        /**
         * Takes in a request and timeout and sends the request to our APIG via Amazon GameLift Servers WebSocket.
         */
        public virtual GenericOutcome SendRequest(Message request, int timeoutMillis)
        {
            if (string.IsNullOrEmpty(request.RequestId))
            {
                Log.Error("Request does not have request ID, cannot process.");
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.INTERNAL_SERVICE_EXCEPTION));
            }

            TaskCompletionSource<GenericOutcome> promise = new TaskCompletionSource<GenericOutcome>();
            if (!requestIdToPromise.TryAdd(request.RequestId, promise))
            {
                Log.ErrorFormat("Request {0} already exists.", request.RequestId);
                promise.SetCanceled();
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.INTERNAL_SERVICE_EXCEPTION));
            }

            GenericOutcome outcome = gameLiftWebSocket.SendMessage(request);

            if (!outcome.Success)
            {
                promise.SetCanceled();
                RemovePromise(request.RequestId, out _);
                return outcome;
            }

#pragma warning disable S4462
            if (!promise.Task.Wait(timeoutMillis))
#pragma warning restore S4462
            {
                Log.ErrorFormat("Response not received within time limit for request {0}.", request.RequestId);
                if (RemovePromise(request.RequestId, out _))
                {
                    promise.SetCanceled();
                }

                return new GenericOutcome(new GameLiftError(GameLiftErrorType.WEBSOCKET_CONNECT_FAILURE_TIMEOUT));
            }

            return promise.Task.Result;
        }

        /**
         * Takes in a request id that was made before and sets the given result. Any thread waiting on the result of the
         * promise will then receive the result. Returns true if promise was resolved with result, false otherwise
         * (e.g. requestId not found).
         */
        public virtual void HandleResponse(string requestId, GenericOutcome result)
        {
            if (!RemovePromise(requestId, out var promise))
            {
                Log.DebugFormat("No promise found for request {0}.", requestId);
                return;
            }

            promise.SetResult(result);
        }

        private bool RemovePromise(string requestId, out TaskCompletionSource<GenericOutcome> promise)
        {
            if (!requestIdToPromise.TryRemove(requestId, out promise))
            {
                Log.DebugFormat("Request {0} already removed.", requestId);
                return false;
            }

            return true;
        }
    }
}
