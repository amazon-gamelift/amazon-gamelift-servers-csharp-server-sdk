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

namespace Aws.GameLift.Server
{
    using System;
    using System.Net;

    /// <summary>
    /// Interface for a UDP client wrapper that abstracts the underlying UDP client functionality.
    /// </summary>
    public interface IUdpClientWrapper : IDisposable
    {
        /// <summary>
        /// Connects the UDP client to a remote endpoint.
        /// </summary>
        /// <param name="endPoint">The endpoint to connect to.</param>
        void Connect(IPEndPoint endPoint);

        /// <summary>
        /// Sends data via UDP.
        /// </summary>
        /// <param name="dgram">The data to send.</param>
        /// <param name="bytes">The number of bytes to send.</param>
        /// <returns>The number of bytes sent.</returns>
        int Send(byte[] dgram, int bytes);

        /// <summary>
        /// Closes the UDP client.
        /// </summary>
        void Close();
    }
}
