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

namespace Aws.GameLift.Server
{
    /// <summary>
    /// Configuration parameters for initializing the Metrics.
    /// </summary>
    public sealed class MetricsParameters : IEquatable<MetricsParameters>
    {
        public string StatsdHost { get; set; }

        public int StatsdPort { get; set; }

        public string CrashReporterHost { get; set; }

        public int CrashReporterPort { get; set; }

        public int FlushIntervalMs { get; set; }

        public int MaxPacketSize { get; set; }

        public MetricsParameters(
            string statsdHost,
            int statsdPort,
            string crashReporterHost,
            int crashReporterPort,
            int flushIntervalMs,
            int maxPacketSize)
        {
            StatsdHost = statsdHost;
            StatsdPort = statsdPort;
            CrashReporterHost = crashReporterHost;
            CrashReporterPort = crashReporterPort;
            FlushIntervalMs = flushIntervalMs;
            MaxPacketSize = maxPacketSize;
        }

        public bool Equals(MetricsParameters other)
        {
            return other != null &&
                   StatsdHost == other.StatsdHost &&
                   StatsdPort == other.StatsdPort &&
                   CrashReporterHost == other.CrashReporterHost &&
                   CrashReporterPort == other.CrashReporterPort &&
                   FlushIntervalMs == other.FlushIntervalMs &&
                   MaxPacketSize == other.MaxPacketSize;
        }
    }
}
