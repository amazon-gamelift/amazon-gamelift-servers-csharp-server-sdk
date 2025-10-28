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
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Moq.Protected;
using NUnit.Framework;

namespace Aws.GameLift.Server.Tests
{
    [TestFixture]
    public class CrashReporterClientTests
    {
        private CrashReporterClient CreateClient(HttpStatusCode statusCode, out Mock<HttpMessageHandler> handlerMock)
        {
            handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = statusCode,
                    ReasonPhrase = statusCode.ToString(),
                });

            var httpClient = new HttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri("http://localhost:1234")
            };
            return new CrashReporterClient(httpClient);
        }

        [Test]
        public void GIVEN_httpClient_WHEN_registerProcess_THEN_sendCorrectParameters()
        {
            var client = CreateClient(HttpStatusCode.OK, out var handlerMock);

            client.RegisterProcess();

            handlerMock.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri.ToString().Contains("register?process_pid=")
                ),
                ItExpr.IsAny<CancellationToken>()
            );
        }

        [Test]
        public void GIVEN_httpClient_WHEN_tagGameSession_THEN_sendCorrectParameters()
        {
            var client = CreateClient(HttpStatusCode.OK, out var handlerMock);
            string sessionId = "session-123";

            client.TagGameSession(sessionId);

            handlerMock.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri.ToString().Contains("update?process_pid=") &&
                    req.RequestUri.ToString().Contains($"session_id={sessionId}")
                ),
                ItExpr.IsAny<CancellationToken>()
            );
        }

        [Test]
        public void GIVEN_httpClient_WHEN_deregisterProcess_THEN_sendCorrectParameters()
        {
            var client = CreateClient(HttpStatusCode.OK, out var handlerMock);

            client.DeregisterProcess();

            handlerMock.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri.ToString().Contains("deregister?process_pid=")
                ),
                ItExpr.IsAny<CancellationToken>()
            );
        }

        [Test]
        public void GIVEN_httpFailure_WHEN_registerProcess_THEN_noExceptionThrown()
        {
            var client = CreateClient(HttpStatusCode.InternalServerError, out var handlerMock);

            Assert.DoesNotThrow(() => client.RegisterProcess());

            handlerMock.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            );
        }

        [Test]
        public void GIVEN_httpFailure_WHEN_tagGameSession_THEN_noExceptionThrown()
        {
            var client = CreateClient(HttpStatusCode.InternalServerError, out var handlerMock);
            string sessionId = "session-123";

            Assert.DoesNotThrow(() => client.TagGameSession(sessionId));

            handlerMock.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            );
        }

        [Test]
        public void GIVEN_httpFailure_WHEN_deregisterProcess_THEN_noExceptionThrown()
        {
            var client = CreateClient(HttpStatusCode.InternalServerError, out var handlerMock);

            Assert.DoesNotThrow(() => client.DeregisterProcess());

            handlerMock.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            );
        }
    }
}
