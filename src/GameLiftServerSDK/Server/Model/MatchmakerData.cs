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
using Newtonsoft.Json.Linq;

namespace Aws.GameLift.Server.Model
{
    public enum BackfillMode
    {
        NOT_SET,
        MANUAL,
        AUTOMATIC
    }

    /// <summary>
    /// Data for each player that helps the FlexMatch matchmaker create balanced game sessions.
    /// </summary>
    public class MatchmakerData
    {
#if ENABLE_IL2CPP
        public static ILog Log { get; set; } = new UnityDebugLog();
#else
        public static ILog Log { get; set; } = LogManager.GetLogger(typeof(MatchmakerData));
#endif

        public string MatchId { get; set; }

        public string MatchmakingConfigurationArn { get; set; }

        public IList<Player> Players { get; } = new List<Player>();

        public string AutoBackfillTicketId { get; set; }

        public BackfillMode BackfillMode { get; set; }

        // Match fields
        private const string FieldMatchId = "matchId";
        private const string FieldMatchmakingConfigArn = "matchmakingConfigurationArn";
        private const string FieldTeams = "teams";
        private const string FieldAutoBackfillTicketId = "autoBackfillTicketId";
        private const string FieldBackfillMode = "autoBackfillMode";

        // Team fields
        private const string FieldName = "name";
        private const string FieldPlayers = "players";

        // Player fields
        private const string FieldPlayerId = "playerId";
        private const string FieldAttributes = "attributes";
        private const string FieldLatency = "attributes";

        // Attribute fields
        private const string FieldAttributeType = "attributeType";
        private const string FieldAttributeValue = "valueAttribute";

        public static MatchmakerData FromJson(string json)
        {
            JObject obj = JObject.Parse(json);

            MatchmakerData matchmakerData = new MatchmakerData();
            matchmakerData.MatchId = (string)obj[FieldMatchId];
            matchmakerData.MatchmakingConfigurationArn = (string)obj[FieldMatchmakingConfigArn];
            matchmakerData.AutoBackfillTicketId = (string)obj[FieldAutoBackfillTicketId];

            string backfillModeRawValue = (string)obj[FieldBackfillMode];
            if (!string.IsNullOrEmpty(backfillModeRawValue))
            {
                backfillModeRawValue = backfillModeRawValue.ToUpper();
                if (System.Enum.TryParse<BackfillMode>(backfillModeRawValue, out BackfillMode backfillMode))
                {
                    matchmakerData.BackfillMode = backfillMode;
                }
                else
                {
                    Log.WarnFormat("Unsupported backfill mode: '{0}'.", backfillModeRawValue);
                }
            }

            JArray parsedTeams = (JArray)obj[FieldTeams];
            foreach (var parsedTeam in parsedTeams.Children())
            {
                matchmakerData.ParseOutPlayersFromTeam((JObject)parsedTeam);
            }

            return matchmakerData;
        }

        public void ParseOutPlayersFromTeam(IDictionary<string, JToken> parsedTeam)
        {
            string teamName = (string)parsedTeam[FieldName];

            JArray parsedPlayers = (JArray)parsedTeam[FieldPlayers];
            foreach (var parsedPlayer in parsedPlayers.Children())
            {
                Players.Add(PlayerFromJson(teamName, (JObject)parsedPlayer));
            }
        }

        private static Player PlayerFromJson(string teamName, IDictionary<string, JToken> obj)
        {
            Player player = new Player();
            player.Team = teamName;
            player.PlayerId = (string)obj[FieldPlayerId];
            player.PlayerAttributes = ParsePlayerAttributes((JObject)obj[FieldAttributes]);
            player.LatencyInMS = ParseLatency((JObject)obj[FieldLatency]);

            return player;
        }

        private static Dictionary<string, AttributeValue> ParsePlayerAttributes(JObject parsedAttrs)
        {
            if (parsedAttrs == null)
            {
                return new Dictionary<string, AttributeValue>();
            }

            Dictionary<string, AttributeValue> attrs = new Dictionary<string, AttributeValue>();
            foreach (var oneAttr in parsedAttrs)
            {
                AttributeValue val = ParseOneAttribute((JObject)oneAttr.Value);
                if (val != null)
                {
                    attrs.Add(oneAttr.Key, val);
                }
            }

            return attrs;
        }

        private static AttributeValue ParseOneAttribute(IDictionary<string, JToken> parsedAttr)
        {
            string attributeType = (string)parsedAttr[FieldAttributeType];
            switch (attributeType)
            {
                case "DOUBLE":
                    return new AttributeValue((double)parsedAttr[FieldAttributeValue]);

                case "STRING":
                    return new AttributeValue((string)parsedAttr[FieldAttributeValue]);

                case "STRING_DOUBLE_MAP":
                    {
                        Dictionary<string, double> values = new Dictionary<string, double>();
                        foreach (var onePair in (JObject)parsedAttr[FieldAttributeValue])
                        {
                            values.Add(onePair.Key, (double)onePair.Value);
                        }

                        return new AttributeValue(values);
                    }

                case "STRING_LIST":
                    {
                        JArray listValues = (JArray)parsedAttr[FieldAttributeValue];
                        string[] values = new string[listValues.Count];

                        int x = 0;
                        foreach (var val in listValues.Children())
                        {
                            values[x] = (string)val;
                            x++;
                        }

                        return new AttributeValue(values);
                    }

                default:
                    return null;
            }
        }

        private static Dictionary<string, int> ParseLatency(JObject parsedLatency)
        {
            if (parsedLatency == null)
            {
                return new Dictionary<string, int>();
            }

            Dictionary<string, int> latency = new Dictionary<string, int>();
            // TODO: We currently don't include latency measurements in the matchmaker data.
            //       If we decide we want to we would need to add the parsing for that here.
            //
            // Reasons why we might want to avoid including it:
            //     (1) data could easily be out of date
            //     (2) the game server has been communicating regularly with all the players,
            //         so it's in a better position to know what the current latency is
            //     (3) including latency bulks up the size of the matchmaker data, which
            //         effectively reduces the maximum match size that can be supported
            //         before exceeding the maximum supported match size.

            return latency;
        }
    }
}
