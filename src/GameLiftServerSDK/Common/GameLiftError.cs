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

namespace Aws.GameLift
{
    public enum GameLiftErrorType
    {
        GAMESESSION_ID_NOT_SET,         // No game sessions are bound to this process.
        SERVICE_CALL_FAILED,            // A call to an AWS service has failed.
        BAD_REQUEST_EXCEPTION,          // Generic error when client creates a bad request. 
        UNAUTHORIZED_EXCEPTION,         // Client not authorized to access a required resource due to missing or invalid auth.
        FORBIDDEN_EXCEPTION,            // Client requesting to use resources/operations they aren't allow to access.
        NOT_FOUND_EXCEPTION,            // Request needs a backend resource that the client failed to install.
        CONFLICT_EXCEPTION,             // Client request error caused by stale data.
        TOO_MANY_REQUESTS_EXCEPTION,    // Client called request too often and is being throttled. 
        INTERNAL_SERVICE_EXCEPTION,     // Generic error when Amazon GameLift Servers WebSocket fails to process a request.
        WEBSOCKET_CONNECT_FAILURE,                  // Failure to connect to the Amazon GameLift Servers WebSocket
        WEBSOCKET_CONNECT_FAILURE_FORBIDDEN,        // Amazon GameLift Servers WebSocket returns 403
        WEBSOCKET_CONNECT_FAILURE_TIMEOUT,          // A call to an AWS service has timed out.
        TERMINATION_TIME_NOT_SET,
        VALIDATION_EXCEPTION,                       // Invalid input parameter.
        METRICS_CONFIGURATION_FAILED,               // Failed to configure or define metrics.
        METRICS_SUBMISSION_FAILED,                  // Failed to submit or send metrics data.
    }

    public class GameLiftError
    {
        private const int FourHundredExceptionStart = 400;
        private const int FourHundredExceptionEnd = 500;

        public GameLiftErrorType ErrorType { get; set; }

        public string ErrorName { get; set; }

        public string ErrorMessage { get; set; }

        public GameLiftError()
        {
        }

        public GameLiftError(int statusCode, string errorMessage)
            : this(GetErrorTypeForStatusCode(statusCode), errorMessage)
        {
        }

        public GameLiftError(GameLiftErrorType errorType)
        {
            ErrorType = errorType;
            ErrorName = GetDefaultNameForErrorType(errorType);
            ErrorMessage = GetDefaultMessageForErrorType(errorType);
        }

        public GameLiftError(GameLiftErrorType errorType, string errorMessage)
        {
            ErrorType = errorType;
            ErrorName = GetDefaultNameForErrorType(errorType);
            ErrorMessage = errorMessage;
        }

        public GameLiftError(GameLiftErrorType errorType, string errorName, string errorMessage)
        {
            ErrorType = errorType;
            ErrorName = errorName;
            ErrorMessage = errorMessage;
        }

#pragma warning disable S1541
        private static string GetDefaultNameForErrorType(GameLiftErrorType errorType)
#pragma warning restore S1541
        {
            switch (errorType)
            {
                case GameLiftErrorType.GAMESESSION_ID_NOT_SET:
                    return "GameSession id is not set.";
                case GameLiftErrorType.SERVICE_CALL_FAILED:
                    return "Service call failed.";
                case GameLiftErrorType.BAD_REQUEST_EXCEPTION:
                    return "Bad request exception.";
                case GameLiftErrorType.UNAUTHORIZED_EXCEPTION:
                    return "Unauthorized exception.";
                case GameLiftErrorType.FORBIDDEN_EXCEPTION:
                    return "Forbidden exception.";
                case GameLiftErrorType.NOT_FOUND_EXCEPTION:
                    return "Not found exception.";
                case GameLiftErrorType.CONFLICT_EXCEPTION:
                    return "Conflict exception.";
                case GameLiftErrorType.TOO_MANY_REQUESTS_EXCEPTION:
                    return "Throttling exception.";
                case GameLiftErrorType.INTERNAL_SERVICE_EXCEPTION:
                    return "Internal service exception.";
                case GameLiftErrorType.WEBSOCKET_CONNECT_FAILURE:
                    return "WebSocket Connection Failed";
                case GameLiftErrorType.WEBSOCKET_CONNECT_FAILURE_FORBIDDEN:
                    return "WebSocket Connection Denied";
                case GameLiftErrorType.WEBSOCKET_CONNECT_FAILURE_TIMEOUT:
                    return "WebSocket Connection has timed out";
                case GameLiftErrorType.TERMINATION_TIME_NOT_SET:
                    return "Termination time is not set";
                case GameLiftErrorType.VALIDATION_EXCEPTION:
                    return "Validation exception";
                case GameLiftErrorType.METRICS_CONFIGURATION_FAILED:
                    return "Metrics configuration failed";
                case GameLiftErrorType.METRICS_SUBMISSION_FAILED:
                    return "Metrics submission failed";
                default:
                    return "Unknown Error";
            }
        }

#pragma warning disable S1541
        private static string GetDefaultMessageForErrorType(GameLiftErrorType errorType)
#pragma warning restore S1541
        {
            switch (errorType)
            {
                case GameLiftErrorType.GAMESESSION_ID_NOT_SET:
                    return "No game sessions are bound to this process.";
                case GameLiftErrorType.SERVICE_CALL_FAILED:
                    return "An AWS service call has failed. See the root cause error for more information.";
                case GameLiftErrorType.BAD_REQUEST_EXCEPTION:
                    return "Bad request exception.";
                case GameLiftErrorType.UNAUTHORIZED_EXCEPTION:
                    return "Unauthorized exception.";
                case GameLiftErrorType.FORBIDDEN_EXCEPTION:
                    return "Forbidden exception.";
                case GameLiftErrorType.NOT_FOUND_EXCEPTION:
                    return "Not found exception.";
                case GameLiftErrorType.CONFLICT_EXCEPTION:
                    return "Conflict exception.";
                case GameLiftErrorType.TOO_MANY_REQUESTS_EXCEPTION:
                    return "Throttling exception.";
                case GameLiftErrorType.INTERNAL_SERVICE_EXCEPTION:
                    return "Internal service exception.";
                case GameLiftErrorType.WEBSOCKET_CONNECT_FAILURE:
                    return "Connection to the Amazon GameLift Servers WebSocket has failed";
                case GameLiftErrorType.WEBSOCKET_CONNECT_FAILURE_FORBIDDEN:
                    return "Connection to the Amazon GameLift Servers WebSocket Connection is denied. A HTTP Status Code 403, Forbidden. Check Authentication Token.";
                case GameLiftErrorType.WEBSOCKET_CONNECT_FAILURE_TIMEOUT:
                    return "Connection to the Amazon GameLift Servers WebSocket has timed out.";
                case GameLiftErrorType.TERMINATION_TIME_NOT_SET:
                    return "Termination time is not set because a game session has not started.";
                case GameLiftErrorType.VALIDATION_EXCEPTION:
                    return "Validation exception";
                case GameLiftErrorType.METRICS_CONFIGURATION_FAILED:
                    return "Failed to configure or define metrics. Check metric parameters and configuration.";
                case GameLiftErrorType.METRICS_SUBMISSION_FAILED:
                    return "Failed to submit or send metrics data. Check network connectivity and StatsD configuration.";
                default:
                    return "An unexpected error has occurred.";
            }
        }

        private static GameLiftErrorType GetErrorTypeForStatusCode(int statusCode)
        {
            // Catch valid 4xx (client errors) status codes returned by websocket lambda
            // All other 4xx codes fallback to "bad request exception"
            if (statusCode >= FourHundredExceptionStart && statusCode < FourHundredExceptionEnd)
            {
                switch (statusCode)
                {
                    case 400:
                        return GameLiftErrorType.BAD_REQUEST_EXCEPTION;
                    case 401:
                        return GameLiftErrorType.UNAUTHORIZED_EXCEPTION;
                    case 403:
                        return GameLiftErrorType.FORBIDDEN_EXCEPTION;
                    case 404:
                        return GameLiftErrorType.NOT_FOUND_EXCEPTION;
                    case 409:
                        return GameLiftErrorType.CONFLICT_EXCEPTION;
                    case 429:
                        return GameLiftErrorType.TOO_MANY_REQUESTS_EXCEPTION;
                }

                return GameLiftErrorType.BAD_REQUEST_EXCEPTION;
            }

            // The websocket can return other error types, in this case classify it as an internal service exception
            return GameLiftErrorType.INTERNAL_SERVICE_EXCEPTION;
        }

        public override string ToString()
        {
            return string.Format("[GameLiftError: ErrorType={0}, ErrorName={1}, ErrorMessage={2}]", ErrorType, ErrorName, ErrorMessage);
        }
    }
}
