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

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Aws.GameLift
{
    [TestFixture]
    public class GameLiftErrorsTest
    {
        [Test]
        public void JustErrorTypeCreatesObjectWithDefaultNameAndMessage()
        {
            // Given
            // When
            GameLiftError error = new GameLiftError(GameLiftErrorType.SERVICE_CALL_FAILED);

            // Then
            Assert.AreEqual(GameLiftErrorType.SERVICE_CALL_FAILED, error.ErrorType);
            Assert.AreEqual("Service call failed.", error.ErrorName);
            Assert.AreEqual("An AWS service call has failed. See the root cause error for more information.", error.ErrorMessage);
        }

        [Test]
        public void AllParamsCreateFullErrorObject()
        {
            // Given
            string errorName = "Error.";
            string errorMessage = "Error Message.";

            // When
            GameLiftError error = new GameLiftError(GameLiftErrorType.SERVICE_CALL_FAILED, errorName, errorMessage);

            // Then
            Assert.AreEqual(GameLiftErrorType.SERVICE_CALL_FAILED, error.ErrorType);
            Assert.AreEqual(errorName, error.ErrorName);
            Assert.AreEqual(errorMessage, error.ErrorMessage);
        }

        [Test]
        [TestCaseSource(nameof(GetClientSideErrors))]
        public void GIVEN_4xxStatusCode_WHEN_constructor_THEN_convertsToErrorType(int statusCode)
        {
            // Given
            string errorMessage = "Error Message.";

            // When
            GameLiftError error = new GameLiftError(statusCode, errorMessage);

            // Then
            GameLiftErrorType errorType;
            switch (statusCode)
            {
                case 400:
                    errorType = GameLiftErrorType.BAD_REQUEST_EXCEPTION; break;
                case 401:
                    errorType = GameLiftErrorType.UNAUTHORIZED_EXCEPTION; break;
                case 403:
                    errorType = GameLiftErrorType.FORBIDDEN_EXCEPTION; break;
                case 404:
                    errorType = GameLiftErrorType.NOT_FOUND_EXCEPTION; break;
                case 409:
                    errorType = GameLiftErrorType.CONFLICT_EXCEPTION; break;
                case 429:
                    errorType = GameLiftErrorType.TOO_MANY_REQUESTS_EXCEPTION; break;
                default:
                    errorType = GameLiftErrorType.BAD_REQUEST_EXCEPTION; break;
            }

            Assert.AreEqual(errorType, error.ErrorType);
            Assert.AreEqual(errorMessage, error.ErrorMessage);
        }

        public static IEnumerable<int> GetClientSideErrors()
        {
            return Enumerable.Range(400, 100);
        }

        [Test]
        [TestCaseSource(nameof(GetServerSideErrors))]
        public void GIVEN_5xxStatusCode_WHEN_constructor_THEN_convertsToErrorType(int statusCode)
        {
            // Given
            string errorMessage = "Error Message.";

            // When
            GameLiftError error = new GameLiftError(statusCode, errorMessage);

            // Then
            Assert.AreEqual(GameLiftErrorType.INTERNAL_SERVICE_EXCEPTION, error.ErrorType);
            Assert.AreEqual(errorMessage, error.ErrorMessage);
        }

        public static IEnumerable<int> GetServerSideErrors()
        {
            return Enumerable.Range(500, 100);
        }

        [Test]
        public void GIVEN_unsupportedComputeTypeErrorType_WHEN_constructor_THEN_hasDefaultNameAndMessage()
        {
            // Given / When
            GameLiftError error = new GameLiftError(GameLiftErrorType.UNSUPPORTED_COMPUTE_TYPE_EXCEPTION);

            // Then
            Assert.AreEqual(GameLiftErrorType.UNSUPPORTED_COMPUTE_TYPE_EXCEPTION, error.ErrorType);
            Assert.AreEqual("Unsupported compute type.", error.ErrorName);
            Assert.AreEqual("This API is not supported on the current compute type.", error.ErrorMessage);
        }
    }
}
