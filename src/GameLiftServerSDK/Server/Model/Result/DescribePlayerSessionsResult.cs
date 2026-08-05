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
using log4net;

namespace Aws.GameLift.Server.Model
{
    /// <summary>
    /// Provides a list of requested player session objects.
    /// </summary>
    public class DescribePlayerSessionsResult
    {
        private const int MaxPlayerSessions = 1024;

#if ENABLE_IL2CPP
        public static ILog Log { get; } = new UnityDebugLog();
#else
        public static ILog Log { get; } = LogManager.GetLogger(typeof(ServerState));
#endif

        public string NextToken { get; set; }

        public IList<PlayerSession> PlayerSessions { get; set; }

        public DescribePlayerSessionsResult(IList<PlayerSession> playerSessions, string nextToken)
        {
            PlayerSessions = playerSessions;
            NextToken = nextToken;
        }

        public void AddPlayerSession(PlayerSession value)
        {
            if (PlayerSessions.Count < MaxPlayerSessions)
            {
                PlayerSessions.Add(value);
            }
            else
            {
                Log.DebugFormat("PlayerSessions count is greater than or equal to max player sessions {0}.", MaxPlayerSessions);
            }
        }
    }
}
