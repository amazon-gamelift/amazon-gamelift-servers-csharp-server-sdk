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
using System.Threading;
using Aws.GameLift.Server;
using NUnit.Framework;

namespace Aws.GameLift.Tests.Server
{
    [TestFixture]
    public class SocketConnectAttemptTest
    {
        private static readonly TimeSpan WaitTime = TimeSpan.FromSeconds(5);

        [Test]
        public void GIVEN_stalledConnection_WHEN_timeoutExpires_THEN_workerIsNotInterruptedAndLateConnectionIsClosed()
        {
            using (var started = new ManualResetEventSlim())
            using (var release = new ManualResetEventSlim())
            using (var closed = new ManualResetEventSlim())
            {
                var interrupted = false;
                var isBackground = false;
                var attempt = new GameLiftWebSocket.SocketConnectAttempt(() =>
                {
                    isBackground = Thread.CurrentThread.IsBackground;
                    started.Set();
                    try
                    {
                        release.Wait();
                    }
                    catch (ThreadInterruptedException)
                    {
                        interrupted = true;
                        throw;
                    }
                }, () => closed.Set());

                attempt.Start();
                try
                {
                    Assert.IsTrue(started.Wait(WaitTime));
                    Assert.IsFalse(attempt.Wait(TimeSpan.Zero));
                    Assert.IsTrue(attempt.IsAbandoned);
                    Assert.IsTrue(attempt.IsRunning);
                    Assert.IsTrue(isBackground);
                    Assert.IsFalse(closed.IsSet);
                }
                finally
                {
                    release.Set();
                    Assert.IsTrue(attempt.Wait(WaitTime), "Connection worker must finish after release");
                }

                Assert.IsFalse(interrupted);
                Assert.IsNull(attempt.Error);
                Assert.IsTrue(closed.IsSet);
            }
        }

        [Test]
        public void GIVEN_completedConnection_WHEN_waited_THEN_connectionRemainsOwnedByCaller()
        {
            var closed = false;
            var attempt = new GameLiftWebSocket.SocketConnectAttempt(() => { }, () => closed = true);
            attempt.Start();

            Assert.IsTrue(attempt.Wait(WaitTime));
            Assert.IsFalse(attempt.IsAbandoned);
            Assert.IsFalse(closed);
            Assert.IsNull(attempt.Error);
        }

        [Test]
        public void GIVEN_connectionThrows_WHEN_workerFinishes_THEN_exceptionIsCaptured()
        {
            var error = new InvalidOperationException("Handshake failed");
            var attempt = new GameLiftWebSocket.SocketConnectAttempt(() => throw error, () => { });
            attempt.Start();

            Assert.IsTrue(attempt.Wait(WaitTime));
            Assert.AreSame(error, attempt.Error);
        }

        [Test]
        public void GIVEN_pendingConnection_WHEN_abandoned_THEN_lateCompletionIsClosed()
        {
            using (var release = new ManualResetEventSlim())
            {
                var closed = false;
                var attempt = new GameLiftWebSocket.SocketConnectAttempt(() => release.Wait(), () => closed = true);
                attempt.Start();
                attempt.Abandon();
                release.Set();

                Assert.IsTrue(attempt.Wait(WaitTime));
                Assert.IsTrue(attempt.IsAbandoned);
                Assert.IsTrue(closed);
            }
        }

        [Test]
        public void GIVEN_abandonedConnection_WHEN_cleanupThrows_THEN_workerStillExits()
        {
            using (var release = new ManualResetEventSlim())
            {
                var attempt = new GameLiftWebSocket.SocketConnectAttempt(() => release.Wait(),
                    () => throw new InvalidOperationException("Close failed"));
                attempt.Start();
                attempt.Abandon();
                release.Set();

                Assert.IsTrue(attempt.Wait(WaitTime));
                Assert.IsFalse(attempt.IsRunning);
            }
        }
    }
}
