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

namespace Aws.GameLift.Server.Common
{
    public abstract class GameLiftToolDetector
    {
        public abstract bool IsToolRunning();

        public abstract string GetToolName();

        public abstract string GetToolVersion();

        public void SetGameLiftTool()
        {
            if (!IsToolRunning())
            {
                return;
            }
            string existingToolName = Environment.GetEnvironmentVariable(GameLiftConstants.EnvironmentVariableSdkToolName);
            string existingToolVersion = Environment.GetEnvironmentVariable(GameLiftConstants.EnvironmentVariableSdkToolVersion);
            string toolName = GetToolName();
            string toolVersion = GetToolVersion();

            if (!string.IsNullOrEmpty(existingToolName))
            {
                if (existingToolVersion == null)
                {
                    existingToolVersion = string.Empty;
                }
                if (!existingToolName.Contains(toolName))
                {
                    toolName = existingToolName + "," + toolName;
                    toolVersion = existingToolVersion + "," + toolVersion;
                }
                else
                {
                    toolName = existingToolName;
                    toolVersion = existingToolVersion;
                }
            }
            Environment.SetEnvironmentVariable(GameLiftConstants.EnvironmentVariableSdkToolName, toolName);
            Environment.SetEnvironmentVariable(GameLiftConstants.EnvironmentVariableSdkToolVersion, toolVersion);
        }
    }
}
