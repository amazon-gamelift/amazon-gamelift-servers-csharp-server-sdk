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
    public static class GameLiftConstants
    {
        public static readonly string EnvironmentVariableWebsocketUrl = "GAMELIFT_SDK_WEBSOCKET_URL";
        public static readonly string EnvironmentVariableComputeType = "GAMELIFT_COMPUTE_TYPE";
        public static readonly string EnvironmentVariableProcessId = "GAMELIFT_SDK_PROCESS_ID";
        public static readonly string EnvironmentVariableHostId = "GAMELIFT_SDK_HOST_ID";
        public static readonly string EnvironmentVariableFleetId = "GAMELIFT_SDK_FLEET_ID";
        public static readonly string EnvironmentVariableAuthToken = "GAMELIFT_SDK_AUTH_TOKEN";
        public static readonly string EnvironmentVariableAwsRegion = "GAMELIFT_REGION";
        public static readonly string EnvironmentVariableAccessKey = "GAMELIFT_ACCESS_KEY";
        public static readonly string EnvironmentVariableSecretKey = "GAMELIFT_SECRET_KEY";
        public static readonly string EnvironmentVariableSessionToken = "GAMELIFT_SESSION_TOKEN";
        public static readonly string EnvironmentVariableSdkToolName = "GAMELIFT_SDK_TOOL_NAME";
        public static readonly string EnvironmentVariableSdkToolVersion = "GAMELIFT_SDK_TOOL_VERSION";
        public static readonly string AgentlessContainerProcessId = "ManagedResource";
        public static readonly string ComputeTypeContainer = "CONTAINER";
        public static readonly string SdkLanguage = "CSharp";

        // Metrics environment variables
        public static readonly string EnvironmentVariableStatsdHost = "GAMELIFT_STATSD_HOST";
        public static readonly string EnvironmentVariableStatsdPort = "GAMELIFT_STATSD_PORT";
        public static readonly string EnvironmentVariableCrashReporterHost = "GAMELIFT_CRASH_REPORTER_HOST";
        public static readonly string EnvironmentVariableCrashReporterPort = "GAMELIFT_CRASH_REPORTER_PORT";
        public static readonly string EnvironmentVariableFlushIntervalMs = "GAMELIFT_FLUSH_INTERVAL_MS";
        public static readonly string EnvironmentVariableMaxPacketSize = "GAMELIFT_MAX_PACKET_SIZE";

        // Metrics defaults
        public static readonly string DefaultStatsdHost = "localhost";
        public static readonly int DefaultStatsdPort = 8125;
        public static readonly string DefaultCrashReporterHost = "localhost";
        public static readonly int DefaultCrashReporterPort = 8126;
        public static readonly int DefaultFlushIntervalMs = 10000;
        public static readonly int DefaultMaxPacketSize = 512;
    }
}
