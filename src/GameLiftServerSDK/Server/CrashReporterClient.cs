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
using System.Net.Http;
using log4net;
using Polly;

namespace Aws.GameLift.Server
{
    /// <summary>
    /// This class is a client to communicate with OTEL Collector Crash Reporter.
    /// </summary>
    public sealed class CrashReporterClient : IDisposable
    {
        private const string RegisterProcessUrlPath = "register";
        private const string UpdateProcessUrlPath = "update";
        private const string DeregisterProcessUrlPath = "deregister";
        private const string ProcessPidParameterName = "process_pid";
        private const string SessionIdParameterName = "session_id";

        // Retry configuration constants
        private const int MaxRetries = 5;
        private const int BaseDelayMs = 1000;
        private const int MaxDelayMs = 16000;


        private readonly HttpClient httpClient;

        private static ILog Log { get; } = LogManager.GetLogger(typeof(ServerState));

        public CrashReporterClient(string host, int port)
        {
            httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri($"http://{host}:{port}");
        }

        public CrashReporterClient(HttpClient httpClient)
        {
            this.httpClient = httpClient;
        }

        private static bool IsRetryableException(Exception exception)
        {
            var errorMessage = exception.Message;
            return errorMessage.Contains("Connection refused") || 
                   errorMessage.Contains("Connection failed");
        }

        public void RegisterProcess()
        {
            int processPid = System.Diagnostics.Process.GetCurrentProcess().Id;
            var requestUri = $"{RegisterProcessUrlPath}?{ProcessPidParameterName}={processPid}";

            Log.Info($"Registering process with {ProcessPidParameterName} {processPid} in OTEL Collector Crash Reporter");

            // Create retry policy with exponential backoff
            var retryPolicy = Policy
                .Handle<Exception>(IsRetryableException)
                .WaitAndRetry(
                    retryCount: MaxRetries,
                    sleepDurationProvider: retryAttempt =>
                    {
                        var delay = Math.Min(MaxDelayMs, BaseDelayMs * Math.Pow(2, retryAttempt - 1));
                        var jitter = new Random().Next(0, (int)(delay * 0.1));
                        return TimeSpan.FromMilliseconds(delay + jitter);
                    },
                    onRetry: (exception, timespan, retryCount, context) =>
                    {
                        Log.Warn($"Failed to register {ProcessPidParameterName} {processPid} to OTEL Collector Crash Reporter " +
                                $"(attempt {retryCount}/{MaxRetries}). Retrying in {timespan.TotalSeconds:F1}s. Error: {exception.Message}");
                    });

            try
            {
                retryPolicy.Execute(() =>
                {
                    var response = httpClient.GetAsync(requestUri).Result;
                    if (response.IsSuccessStatusCode)
                    {
                        Log.Info($"Successfully registered {ProcessPidParameterName} {processPid} to OTEL Collector Crash Reporter");
                    }
                    else
                    {
                        Log.Error($"Failed to register {ProcessPidParameterName} {processPid} to OTEL Collector Crash Reporter, " +
                                 $"Http response: {response.StatusCode} - {response.ReasonPhrase}");
                    }
                });
            }
            catch (Exception e)
            {
                Log.Error($"Failed to register {ProcessPidParameterName} {processPid} to OTEL Collector Crash Reporter " +
                         $"after {MaxRetries} retries. Final error: {e.Message}", e);
            }
        }

        public void TagGameSession(string sessionId)
        {
            int processPid = System.Diagnostics.Process.GetCurrentProcess().Id;
            var requestUri =
                $"{UpdateProcessUrlPath}?{ProcessPidParameterName}={processPid}&{SessionIdParameterName}={sessionId}";
            try
            {
                var response = httpClient.GetAsync(requestUri).Result;

                Log.Info(
                    $"Adding {SessionIdParameterName} tag {sessionId} to process with {ProcessPidParameterName} {processPid} to the OTEL Collector Crash Reporter");
                if (!response.IsSuccessStatusCode)
                {
                    Log.Error(
                        $"Failed to add {SessionIdParameterName} tag {sessionId} to process with {ProcessPidParameterName} {processPid} in the OTEL Collector Crash Reporter, " +
                        $"Http response: {response.StatusCode} - {response.ReasonPhrase}");
                }
            }
            catch (Exception e)
            {
                Log.Error(
                    $"Failed to add {SessionIdParameterName} tag {sessionId} to process with {ProcessPidParameterName} {processPid} in the OTEL Collector Crash Reporter " +
                    $"due to error: {e.Message}",
                    e);
            }
        }

        public void DeregisterProcess()
        {
            int processPid = System.Diagnostics.Process.GetCurrentProcess().Id;
            var requestUri = $"{DeregisterProcessUrlPath}?{ProcessPidParameterName}={processPid}";
            try
            {
                var response = httpClient.GetAsync(requestUri).Result;

                Log.Info(
                    $"Unregistering process with {ProcessPidParameterName} {processPid} in the OTEL Collector Crash Reporter");
                if (!response.IsSuccessStatusCode)
                {
                    Log.Error(
                        $"Failed to deregister {ProcessPidParameterName} {processPid} in the OTEL Collector Crash Reporter, Http response: {response.StatusCode} - {response.ReasonPhrase}");
                }
            }
            catch (Exception e)
            {
                Log.Error(
                    $"Failed to deregister {ProcessPidParameterName} {processPid} in the OTEL Collector Crash Reporter due to error: {e.Message}",
                    e);
            }
        }

        public void Dispose()
        {
            httpClient?.Dispose();
        }
    }
}
