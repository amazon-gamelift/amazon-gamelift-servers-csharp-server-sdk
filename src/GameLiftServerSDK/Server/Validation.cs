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
using System.Text.RegularExpressions;
using Aws.GameLift.Server.Model;

namespace Aws.GameLift.Server
{
    public static class Validation
    {
        // Regex Patterns
        private const string GuidStringPattern = @"^[a-zA-Z0-9.-]+$";
        private const string ComputeIdStringPattern = @"^[a-zA-Z0-9\-]+(\/[a-zA-Z0-9\-]+)?$";
        private const string ArnStringPattern = @"^[a-zA-Z0-9:/-]+$";
        private const string PlayerSessionStatusFilterPattern = @"^(RESERVED|ACTIVE|COMPLETED|TIMEDOUT)$";
        private const string GameLiftArnPattern = @"^arn:(aws|aws-cn):gamelift:([a-z]{2}-[a-z]+-\d{1}):(\d{12})?:([a-z]+)\/(.+)$";
        private const string MatchmakingIdPattern = @"^[a-zA-Z0-9-\.]*$";
        private const string RoleSessionNamePattern = @"^[\w+=,.@-]*$";

        // Error messages
        public const string AwsCredentialsError = "Failed to provide a valid authorization strategy: either AuthToken or AwsRegion and AwsCredentials are required";
        public const string AwsCredentialsErrorContainers = "Failed to provide a valid authorization strategy: either AuthToken or AwsRegion are required";
        public const string PlayerSessionCreationPolicyError =
            "PlayerSessionCreationPolicy must be one of [ACCEPT_ALL, DENY_ALL]";

        public const string DescribePlayerSessionsOneFieldError = "Exactly one of GameSessionId, PlayerSessionId, or PlayerId is required";
        public const string InvalidArnError = "Invalid ARN format.";
        public const string InvalidGameLiftArnError = "Invalid Amazon GameLift Servers ARN format.";
        public const string PlayerSessionStatusFilterError = "PlayerSessionStatusFilter must be one of [RESERVED, ACTIVE, COMPLETED, TIMEDOUT]";
        public const string EmptyPlayersError = "Players cannot be empty.";

        // Length constants
        public const int MinLengthRoleSessionName = 2;
        public const int MaxStringLengthMatchmakingId = 128;
        public const int MaxStringLengthId = 256;
        public const int MaxStringLengthArn = 256;
        public const int MaxStringLengthLong = 1024;
        public const int MaxLengthRoleSessionName = 64;
        public const int PortMin = 1;
        public const int PortMax = 60000;

        // Regex objects
        public static readonly Regex GuidRegex = new Regex(GuidStringPattern);
        public static readonly Regex ComputeIdRegex = new Regex(ComputeIdStringPattern);
        public static readonly Regex ArnRegex = new Regex(ArnStringPattern);
        public static readonly Regex PlayerSessionStatusFilterRegex = new Regex(PlayerSessionStatusFilterPattern);
        public static readonly Regex GameLiftArnRegex = new Regex(GameLiftArnPattern);
        public static readonly Regex MatchmakingIdRegex = new Regex(MatchmakingIdPattern);
        public static readonly Regex RoleSessionNameRegex = new Regex(RoleSessionNamePattern);

        public static GenericOutcome ValidateServerParameters(ServerParameters input, bool isContainerComputeType = false)
        {
            GenericOutcome outcome = ValidationCommon.ValidateString(fieldName: "WebSocketUrl", input: input.WebSocketUrl, required: true);
            if (!outcome.Success)
            {
                return outcome;
            }

            outcome = ValidationCommon.ValidateString(fieldName: "ProcessId", input: input.ProcessId, required: true);
            if (!outcome.Success)
            {
                return outcome;
            }

            outcome = ValidationCommon.ValidateString(fieldName: "HostId", input: input.HostId, required: !isContainerComputeType, regex: ComputeIdRegex, maxLength: MaxStringLengthId);
            if (!outcome.Success)
            {
                return outcome;
            }

            outcome = ValidationCommon.ValidateString(fieldName: "FleetId", input: input.FleetId, required: true, regex: GuidRegex, maxLength: MaxStringLengthId);
            if (!outcome.Success)
            {
                return outcome;
            }

            bool authTokenPassed = !string.IsNullOrEmpty(input.AuthToken);
            bool regionPassed = !string.IsNullOrEmpty(input.AwsRegion);
            bool sigV4ParametersPassed = regionPassed
                                        && !string.IsNullOrEmpty(input.AccessKey)
                                        && !string.IsNullOrEmpty(input.SecretKey);
            if (isContainerComputeType)
            {
                if (!regionPassed && !authTokenPassed)
                {
                    return new GenericOutcome(new GameLiftError(GameLiftErrorType.VALIDATION_EXCEPTION, AwsCredentialsErrorContainers));
                }
            }
            else if (!authTokenPassed && !sigV4ParametersPassed)
            {
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.VALIDATION_EXCEPTION, AwsCredentialsError));
            }

            return outcome;
        }

        public static GenericOutcome ValidateProcessParameters(ProcessParameters input)
        {
            if (input.Port < PortMin || input.Port > PortMax)
            {
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.VALIDATION_EXCEPTION,
                    string.Format("Port must be between {0} and {1}", PortMin, PortMax)));
            }

            return new GenericOutcome();
        }

        public static GenericOutcome ValidatePlayerSessionCreationPolicy(PlayerSessionCreationPolicy input)
        {
            if (input != PlayerSessionCreationPolicy.ACCEPT_ALL && input != PlayerSessionCreationPolicy.DENY_ALL)
            {
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.VALIDATION_EXCEPTION, PlayerSessionCreationPolicyError));
            }

            return new GenericOutcome();
        }

        public static GenericOutcome ValidatePlayerSessionId(string input)
        {
            return ValidationCommon.ValidateString(
                fieldName: "PlayerSessionId",
                input: input,
                required: true,
                regex: GuidRegex,
                maxLength: MaxStringLengthId);
        }

        public static GenericOutcome ValidateDescribePlayerSessionsRequest(DescribePlayerSessionsRequest input)
        {
            int numDefined = 0;
            if (!string.IsNullOrEmpty(input.GameSessionId))
            {
                numDefined++;
            }

            if (!string.IsNullOrEmpty(input.PlayerSessionId))
            {
                numDefined++;
            }

            if (!string.IsNullOrEmpty(input.PlayerId))
            {
                numDefined++;
            }

            if (numDefined != 1)
            {
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.VALIDATION_EXCEPTION, DescribePlayerSessionsOneFieldError));
            }

            GenericOutcome outcome = ValidationCommon.ValidateString(
                fieldName: "GameSessionId",
                input: input.GameSessionId,
                required: false,
                regex: ArnRegex,
                maxLength: MaxStringLengthArn,
                overrideErrorMessage: string.Format("GameSessionId is invalid. {0}", InvalidArnError));
            if (!outcome.Success)
            {
                return outcome;
            }

            outcome = ValidationCommon.ValidateString(fieldName: "PlayerSessionId", input: input.PlayerSessionId, required: false, maxLength: MaxStringLengthLong);
            if (!outcome.Success)
            {
                return outcome;
            }

            outcome = ValidationCommon.ValidateString(fieldName: "PlayerId", input: input.PlayerId, required: false, maxLength: MaxStringLengthLong);
            if (!outcome.Success)
            {
                return outcome;
            }

            outcome = ValidationCommon.ValidateString(
                fieldName: "PlayerSessionStatusFilter",
                input: input.PlayerSessionStatusFilter,
                required: false,
                regex: PlayerSessionStatusFilterRegex,
                maxLength: MaxStringLengthLong,
                overrideErrorMessage: PlayerSessionStatusFilterError);
            if (!outcome.Success)
            {
                return outcome;
            }

            return outcome;
        }

        public static GenericOutcome ValidateStartMatchBackfillRequest(StartMatchBackfillRequest input)
        {
            GenericOutcome outcome = ValidationCommon.ValidateString(
                fieldName: "GameSessionArn",
                input: input.GameSessionArn,
                required: true,
                regex: ArnRegex,
                maxLength: MaxStringLengthArn,
                overrideErrorMessage: string.Format("GameSessionArn is invalid. {0}", InvalidArnError));
            if (!outcome.Success)
            {
                return outcome;
            }

            outcome = ValidationCommon.ValidateString(
                fieldName: "MatchmakingConfigurationArn",
                input: input.MatchmakingConfigurationArn,
                required: true,
                regex: GameLiftArnRegex,
                maxLength: MaxStringLengthArn,
                overrideErrorMessage: string.Format("MatchmakingConfigurationArn is invalid. {0}", InvalidGameLiftArnError));
            if (!outcome.Success)
            {
                return outcome;
            }

            outcome = ValidationCommon.ValidateString(
                fieldName: "TicketId",
                input: input.TicketId,
                required: false,
                regex: MatchmakingIdRegex,
                maxLength: MaxStringLengthMatchmakingId);
            if (!outcome.Success)
            {
                return outcome;
            }

            if (input.Players == null || input.Players.Length == 0)
            {
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.VALIDATION_EXCEPTION, EmptyPlayersError));
            }

            return outcome;
        }

        public static GenericOutcome ValidateStopMatchBackfillRequest(StopMatchBackfillRequest input)
        {
            GenericOutcome outcome = ValidationCommon.ValidateString(
                fieldName: "GameSessionArn",
                input: input.GameSessionArn,
                required: true,
                regex: GameLiftArnRegex,
                maxLength: MaxStringLengthArn,
                overrideErrorMessage: string.Format("GameSessionArn is invalid. {0}", InvalidGameLiftArnError));
            if (!outcome.Success)
            {
                return outcome;
            }

            outcome = ValidationCommon.ValidateString(
                fieldName: "MatchmakingConfigurationArn",
                input: input.MatchmakingConfigurationArn,
                required: true,
                regex: GameLiftArnRegex,
                overrideErrorMessage: string.Format("MatchmakingConfigurationArn is invalid. {0}", InvalidGameLiftArnError));
            if (!outcome.Success)
            {
                return outcome;
            }

            outcome = ValidationCommon.ValidateString(
                fieldName: "TicketId",
                input: input.TicketId,
                required: true,
                regex: MatchmakingIdRegex,
                maxLength: MaxStringLengthMatchmakingId);
            if (!outcome.Success)
            {
                return outcome;
            }

            return outcome;
        }

        public static GenericOutcome ValidateGetFleetRoleCredentialsRequest(GetFleetRoleCredentialsRequest input)
        {
            GenericOutcome outcome = ValidationCommon.ValidateString(
                fieldName: "RoleArn",
                input: input.RoleArn,
                required: true,
                regex: ArnRegex,
                maxLength: MaxStringLengthArn,
                overrideErrorMessage: string.Format("RoleArn is invalid. {0}", InvalidArnError));
            if (!outcome.Success)
            {
                return outcome;
            }

            outcome = ValidationCommon.ValidateString(
                fieldName: "RoleSessionName",
                input: input.RoleSessionName,
                required: false,
                regex: RoleSessionNameRegex,
                minLength: MinLengthRoleSessionName,
                maxLength: MaxLengthRoleSessionName);
            if (!outcome.Success)
            {
                return outcome;
            }

            return outcome;
        }

        public static GenericOutcome ValidateMetricsParameters(MetricsParameters input)
        {
            GenericOutcome outcome = ValidationCommon.ValidateString(
                fieldName: "StatsdHost",
                input: input.StatsdHost,
                required: true);
            if (!outcome.Success)
            {
                return outcome;
            }

            if (input.StatsdPort < PortMin || input.StatsdPort > PortMax)
            {
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.VALIDATION_EXCEPTION, $"StatsdPort must be between {PortMin} and {PortMax}"));
            }

            outcome = ValidationCommon.ValidateString(
                fieldName: "CrashReporterHost",
                input: input.CrashReporterHost,
                required: true);
            if (!outcome.Success)
            {
                return outcome;
            }

            if (input.CrashReporterPort < PortMin || input.CrashReporterPort > PortMax)
            {
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.VALIDATION_EXCEPTION, $"StatsdPort must be between {PortMin} and {PortMax}"));
            }

            if (input.FlushIntervalMs < 0)
            {
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.VALIDATION_EXCEPTION, "FlushIntervalMs must be non-negative"));
            }

            if (input.MaxPacketSize < 0)
            {
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.VALIDATION_EXCEPTION, "MaxPacketSize must be non-negative"));
            }

            return new GenericOutcome();
        }
    }
}
