using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using ClaudeStoryteller.Models;
using Verse;

namespace ClaudeStoryteller
{
    public class ClaudeApiClient
    {
        private static readonly HttpClient client = new HttpClient();
        private readonly string apiKey;
        private const string API_URL = "https://api.anthropic.com/v1/messages";
        private const string MODEL = "claude-sonnet-4-20250514";

        // Rate limiting
        private static DateTime lastCallTime = DateTime.MinValue;
        private static readonly object rateLimitLock = new object();
        private const int MIN_SECONDS_BETWEEN_CALLS = 30;

        private const string MINOR_SYSTEM_PROMPT = @"You are a chaotic, creative AI Storyteller for RimWorld. This is a MINOR EVENT call. Your job is to keep the world feeling alive, unpredictable, and dangerous — not comfortable.

MINOR EVENTS (use exact defNames):
COMMON — use these most of the time:
- ManhunterPack: Animal attack. Pick interesting animals. Intensity 0.3-0.6.
- ColdSnap: Sudden freeze
- HeatWave: Scorching heat
- Flashstorm: Lightning storm — great near wooden bases
- ShipChunkDrop: Debris from space
- AmbrosiaSprout: Ambrosia appears (addictive, causes problems)
- Disease: Minor illness sweeping through

RARE — only when there's a good narrative reason:
- TraderCaravanArrival: Traders. MAX once every 5+ days. Don't default to this.
- WandererJoin: Free colonist. MAX once every 7+ days. More mouths to feed isn't always a gift.
- ResourcePodCrash: Supply drop. MAX once every 5+ days.
- TravelerGroup: Passers-by
- VisitorGroup: Visitors

RULES:
- NEVER default to traders or wanderers. They are boring. Be interesting.
- Bias toward weather, animals, and environmental chaos
- Check do_not_repeat and last_events — if you see traders or wanderers recently, DO NOT send more
- ""wait"" is a valid choice if nothing interesting fits
- Intensity 0.3-0.8
- delay_hours 0-6

You must respond ONLY with valid JSON. No markdown, no backticks, no explanation outside JSON:
{
  ""decision"": ""fire_event"" or ""wait"",
  ""event"": {
    ""type"": ""<exact defName>"",
    ""intensity"": <0.3 to 0.8>,
    ""delay_hours"": <0 to 6>,
    ""animal"": ""<animal defName or null>"",
    ""note"": ""<flavor text>""
  },
  ""reasoning"": ""<1-2 sentences>"",
  ""adjust_timers"": null
}";

        private const string MAJOR_SYSTEM_PROMPT = @"You are a ruthless, dramatic AI Storyteller for RimWorld. This is a MAJOR EVENT call. You exist to test colonies, create memorable disasters, and force desperate decisions.

COLONY PHASES:
- ""early"" (days 0-30, <30k wealth): Fragile but not immune. Small raids teach lessons. Don't coddle.
- ""establishing"" (days 30-90, 30-80k wealth): Hit their weak points. Sappers against walls, infestations in bedrooms.
- ""mid-game"" (days 90-200, 80-200k wealth): Gloves off. Combo threats, exploit every vulnerability.
- ""late-game"" (days 200+, 200k+ wealth): Overwhelming force. Multi-faction, drop pods, sieges.

NARRATIVE STATES:
- ""struggling"": They're bleeding. ONE more small push or let them almost recover then hit again.
- ""recovering"": They think they're safe. They're wrong. Light raid to keep paranoia alive.
- ""stable"": Stability is boring. Break something.
- ""thriving"": Punish complacency hard. They've had it too easy.
- ""snowballing"": Everything you've got. Back to back if needed.

MAJOR EVENTS (use exact defNames):
- RaidEnemy: Standard raid. Use subtypes aggressively.
- Infestation: Bug infestation. Devastating indoors.
- MechCluster: Mechanoid cluster. Forces engagement.
- ManhunterPack: Large animal attack (high intensity, pick dangerous animals)
- Disease: Plague sweeping through at the worst time
- ToxicFallout: Forces everyone inside, kills crops
- Defoliator: Defoliator ship part. Ticking clock.
- PsychicDrone: Mental breaks incoming

RAID SUBTYPES: ""assault"", ""sapper"", ""siege"", ""drop_pods""
Pick the subtype that exploits their specific weakness. No turrets? Assault. Strong killbox? Sappers or drop pods. Wooden base? Siege with fire.

RULES:
- Check current_queue — don't conflict but DO stack pressure when appropriate
- ""wait"" is valid but don't be a coward about it. Waiting should be strategic, not merciful.
- Exploit vulnerabilities listed in combat_readiness
- Intensity 0.5-1.5 based on how much they deserve it
- delay_hours 0-48
- send_help should be EXTREMELY rare — only when the colony is genuinely about to die

You must respond ONLY with valid JSON. No markdown, no backticks, no explanation outside JSON:
{
  ""decision"": ""fire_event"" or ""wait"" or ""send_help"",
  ""event"": {
    ""type"": ""<exact defName>"",
    ""subtype"": ""<raid subtype or null>"",
    ""faction"": ""Pirate"" or ""Tribal"" or ""Mechanoid"" or null,
    ""intensity"": <0.5 to 1.5>,
    ""delay_hours"": <0 to 48>,
    ""note"": ""<reasoning flavor>""
  },
  ""reasoning"": ""<1-2 sentences>"",
  ""narrative_intent"": ""test_defenses"" or ""exploit_weakness"" or ""reward_progress"" or ""punish_complacency"" or ""create_drama"" or ""provide_relief"",
  ""adjust_timers"": {
    ""minor_min_hours"": <number or 0 to skip>,
    ""minor_max_hours"": <number or 0>,
    ""major_min_days"": <number or 0>,
    ""major_max_days"": <number or 0>,
    ""narrative_min_days"": <number or 0>,
    ""narrative_max_days"": <number or 0>
  }
}";

        private const string NARRATIVE_SYSTEM_PROMPT = @"You are a dramatic, creative AI Storyteller for RimWorld. This is a NARRATIVE ARC call. Design a multi-event story that will be remembered. You're writing a saga, not a grocery list.

NARRATIVE ARCS should:
- Tell a coherent story with setup, escalation, climax, and consequences
- Use 3-7 events spread across hours/days
- Mix event types in ways that compound — weather during raids, disease after food loss, manhunters during a siege
- Have a creative theme (revenge arcs, ironic twists, escalating absurdity, tragic downfalls)
- Reference the colony's ACTUAL weaknesses and exploit them narratively
- Include moments of false hope followed by disaster
- Traders and wanderers are ONLY acceptable as narrative tools (the wanderer who brings plague, the trader who arrives right before the raid)

EVENT TYPES AVAILABLE:
- RaidEnemy, Infestation, MechCluster, ManhunterPack
- ColdSnap, HeatWave, ToxicFallout, Flashstorm
- TraderCaravanArrival, WandererJoin, ResourcePodCrash
- Disease, PsychicDrone, Defoliator
- ShipChunkDrop, AmbrosiaSprout

CHECK current_queue before planning — don't conflict with already-scheduled events.
CHECK last_arc for continuity — callbacks to previous arcs create long-running storylines.

You must respond ONLY with valid JSON. No markdown, no backticks, no explanation outside JSON:
{
  ""arc_name"": ""<creative name for this arc>"",
  ""events"": [
    {
      ""delay_hours"": <hours from now>,
      ""type"": ""<exact defName>"",
      ""subtype"": ""<raid subtype or null>"",
      ""faction"": ""<faction or null>"",
      ""intensity"": <0.3 to 1.5>,
      ""animal"": ""<animal defName or null>"",
      ""note"": ""<what this event means in the arc>""
    }
  ],
  ""reasoning"": ""<explain the arc's narrative logic>"",
  ""adjust_timers"": {
    ""minor_min_hours"": <number or 0>,
    ""minor_max_hours"": <number or 0>,
    ""major_min_days"": <number or 0>,
    ""major_max_days"": <number or 0>,
    ""narrative_min_days"": <number or 0>,
    ""narrative_max_days"": <number or 0>
  }
}";

        public ClaudeApiClient(string apiKey)
        {
            this.apiKey = apiKey;
        }

        public static bool CanMakeCall()
        {
            lock (rateLimitLock)
            {
                var elapsed = DateTime.Now - lastCallTime;
                return elapsed.TotalSeconds >= MIN_SECONDS_BETWEEN_CALLS;
            }
        }

        private static void RecordCallTime()
        {
            lock (rateLimitLock)
            {
                lastCallTime = DateTime.Now;
            }
        }

        public static async Task<string> TestConnection(string apiKey)
        {
            try
            {
                var testClient = new HttpClient();
                testClient.DefaultRequestHeaders.Clear();
                testClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
                testClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

                string requestBody = SimpleJson.Serialize(new
                {
                    model = MODEL,
                    max_tokens = 50,
                    messages = new[]
                    {
                        new { role = "user", content = "Reply with only: CONNECTION_OK" }
                    }
                });

                var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                var response = await testClient.PostAsync(API_URL, content);
                string responseJson = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    if (responseJson.Contains("CONNECTION_OK"))
                    {
                        return "Success! API key is valid.";
                    }
                    return "Connected but unexpected response.";
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return "Invalid API key.";
                }
                else
                {
                    return $"Error: HTTP {(int)response.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                return $"Connection failed: {ex.Message}";
            }
        }

        public async Task<ClaudeResponse> GetMinorDecision(ColonyState state)
        {
            return await MakeSingleEventCall(state, MINOR_SYSTEM_PROMPT);
        }

        public async Task<ClaudeResponse> GetMajorDecision(ColonyState state)
        {
            return await MakeSingleEventCall(state, MAJOR_SYSTEM_PROMPT);
        }

        public async Task<NarrativeArcResponse> GetNarrativeArc(ColonyState state)
        {
            if (!CanMakeCall()) return null;

            try
            {
                RecordCallTime();

                var requestClient = new HttpClient();
                requestClient.DefaultRequestHeaders.Clear();
                requestClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
                requestClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

                string stateJson = SimpleJson.Serialize(state);
                ClaudeLogger.LogStateRequest(stateJson);

                string requestBody = SimpleJson.Serialize(new
                {
                    model = MODEL,
                    max_tokens = 1500,
                    system = NARRATIVE_SYSTEM_PROMPT,
                    messages = new[]
                    {
                        new { role = "user", content = stateJson }
                    }
                });

                var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                var response = await requestClient.PostAsync(API_URL, content);
                string responseJson = await response.Content.ReadAsStringAsync();
                ClaudeLogger.LogRawResponse(responseJson);

                if (!response.IsSuccessStatusCode)
                {
                    ClaudeLogger.LogApiError($"HTTP {response.StatusCode}", responseJson);
                    return null;
                }

                string claudeText = ExtractTextContent(responseJson);
                if (string.IsNullOrEmpty(claudeText))
                {
                    ClaudeLogger.LogApiError("Failed to extract text from narrative response", responseJson);
                    return null;
                }

                ClaudeLogger.LogEntry("EXTRACTED_JSON", claudeText);
                return ParseNarrativeResponse(claudeText);
            }
            catch (Exception ex)
            {
                ClaudeLogger.LogApiError(ex.Message, ex.StackTrace);
                return null;
            }
        }

        private async Task<ClaudeResponse> MakeSingleEventCall(ColonyState state, string systemPrompt)
        {
            if (!CanMakeCall()) return null;

            try
            {
                RecordCallTime();

                var requestClient = new HttpClient();
                requestClient.DefaultRequestHeaders.Clear();
                requestClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
                requestClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

                string stateJson = SimpleJson.Serialize(state);
                ClaudeLogger.LogStateRequest(stateJson);

                string requestBody = SimpleJson.Serialize(new
                {
                    model = MODEL,
                    max_tokens = 500,
                    system = systemPrompt,
                    messages = new[]
                    {
                        new { role = "user", content = stateJson }
                    }
                });

                var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                var response = await requestClient.PostAsync(API_URL, content);
                string responseJson = await response.Content.ReadAsStringAsync();
                ClaudeLogger.LogRawResponse(responseJson);

                if (!response.IsSuccessStatusCode)
                {
                    ClaudeLogger.LogApiError($"HTTP {response.StatusCode}", responseJson);
                    return null;
                }

                string claudeText = ExtractTextContent(responseJson);
                if (string.IsNullOrEmpty(claudeText))
                {
                    ClaudeLogger.LogApiError("Failed to extract text from response", responseJson);
                    return null;
                }

                ClaudeLogger.LogEntry("EXTRACTED_JSON", claudeText);
                var parsed = ParseClaudeResponse(claudeText);

                ClaudeLogger.LogParsedDecision(
                    parsed?.Decision ?? "null",
                    parsed?.Event?.Type,
                    parsed?.Reasoning ?? "none",
                    parsed?.NarrativeIntent ?? "none"
                );

                return parsed;
            }
            catch (Exception ex)
            {
                ClaudeLogger.LogApiError(ex.Message, ex.StackTrace);
                return null;
            }
        }

        private ClaudeResponse ParseClaudeResponse(string json)
        {
            try
            {
                var response = new ClaudeResponse();
                response.Decision = ExtractStringValue(json, "decision");
                response.Reasoning = ExtractStringValue(json, "reasoning");
                response.NarrativeIntent = ExtractStringValue(json, "narrative_intent");
                response.AdjustTimers = ParseTimerAdjustment(json);

                int eventStart = json.IndexOf("\"event\"");
                if (eventStart >= 0)
                {
                    int braceStart = json.IndexOf('{', eventStart);
                    if (braceStart >= 0)
                    {
                        string eventJson = ExtractBracedBlock(json, braceStart);
                        if (eventJson != null)
                        {
                            response.Event = new EventChoice
                            {
                                Category = ExtractStringValue(eventJson, "category"),
                                Type = ExtractStringValue(eventJson, "type"),
                                Subtype = ExtractStringValue(eventJson, "subtype"),
                                Faction = ExtractStringValue(eventJson, "faction"),
                                Intensity = ExtractFloatValue(eventJson, "intensity", 1.0f),
                                DelayHours = ExtractIntValue(eventJson, "delay_hours", 0),
                                Animal = ExtractStringValue(eventJson, "animal"),
                                Note = ExtractStringValue(eventJson, "note")
                            };
                        }
                    }
                }

                return response;
            }
            catch (Exception ex)
            {
                ClaudeLogger.LogApiError("ParseClaudeResponse failed", ex.Message);
                return null;
            }
        }

        private NarrativeArcResponse ParseNarrativeResponse(string json)
        {
            try
            {
                var response = new NarrativeArcResponse();
                response.ArcName = ExtractStringValue(json, "arc_name");
                response.Reasoning = ExtractStringValue(json, "reasoning");
                response.AdjustTimers = ParseTimerAdjustment(json);
                response.Events = new List<ArcEvent>();

                int eventsStart = json.IndexOf("\"events\"");
                if (eventsStart < 0) return response;

                int arrayStart = json.IndexOf('[', eventsStart);
                if (arrayStart < 0) return response;

                int depth = 1;
                int arrayEnd = arrayStart + 1;
                while (arrayEnd < json.Length && depth > 0)
                {
                    if (json[arrayEnd] == '[') depth++;
                    else if (json[arrayEnd] == ']') depth--;
                    arrayEnd++;
                }

                string arrayContent = json.Substring(arrayStart + 1, arrayEnd - arrayStart - 2);

                int pos = 0;
                while (pos < arrayContent.Length)
                {
                    int objStart = arrayContent.IndexOf('{', pos);
                    if (objStart < 0) break;

                    string eventJson = ExtractBracedBlock(arrayContent, objStart);
                    if (eventJson == null) break;

                    var arcEvent = new ArcEvent
                    {
                        DelayHours = ExtractFloatValue(eventJson, "delay_hours", 0),
                        Type = ExtractStringValue(eventJson, "type"),
                        Subtype = ExtractStringValue(eventJson, "subtype"),
                        Faction = ExtractStringValue(eventJson, "faction"),
                        Intensity = ExtractFloatValue(eventJson, "intensity", 1.0f),
                        Animal = ExtractStringValue(eventJson, "animal"),
                        Note = ExtractStringValue(eventJson, "note")
                    };

                    response.Events.Add(arcEvent);
                    pos = objStart + eventJson.Length;
                }

                ClaudeLogger.LogEntry("NARRATIVE_PARSED",
                    $"Arc: {response.ArcName}, Events: {response.Events.Count}, Reasoning: {response.Reasoning}"
                );

                return response;
            }
            catch (Exception ex)
            {
                ClaudeLogger.LogApiError("ParseNarrativeResponse failed", ex.Message);
                return null;
            }
        }

        private TimerAdjustment ParseTimerAdjustment(string json)
        {
            int adjStart = json.IndexOf("\"adjust_timers\"");
            if (adjStart < 0) return null;

            int colonPos = json.IndexOf(':', adjStart);
            if (colonPos < 0) return null;
            string afterColon = json.Substring(colonPos + 1).TrimStart();
            if (afterColon.StartsWith("null")) return null;

            int braceStart = json.IndexOf('{', adjStart);
            if (braceStart < 0) return null;

            string adjJson = ExtractBracedBlock(json, braceStart);
            if (adjJson == null) return null;

            return new TimerAdjustment
            {
                MinorMinHours = ExtractFloatValue(adjJson, "minor_min_hours", 0),
                MinorMaxHours = ExtractFloatValue(adjJson, "minor_max_hours", 0),
                MajorMinDays = ExtractFloatValue(adjJson, "major_min_days", 0),
                MajorMaxDays = ExtractFloatValue(adjJson, "major_max_days", 0),
                NarrativeMinDays = ExtractFloatValue(adjJson, "narrative_min_days", 0),
                NarrativeMaxDays = ExtractFloatValue(adjJson, "narrative_max_days", 0)
            };
        }

        private string ExtractBracedBlock(string json, int braceStart)
        {
            int depth = 1;
            int braceEnd = braceStart + 1;
            bool inString = false;
            bool escaped = false;

            while (braceEnd < json.Length && depth > 0)
            {
                char c = json[braceEnd];
                if (escaped) { escaped = false; }
                else if (c == '\\') { escaped = true; }
                else if (c == '"') { inString = !inString; }
                else if (!inString)
                {
                    if (c == '{') depth++;
                    else if (c == '}') depth--;
                }
                braceEnd++;
            }

            if (depth != 0) return null;
            return json.Substring(braceStart, braceEnd - braceStart);
        }

        private string ExtractStringValue(string json, string key)
        {
            string pattern = $"\"{key}\":";
            int keyIndex = json.IndexOf(pattern);
            if (keyIndex < 0) return null;

            int valueStart = keyIndex + pattern.Length;
            while (valueStart < json.Length && char.IsWhiteSpace(json[valueStart]))
                valueStart++;

            if (valueStart >= json.Length) return null;
            if (json.Substring(valueStart).StartsWith("null")) return null;
            if (json[valueStart] != '"') return null;

            valueStart++;
            StringBuilder sb = new StringBuilder();
            bool esc = false;
            for (int i = valueStart; i < json.Length; i++)
            {
                char c = json[i];
                if (esc) { sb.Append(c); esc = false; }
                else if (c == '\\') { esc = true; }
                else if (c == '"') { break; }
                else { sb.Append(c); }
            }

            return sb.ToString();
        }

        private float ExtractFloatValue(string json, string key, float defaultValue)
        {
            string pattern = $"\"{key}\":";
            int keyIndex = json.IndexOf(pattern);
            if (keyIndex < 0) return defaultValue;

            int valueStart = keyIndex + pattern.Length;
            while (valueStart < json.Length && char.IsWhiteSpace(json[valueStart]))
                valueStart++;

            StringBuilder sb = new StringBuilder();
            for (int i = valueStart; i < json.Length; i++)
            {
                char c = json[i];
                if (char.IsDigit(c) || c == '.' || c == '-')
                    sb.Append(c);
                else
                    break;
            }

            if (float.TryParse(sb.ToString(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float result))
                return result;
            return defaultValue;
        }

        private int ExtractIntValue(string json, string key, int defaultValue)
        {
            return (int)ExtractFloatValue(json, key, defaultValue);
        }

        private string ExtractTextContent(string responseJson)
        {
            try
            {
                string marker = "\"text\":\"";
                int textStart = responseJson.IndexOf(marker);
                if (textStart < 0)
                {
                    marker = "\"text\": \"";
                    textStart = responseJson.IndexOf(marker);
                }

                if (textStart < 0) return null;
                textStart += marker.Length;

                StringBuilder sb = new StringBuilder();
                bool escaped = false;

                for (int i = textStart; i < responseJson.Length; i++)
                {
                    char c = responseJson[i];
                    if (escaped)
                    {
                        if (c == 'n') sb.Append('\n');
                        else if (c == 't') sb.Append('\t');
                        else if (c == 'r') sb.Append('\r');
                        else if (c == '"') sb.Append('"');
                        else if (c == '\\') sb.Append('\\');
                        else sb.Append(c);
                        escaped = false;
                    }
                    else if (c == '\\') { escaped = true; }
                    else if (c == '"') { break; }
                    else { sb.Append(c); }
                }

                string extracted = sb.ToString().Trim();
                int jsonStart = extracted.IndexOf('{');
                int jsonEnd = extracted.LastIndexOf('}');

                if (jsonStart >= 0 && jsonEnd > jsonStart)
                    return extracted.Substring(jsonStart, jsonEnd - jsonStart + 1);

                return extracted;
            }
            catch (Exception ex)
            {
                Log.Warning($"[ClaudeStoryteller] ExtractTextContent failed: {ex.Message}");
                return null;
            }
        }

        public static bool ValidateApiKey(string key)
        {
            return !string.IsNullOrEmpty(key) && key.StartsWith("sk-ant-");
        }
    }
}
