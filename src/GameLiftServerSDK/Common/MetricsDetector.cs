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
using System.Diagnostics;

namespace Aws.GameLift.Server.Common
{
    public class MetricsDetector : GameLiftToolDetector
    {
        private const string ToolName = "Metrics";
        private const string ToolVersion = "1.0.0";
        private const string WindowsServiceCommand = "sc";
        private const string WindowsServiceName = "GLOTelCollector";
        private const string WindowsRunningStatus = "RUNNING";
        private const string LinuxServiceCommand = "systemctl";
        private const string LinuxServiceName = "gl-otel-collector.service";
        private const string LinuxActiveStatus = "active";

        private static readonly string WindowsServiceArgs = "query " + WindowsServiceName;
        private static readonly string LinuxServiceArgs = "is-active " + LinuxServiceName;

        public override bool IsToolRunning()
        {
            try
            {
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    return CheckService(WindowsServiceCommand, WindowsServiceArgs, output =>
                        output.Contains(WindowsRunningStatus));
                }
                else
                {
                    return CheckService(LinuxServiceCommand, LinuxServiceArgs, output =>
                        output.Trim() == LinuxActiveStatus);
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool CheckService(string command, string arguments, Func<string, bool> outputValidator)
        {
            using (var process = new Process())
            {
                process.StartInfo.FileName = command;
                process.StartInfo.Arguments = arguments;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.CreateNoWindow = true;

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                return process.ExitCode == 0 && outputValidator(output);
            }
        }

        public override string GetToolName()
        {
            return ToolName;
        }

        public override string GetToolVersion()
        {
            return ToolVersion;
        }
    }
}
