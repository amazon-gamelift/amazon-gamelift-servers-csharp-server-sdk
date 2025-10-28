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
using System.Text.RegularExpressions;

namespace Aws.GameLift
{
    public static class ValidationCommon
    {
        public static GenericOutcome ValidateString(
            string fieldName,
            string input,
            bool required,
            Regex regex = null,
            int minLength = 1,
            int maxLength = Int32.MaxValue,
            string overrideErrorMessage = null)
        {
            if (string.IsNullOrEmpty(input))
            {
                if (required)
                {
                    return new GenericOutcome(new GameLiftError(GameLiftErrorType.VALIDATION_EXCEPTION, string.Format("{0} is required.", fieldName)));
                }
            }
            else
            {
                if (input.Length < minLength || input.Length > maxLength)
                {
                    if (maxLength == Int32.MaxValue)
                    {
                        return new GenericOutcome(new GameLiftError(
                            GameLiftErrorType.VALIDATION_EXCEPTION,
                            string.Format("{0} is invalid. Length must be at least {1} characters.", fieldName, minLength)));
                    }

                    return new GenericOutcome(new GameLiftError(
                        GameLiftErrorType.VALIDATION_EXCEPTION,
                        string.Format("{0} is invalid. Length must be between {1} and {2} characters.", fieldName, minLength, maxLength)));
                }

                if (regex != null && !regex.IsMatch(input))
                {
                    if (overrideErrorMessage != null)
                    {
                        return new GenericOutcome(new GameLiftError(
                            GameLiftErrorType.VALIDATION_EXCEPTION, overrideErrorMessage));
                    }

                    return new GenericOutcome(new GameLiftError(GameLiftErrorType.VALIDATION_EXCEPTION,
                        string.Format("{0} is invalid. Must match the pattern: {1}.", fieldName, regex.ToString())));
                }
            }

            return new GenericOutcome();
        }
    }
}
