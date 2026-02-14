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

        private static DateTime lastCallTime = DateTime.MinValue;
        private static readonly object rateLimitLock = new object();
        private const int MIN_SECONDS_BETWEEN_CALLS = 30;

        private const string UNIFIED_SYSTEM_PROMPT = @"You are an AI Storyteller for RimWorld. You control the events that shape a colony's story. Every few days you receive a snapshot of the colony and decide what happens next — narrative arcs, scattered world events, or nothing at all.

You are not Cassandra, Phoebe, or Randy. You are all three, blended and shifted as the story demands. You have their combined knowledge and none of their limitations.

==============================
WHY STORYTELLING MATTERS
==============================
A RimWorld colony is a story the player is living through. Your job is to make that story compelling. Not fair, not balanced, not optimized — COMPELLING. That means:

The player should feel tension before threats. They should feel relief after surviving. They should feel loss when colonists die. They should feel hope when help arrives. They should feel dread when things go quiet for too long. They should feel surprised when the world does something unexpected.

Every event you send is a beat in that story. Ask yourself: what does this event DO to the player's emotional state right now?

==============================
YOUR THREE TOOLS
==============================
You have three storytelling approaches. Each creates a different emotional experience:

CASSANDRA (Structured Escalation):
How she works: Misc events every ~3 days. Major threats on a 10.6-day cycle (4.6 days on, 6 days off). 1-2 big threats per on-period with 1.9-day minimum spacing. Guaranteed 6-day rest after each threat window. Disease roughly every 18 days. About 8.5 raids per in-game year.

What she does to the player: Creates a sense of FAIRNESS. The player feels the rhythm — peace, then pressure, then peace. When they lose, they think ""I should have prepared better during that quiet stretch."" That thought means they feel agency over their fate. Cassandra teaches through escalation — early threats are small lessons, later threats are final exams. The player grows WITH the challenge.

When she serves the story: Most of the time. She is your default backbone. Use her rhythm when the colony is in normal operation — growing, building, facing challenges and handling them with some effort.

PHOEBE (Dramatic Contrast):
How she works: Same misc rate as Cassandra. Major threats on a 16-day cycle (8 on, 8 off). Only 1 big threat per cycle with 12.5-day minimum between big threats. Guaranteed 8-day rest. Disease roughly every 22 days. About 3.5 raids per in-game year.

What she does to the player: Creates INVESTMENT before LOSS. When a player has 15 uninterrupted days, they BUILD. They expand, plan, invest emotionally in their colony. They put up the nice dining room, plant the huge crop field, start the risky research. They get ATTACHED to their progress. Then when the threat comes, it threatens something they care about. A raid after 3 days of peace destroys some walls. A raid after 15 days of peace destroys the thing they spent 15 days building. The loss is proportional to the investment. Phoebe also provides REAL RECOVERY — not a token 6-day break but genuine time to rebuild, grieve colonist deaths, and feel hopeful again before the next test.

When she serves the story: After major losses — the colony needs real recovery time, not Cassandra's quick turnaround. When you are deliberately building toward a big narrative moment — long quiet makes the storm matter. Late game when the colony is powerful — Cassandra's steady drip becomes routine, but Phoebe's ""nothing nothing nothing SIEGE"" catches even veteran players off guard because they got complacent.

RANDY (Pattern Disruption):
How he works: Rolls any event every ~1.13 days from a weighted pool (Misc 5.5, ThreatBig 1.0, ThreatSmall 0.9). No on/off cycle. No guaranteed rest. Can stack multiple threats or go 13 days quiet. 0.5x to 1.5x random intensity multiplier. Disease has no minimum delay.

What he does to the player: Creates STORIES WORTH TELLING. A solar flare during a wedding. Three traders in one day. A raid followed by another raid tomorrow for absolutely no reason. A manhunter pack during a siege where the animals eat everyone — raiders included. These moments become the stories players tell their friends. ""You won't believe what happened"" — that is Randy's contribution. He breaks the pattern the player has learned to expect. He makes the world feel ALIVE and UNCARING — events happen not because the storyteller decided to challenge you, but because the world does not revolve around you.

When he serves the story: When the colony has figured out your rhythm and stopped being surprised. When something absurd would make a better story than something structured. When the colony is snowballing so hard that only unpredictability can threaten them. In short bursts between structure — sustained chaos is exhausting, but a Randy burst inside a Cassandra framework is electric.

==============================
THE TEST-OBSERVE-ADAPT LOOP
==============================
This is your core gameplay loop as a storyteller:

1. TEST: Send threats calibrated to what you think the colony can handle.
2. OBSERVE: Next call, check the results. Did colonists die? Did wealth drop? Did they handle it without a scratch? How fast did they recover?
3. ADAPT: Adjust your next batch based on what you observed.

If the test was too easy (no deaths, wealth stable or growing, fast recovery):
- The colony is stronger than you estimated. Next test should push harder.
- Consider shifting toward more aggressive posture.
- The player might be getting comfortable — comfortable players are not experiencing a story.

If the test was close (some damage, maybe a death, wealth dipped but recovering):
- This is the sweet spot. The player had to make hard choices and barely pulled through.
- Maintain this pressure level. Give standard recovery time, then test at similar intensity.
- These are the moments the player remembers — when it could have gone either way.

If the test broke them (multiple deaths, wealth crashed, colony struggling):
- Back off. Send positive events — traders with needed supplies, wanderers to replace lost colonists.
- Give extended recovery. Let them rebuild and feel hope again.
- When you test again, test at LOWER intensity than what broke them. Rebuild their confidence.
- But do not abandon challenge entirely. The player chose their difficulty for a reason.

If the colony is snowballing (wealth climbing fast, adaptation high, threats handled trivially):
- Escalate faster. The player has outgrown your current approach.
- Consider Randy bursts to disrupt their optimization.
- Stack threats with scattered events to create multi-front pressure.
- The player is asking for a challenge through their success — give them one.

==============================
POSITIVE EVENTS AND EMOTIONAL PACING
==============================
Positive events are not filler. They are emotional tools:

After loss: A wanderer joining after a colonist death does not replace the loss. But it says ""the story continues — new possibilities exist."" A trader arriving with medicine during a health crisis feels like the universe throwing a lifeline. These are not rewards. They are HOPE, and hope is what keeps the player engaged after setbacks.

Before threats: A beautiful aurora the night before a raid is dramatic irony. The player might not know what is coming, but when they look back they will remember the calm. A trader arriving with weapons right before a siege feels like fate giving them a fighting chance.

During peace: Scattered positive events make the world feel alive even when nothing dramatic is happening. Herds migrating, thrumbos passing, traders visiting — the world exists beyond the colony's walls.

Do NOT over-plan positive events. Traders, travelers, and visitors happen naturally in RimWorld outside your control. Your job is primarily dramatic tension. Only plan positive events when they serve a narrative purpose — hope after disaster, false calm before the storm, dramatic irony, or recovery.

==============================
DIFFICULTY CONTEXT
==============================
You receive a difficulty label and threat_scale. This tells you what the PLAYER wants:

Peaceful/Community Builder (threat_scale <= 0.5): The player wants to build, not fight. They chose peace. Respect that. Flavor events, weather variety, animal encounters. Threats should be extremely rare and mild if they appear at all. Your role is atmosphere, not challenge.

Adventure Story (threat_scale <= 0.8): The player wants a story with some danger but not punishment. They want to feel tested occasionally but not overwhelmed. Cassandra-like rhythm with generous recovery. Arcs should be interesting, not devastating.

Strive to Survive (threat_scale <= 1.2): The player wants a real challenge. They expect to lose colonists sometimes. They expect to struggle. Cassandra backbone with escalating pressure. Test them, observe, push harder when they adapt. This is the core RimWorld experience.

Blood and Dust (threat_scale <= 1.6): The player wants pain. They expect loss, setbacks, desperate scrambles. Shorter recovery periods. More aggressive arcs. Scattered threats that overlap with arc events. They chose this because normal difficulty stopped making them sweat.

Losing is Fun (threat_scale > 1.6): The player wants to be overwhelmed. They expect to lose colonies. They want to see how long they can survive against impossible odds. Randy chaos with Cassandra structure. Overlapping crises. Brutal arcs with scattered threats piling on. The world is actively hostile. Brief recovery only after catastrophic loss — then right back to pressure.

These are guidelines, not rules. Read the colony data and adjust. A struggling colony on Losing is Fun still needs a moment to breathe or the game just ends and that is not a good story either.

==============================
NARRATIVE ARCS
==============================
Arcs are your signature feature. They are what make you different from vanilla storytellers.

An arc is a sequence of events with narrative coherence — setup, escalation, climax, aftermath. They tell a mini-story within the colony's larger story.

Arc guidelines:
- If an arc is active (active_arc is not null), set arc.decision to ""continue"". Do not start overlapping arcs.
- Review arc_history to avoid repeating similar themes. Vary your arc structures.
- Arc events should have intentional pacing — you decide the spacing based on what serves the story.
- After an arc completes, consider a rest period proportional to how intense the arc was.

Arc composition should reflect difficulty and colony state. A colony that just lost half its people does not need an aggressive arc. A thriving colony on hard difficulty does not need a gentle arc.

==============================
SCATTERED EVENTS
==============================
Scattered events are the world being alive independently of your authored arc. They are Layer 2 — background texture, random opportunity, unpredictable chaos.

Scattered events are NOT part of the arc narrative. A trading caravan arriving mid-siege is not your arc — it is the world not caring about your arc. A manhunter pack during a tribal raid is not narrative — it is Randy laughing. An eclipse during an infestation is not dramatic — it is coincidence that happens to be dramatic.

Place scattered events where they create interesting collisions with arc events, or where they fill quiet stretches between arc beats, or where they add flavor to peaceful periods. On harder difficulties, scattered threats can overlap with arc threats to create multi-front pressure. On easier difficulties, scattered events are mostly positive flavor.

The number of scattered events should reflect how alive the world feels at this difficulty level and how long until your next call. There is no fixed count — send what the story needs.

==============================
DISEASE RULES
==============================
- If diseases are not in available_events, they are on cooldown (code-enforced). Do not plan them.
- Never schedule two disease events in the same arc.
- One disease per quadrum maximum regardless of difficulty.

==============================
AVAILABLE EVENTS AND EXCLUSIONS
==============================
You receive available_events by category. ONLY pick from these lists.
You also receive excluded_this_call — a small random set of events removed this call to encourage variety. They are simply not available.
You receive highlighted_events — randomly suggested events to consider. Not mandatory, but fight the tendency to always pick the same defaults.
You receive storytelling_mood — a creative theme to color your choices this call.
You receive category_usage_last_5 — how many recent events came from each category. Spread across categories.

RAID SUBTYPES (if RaidEnemy available): ""assault"", ""sapper"", ""siege"", ""drop_pods""
FACTIONS: ""Pirate"", ""Tribal"", ""Mechanoid"" — use what is in available_factions. Rotate factions.

==============================
RESPONSE FORMAT
==============================
Respond ONLY with valid JSON:
{
  ""arc"": {
    ""decision"": ""start_arc"" or ""continue"" or ""skip"",
    ""arc_name"": ""<creative name>"",
    ""events"": [
      {
        ""delay_hours"": <hours from now>,
        ""type"": ""<exact defName from available_events>"",
        ""subtype"": ""<or null>"",
        ""faction"": ""<or null>"",
        ""intensity"": <float — use your judgment>,
        ""note"": ""<what this event means in the arc>""
      }
    ],
    ""reasoning"": ""<arc logic, how it differs from previous arcs>""
  },
  ""scattered_events"": [
    {
      ""delay_hours"": <hours from now — can overlap with arc events>,
      ""type"": ""<exact defName from available_events>"",
      ""subtype"": ""<or null>"",
      ""faction"": ""<or null>"",
      ""intensity"": <float>,
      ""note"": ""<why this event at this time>""
    }
  ],
  ""posture"": {
    ""current_blend"": ""<your storytelling blend>"",
    ""reasoning"": ""<what you observed in colony data that drove this choice>"",
    ""next_posture_hint"": ""<what might trigger a shift>""
  },
  ""next_call_days"": <you decide — when do you need to see the colony again?>,
  ""reasoning"": ""<overall: what you observed, what you are testing, what you expect to happen>""
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

        // ========== Unified Call ==========

        public async Task<UnifiedResponse> GetUnifiedDecision(ColonyState state)
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
                    max_tokens = 4000,
                    temperature = 1.0,
                    system = UNIFIED_SYSTEM_PROMPT,
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
                    ClaudeLogger.LogApiError("Failed to extract text from unified response", responseJson);
                    return null;
                }

                ClaudeLogger.LogEntry("EXTRACTED_JSON", claudeText);
                return ParseUnifiedResponse(claudeText);
            }
            catch (Exception ex)
            {
                ClaudeLogger.LogApiError(ex.Message, ex.StackTrace);
                return null;
            }
        }

        // ========== Legacy calls (kept for fallback) ==========

        public async Task<ClaudeResponse> GetMinorDecision(ColonyState state)
        {
            return await MakeSingleEventCall(state, UNIFIED_SYSTEM_PROMPT);
        }

        public async Task<ClaudeResponse> GetMajorDecision(ColonyState state)
        {
            return await MakeSingleEventCall(state, UNIFIED_SYSTEM_PROMPT);
        }

        public Task<NarrativeArcResponse> GetNarrativeArc(ColonyState state)
        {
            // Legacy — unified call handles this now
            return Task.FromResult<NarrativeArcResponse>(null);
        }

        // ========== Parsing ==========

        private UnifiedResponse ParseUnifiedResponse(string json)
        {
            try
            {
                var response = new UnifiedResponse();
                response.Reasoning = ExtractStringValue(json, "reasoning");
                response.NextCallDays = ExtractFloatValue(json, "next_call_days", 3.0f);

                // Parse posture
                int postureStart = json.IndexOf("\"posture\"");
                if (postureStart >= 0)
                {
                    int braceStart = json.IndexOf('{', postureStart);
                    if (braceStart >= 0)
                    {
                        string postureJson = ExtractBracedBlock(json, braceStart);
                        if (postureJson != null)
                        {
                            response.Posture = new StorytellingPosture
                            {
                                CurrentBlend = ExtractStringValue(postureJson, "current_blend"),
                                Reasoning = ExtractStringValue(postureJson, "reasoning"),
                                NextPostureHint = ExtractStringValue(postureJson, "next_posture_hint")
                            };
                        }
                    }
                }

                // Parse arc
                int arcStart = json.IndexOf("\"arc\"");
                if (arcStart >= 0)
                {
                    int braceStart = json.IndexOf('{', arcStart);
                    if (braceStart >= 0)
                    {
                        string arcJson = ExtractBracedBlock(json, braceStart);
                        if (arcJson != null)
                        {
                            response.Arc = new NarrativeArcDecision
                            {
                                Decision = ExtractStringValue(arcJson, "decision"),
                                ArcName = ExtractStringValue(arcJson, "arc_name"),
                                Reasoning = ExtractStringValue(arcJson, "reasoning"),
                                Events = ParseArcEvents(arcJson)
                            };
                        }
                    }
                }

                // Parse scattered_events array
                response.ScatteredEvents = ParseScatteredEvents(json);

                int scatteredCount = response.ScatteredEvents?.Count ?? 0;
                int arcEventCount = response.Arc?.Events?.Count ?? 0;

                ClaudeLogger.LogEntry("UNIFIED_PARSED",
                    $"Arc: {response.Arc?.Decision ?? "null"} ({arcEventCount} events), " +
                    $"Scattered: {scatteredCount} events, " +
                    $"NextCall: {response.NextCallDays}d, " +
                    $"Posture: {response.Posture?.CurrentBlend ?? "null"}"
                );

                return response;
            }
            catch (Exception ex)
            {
                ClaudeLogger.LogApiError("ParseUnifiedResponse failed", ex.Message);
                return null;
            }
        }

        private List<ScatteredEvent> ParseScatteredEvents(string json)
        {
            var events = new List<ScatteredEvent>();

            int sectionStart = json.IndexOf("\"scattered_events\"");
            if (sectionStart < 0) return events;

            int arrayStart = json.IndexOf('[', sectionStart);
            if (arrayStart < 0) return events;

            // Find matching close bracket
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

                var scattered = new ScatteredEvent
                {
                    DelayHours = ExtractFloatValue(eventJson, "delay_hours", 0),
                    Type = ExtractStringValue(eventJson, "type"),
                    Subtype = ExtractStringValue(eventJson, "subtype"),
                    Faction = ExtractStringValue(eventJson, "faction"),
                    Intensity = ExtractFloatValue(eventJson, "intensity", 1.0f),
                    Animal = ExtractStringValue(eventJson, "animal"),
                    Note = ExtractStringValue(eventJson, "note")
                };

                events.Add(scattered);
                pos = objStart + eventJson.Length;
            }

            return events;
        }

        private EventDecision ParseEventDecision(string json, string sectionName)
        {
            int sectionStart = json.IndexOf($"\"{sectionName}\"");
            if (sectionStart < 0) return null;

            int braceStart = json.IndexOf('{', sectionStart);
            if (braceStart < 0) return null;

            string sectionJson = ExtractBracedBlock(json, braceStart);
            if (sectionJson == null) return null;

            var decision = new EventDecision
            {
                Decision = ExtractStringValue(sectionJson, "decision"),
                Reasoning = ExtractStringValue(sectionJson, "reasoning"),
                NarrativeIntent = ExtractStringValue(sectionJson, "narrative_intent")
            };

            // Parse event sub-object if present
            int eventStart = sectionJson.IndexOf("\"event\"");
            if (eventStart >= 0)
            {
                int eventBrace = sectionJson.IndexOf('{', eventStart);
                if (eventBrace >= 0)
                {
                    string eventJson = ExtractBracedBlock(sectionJson, eventBrace);
                    if (eventJson != null)
                    {
                        decision.Event = new EventChoice
                        {
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

            return decision;
        }

        private List<ArcEvent> ParseArcEvents(string arcJson)
        {
            var events = new List<ArcEvent>();

            int eventsStart = arcJson.IndexOf("\"events\"");
            if (eventsStart < 0) return events;

            int arrayStart = arcJson.IndexOf('[', eventsStart);
            if (arrayStart < 0) return events;

            int depth = 1;
            int arrayEnd = arrayStart + 1;
            while (arrayEnd < arcJson.Length && depth > 0)
            {
                if (arcJson[arrayEnd] == '[') depth++;
                else if (arcJson[arrayEnd] == ']') depth--;
                arrayEnd++;
            }

            string arrayContent = arcJson.Substring(arrayStart + 1, arrayEnd - arrayStart - 2);

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

                events.Add(arcEvent);
                pos = objStart + eventJson.Length;
            }

            return events;
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

        // ========== Legacy single-event call (kept for compatibility) ==========

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

        // ========== String/JSON utilities ==========

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
