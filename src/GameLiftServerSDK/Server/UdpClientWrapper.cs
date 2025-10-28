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
    using System.Net.Sockets;

    /// <summary>
    /// Concrete implementation of IUdpClientWrapper that wraps the .NET UdpClient.
    /// </summary>
    public class UdpClientWrapper : IUdpClientWrapper
    {
        private readonly UdpClient udpClient;
        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="UdpClientWrapper"/> class.
        /// </summary>
        public UdpClientWrapper()
        {
            udpClient = new UdpClient();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UdpClientWrapper"/> class with an existing UdpClient.
        /// </summary>
        /// <param name="udpClient">The existing UdpClient to wrap.</param>
        public UdpClientWrapper(UdpClient udpClient)
        {
            this.udpClient = udpClient ?? throw new ArgumentNullException(nameof(udpClient));
        }

        /// <summary>
        /// Connects the UDP client to a remote endpoint.
        /// </summary>
        /// <param name="endPoint">The endpoint to connect to.</param>
        public void Connect(IPEndPoint endPoint)
        {
            udpClient.Connect(endPoint);
        }

        /// <summary>
        /// Sends data via UDP.
        /// </summary>
        /// <param name="dgram">The data to send.</param>
        /// <param name="bytes">The number of bytes to send.</param>
        /// <returns>The number of bytes sent.</returns>
        public int Send(byte[] dgram, int bytes)
        {
            return udpClient.Send(dgram, bytes);
        }

        /// <summary>
        /// Closes the UDP client.
        /// </summary>
        public void Close()
        {
            udpClient?.Close();
        }

        /// <summary>
        /// Disposes of the UDP client resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                udpClient?.Close();
                udpClient?.Dispose();
                disposed = true;
            }
        }
    }
}
