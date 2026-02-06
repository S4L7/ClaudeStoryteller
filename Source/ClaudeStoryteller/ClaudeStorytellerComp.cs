using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using ClaudeStoryteller.Models;

namespace ClaudeStoryteller
{
    public class StorytellerCompProperties_Claude : StorytellerCompProperties
    {
        public StorytellerCompProperties_Claude()
        {
            compClass = typeof(StorytellerComp_Claude);
        }
    }

    public class StorytellerComp_Claude : StorytellerComp
    {
        private int lastMinorTick = 0;
        private int lastMajorTick = 0;
        private int lastNarrativeTick = 0;

        private int nextMinorIntervalTicks = 0;
        private int nextMajorIntervalTicks = 0;
        private int nextNarrativeIntervalTicks = 0;

        private bool minorCallInProgress = false;
        private bool majorCallInProgress = false;
        private bool narrativeCallInProgress = false;

        private static readonly object lockObj = new object();
        private bool initialized = false;

        private ClaudeResponse pendingMinorResponse = null;
        private ClaudeResponse pendingMajorResponse = null;
        private NarrativeArcResponse pendingNarrativeResponse = null;

        private DifficultyInfo cachedDifficulty = null;
        private int lastDifficultyCheckTick = 0;
        private const int DIFFICULTY_CHECK_INTERVAL = 2500;

        private static readonly HashSet<string> ThreatEvents = new HashSet<string>
        {
            "RaidEnemy", "Infestation", "MechCluster", "ManhunterPack",
            "Disease", "ToxicFallout", "PsychicDrone", "Defoliator",
            "ColdSnap", "HeatWave", "Flashstorm"
        };

        private static readonly HashSet<string> MajorThreatEvents = new HashSet<string>
        {
            "RaidEnemy", "Infestation", "MechCluster", "Defoliator"
        };

        private DifficultyInfo GetDifficulty()
        {
            int currentTick = Find.TickManager.TicksGame;
            if (cachedDifficulty == null || currentTick - lastDifficultyCheckTick > DIFFICULTY_CHECK_INTERVAL)
            {
                cachedDifficulty = ColonyStateCollector.CollectDifficulty();
                lastDifficultyCheckTick = currentTick;
            }
            return cachedDifficulty;
        }

        private void InitializeTimers(int currentTick)
        {
            ClaudeLogger.Initialize();

            lastMinorTick = currentTick;
            lastMajorTick = currentTick;
            lastNarrativeTick = currentTick;

            SetNextMinorInterval();
            SetNextMajorInterval();
            SetNextNarrativeInterval();

            initialized = true;
        }

        private void SetNextMinorInterval()
        {
            float hours = ClaudeStorytellerMod.settings.GetMinorInterval();
            nextMinorIntervalTicks = (int)(hours * GenDate.TicksPerHour);
            ClaudeLogger.LogEntry("SCHEDULER", $"Next minor event in {hours:F1} game hours");
        }

        private void SetNextMajorInterval()
        {
            float hours = ClaudeStorytellerMod.settings.GetMajorInterval();
            nextMajorIntervalTicks = (int)(hours * GenDate.TicksPerHour);
            ClaudeLogger.LogEntry("SCHEDULER", $"Next major event in {hours / 24f:F1} game days");
        }

        private void SetNextNarrativeInterval()
        {
            float hours = ClaudeStorytellerMod.settings.GetNarrativeInterval();
            nextNarrativeIntervalTicks = (int)(hours * GenDate.TicksPerHour);
            ClaudeLogger.LogEntry("SCHEDULER", $"Next narrative arc in {hours / 24f:F1} game days");
        }

        public override IEnumerable<FiringIncident> MakeIntervalIncidents(IIncidentTarget target)
        {
            if (!ClaudeStorytellerMod.settings.enabled)
                yield break;

            if (!ClaudeStorytellerMod.settings.HasApiKey)
                yield break;

            Map map = target as Map;
            if (map == null)
                yield break;

            int currentTick = Find.TickManager.TicksGame;

            if (!initialized)
                InitializeTimers(currentTick);

            // Process pending responses
            ClaudeResponse minorResp = null;
            lock (lockObj)
            {
                if (pendingMinorResponse != null)
                {
                    minorResp = pendingMinorResponse;
                    pendingMinorResponse = null;
                }
            }
            if (minorResp != null)
            {
                foreach (var incident in ProcessSingleResponse(minorResp, target, "minor"))
                    yield return incident;
            }

            ClaudeResponse majorResp = null;
            lock (lockObj)
            {
                if (pendingMajorResponse != null)
                {
                    majorResp = pendingMajorResponse;
                    pendingMajorResponse = null;
                }
            }
            if (majorResp != null)
            {
                foreach (var incident in ProcessSingleResponse(majorResp, target, "major"))
                    yield return incident;
            }

            NarrativeArcResponse narrativeResp = null;
            lock (lockObj)
            {
                if (pendingNarrativeResponse != null)
                {
                    narrativeResp = pendingNarrativeResponse;
                    pendingNarrativeResponse = null;
                }
            }
            if (narrativeResp != null)
            {
                ProcessNarrativeArc(narrativeResp);
            }

            // Fire queued events
            var readyEvents = EventQueue.PopReady(currentTick);
            foreach (var queued in readyEvents)
            {
                var incident = ConvertQueuedToIncident(queued, target);
                if (incident != null)
                {
                    ClaudeLogger.LogEventFired(
                        queued.EventType,
                        queued.Intensity,
                        queued.Faction,
                        queued.Subtype,
                        incident.parms?.points ?? 0
                    );
                    ColonyStateCollector.RecordEvent(queued.EventType, "fired");
                    yield return incident;
                }
                else
                {
                    ClaudeLogger.LogEventSkipped($"Queued event {queued.EventType} failed to convert");
                }
            }

            // Check timers and start API calls
            if (currentTick - lastMinorTick >= nextMinorIntervalTicks && !minorCallInProgress)
            {
                lastMinorTick = currentTick;
                SetNextMinorInterval();

                int narrativeQueued = 0;
                foreach (var q in EventQueue.PeekAll())
                {
                    if (q.SourceCycle == "narrative") narrativeQueued++;
                }

                if (narrativeQueued >= 2)
                {
                    ClaudeLogger.LogEventSkipped($"Minor cycle deferred: {narrativeQueued} narrative events pending");
                }
                else
                {
                    StartMinorCall(map);
                }
            }

            if (currentTick - lastMajorTick >= nextMajorIntervalTicks && !majorCallInProgress)
            {
                lastMajorTick = currentTick;
                SetNextMajorInterval();
                StartMajorCall(map);
            }

            if (currentTick - lastNarrativeTick >= nextNarrativeIntervalTicks && !narrativeCallInProgress)
            {
                lastNarrativeTick = currentTick;
                SetNextNarrativeInterval();
                StartNarrativeCall(map);
            }
        }

        private async void StartMinorCall(Map map)
        {
            lock (lockObj) { if (minorCallInProgress) return; minorCallInProgress = true; }

            try
            {
                if (!ClaudeApiClient.CanMakeCall())
                {
                    ClaudeLogger.LogEventSkipped("Minor: Rate limited");
                    return;
                }

                var state = ColonyStateCollector.CollectState(map, "minor");
                if (state == null) { ClaudeLogger.LogApiError("Minor: CollectState returned null"); return; }

                var client = new ClaudeApiClient(ClaudeStorytellerMod.settings.ApiKey);
                var response = await client.GetMinorDecision(state);

                lock (lockObj)
                {
                    if (response != null && response.Decision == "fire_event")
                        pendingMinorResponse = response;
                    else if (response != null)
                        ClaudeLogger.LogEventSkipped($"Minor: Claude chose {response.Decision}: {response.Reasoning}");

                    if (response?.AdjustTimers != null)
                        ClaudeStorytellerMod.settings.ApplyTimerAdjustment(response.AdjustTimers);
                }
            }
            catch (Exception ex) { ClaudeLogger.LogApiError("Minor call failed", ex.Message); }
            finally { lock (lockObj) { minorCallInProgress = false; } }
        }

        private async void StartMajorCall(Map map)
        {
            lock (lockObj) { if (majorCallInProgress) return; majorCallInProgress = true; }

            try
            {
                if (!ClaudeApiClient.CanMakeCall())
                {
                    ClaudeLogger.LogEventSkipped("Major: Rate limited");
                    return;
                }

                var state = ColonyStateCollector.CollectState(map, "major");
                if (state == null) { ClaudeLogger.LogApiError("Major: CollectState returned null"); return; }

                var client = new ClaudeApiClient(ClaudeStorytellerMod.settings.ApiKey);
                var response = await client.GetMajorDecision(state);

                lock (lockObj)
                {
                    if (response != null && response.Decision == "fire_event")
                        pendingMajorResponse = response;
                    else if (response != null && response.Decision == "send_help")
                        pendingMajorResponse = response;
                    else if (response != null)
                        ClaudeLogger.LogEventSkipped($"Major: Claude chose {response.Decision}: {response.Reasoning}");

                    if (response?.AdjustTimers != null)
                        ClaudeStorytellerMod.settings.ApplyTimerAdjustment(response.AdjustTimers);
                }
            }
            catch (Exception ex) { ClaudeLogger.LogApiError("Major call failed", ex.Message); }
            finally { lock (lockObj) { majorCallInProgress = false; } }
        }

        private async void StartNarrativeCall(Map map)
        {
            lock (lockObj) { if (narrativeCallInProgress) return; narrativeCallInProgress = true; }

            try
            {
                if (!ClaudeApiClient.CanMakeCall())
                {
                    ClaudeLogger.LogEventSkipped("Narrative: Rate limited");
                    return;
                }

                var state = ColonyStateCollector.CollectState(map, "narrative");
                if (state == null) { ClaudeLogger.LogApiError("Narrative: CollectState returned null"); return; }

                var client = new ClaudeApiClient(ClaudeStorytellerMod.settings.ApiKey);
                var response = await client.GetNarrativeArc(state);

                lock (lockObj)
                {
                    if (response != null && response.Events != null && response.Events.Count > 0)
                        pendingNarrativeResponse = response;
                    else if (response != null)
                        ClaudeLogger.LogEventSkipped($"Narrative: No events in arc: {response.Reasoning}");

                    if (response?.AdjustTimers != null)
                        ClaudeStorytellerMod.settings.ApplyTimerAdjustment(response.AdjustTimers);
                }
            }
            catch (Exception ex) { ClaudeLogger.LogApiError("Narrative call failed", ex.Message); }
            finally { lock (lockObj) { narrativeCallInProgress = false; } }
        }

        private IEnumerable<FiringIncident> ProcessSingleResponse(ClaudeResponse response, IIncidentTarget target, string source)
        {
            if (response?.Event == null) yield break;

            if (response.Event.DelayHours > 0)
            {
                var queued = new QueuedEvent
                {
                    EventType = response.Event.Type,
                    Category = response.Event.Category,
                    Subtype = response.Event.Subtype,
                    Faction = response.Event.Faction,
                    Intensity = response.Event.Intensity,
                    SourceCycle = source,
                    Note = response.Event.Note
                };
                EventQueue.EnqueueDelayed(queued, response.Event.DelayHours);
                yield break;
            }

            var incident = ConvertResponseToIncident(response, target);
            if (incident != null)
            {
                ClaudeLogger.LogEventFired(
                    response.Event.Type,
                    response.Event.Intensity,
                    response.Event.Faction,
                    response.Event.Subtype,
                    incident.parms?.points ?? 0
                );
                ColonyStateCollector.RecordEvent(response.Event.Type, "fired");
                yield return incident;
            }
            else
            {
                ClaudeLogger.LogEventSkipped($"{source}: ConvertResponseToIncident returned null for {response.Event.Type}");
            }
        }

        private void ProcessNarrativeArc(NarrativeArcResponse arc)
        {
            ClaudeLogger.LogEntry("NARRATIVE_ARC",
                $"Scheduling arc: {arc.ArcName} with {arc.Events.Count} events\n{arc.Reasoning}"
            );

            EventQueue.ClearBySource("narrative");

            foreach (var arcEvent in arc.Events)
            {
                var queued = new QueuedEvent
                {
                    EventType = arcEvent.Type,
                    Subtype = arcEvent.Subtype,
                    Faction = arcEvent.Faction,
                    Intensity = arcEvent.Intensity,
                    SourceCycle = "narrative",
                    ArcName = arc.ArcName,
                    Note = arcEvent.Note
                };

                EventQueue.EnqueueDelayed(queued, arcEvent.DelayHours);
            }
        }

        private FiringIncident ConvertResponseToIncident(ClaudeResponse response, IIncidentTarget target)
        {
            if (response?.Event == null) return null;

            return ConvertToIncident(
                response.Event.Type,
                response.Event.Intensity,
                response.Event.Faction,
                response.Event.Subtype,
                target
            );
        }

        private FiringIncident ConvertQueuedToIncident(QueuedEvent queued, IIncidentTarget target)
        {
            return ConvertToIncident(
                queued.EventType,
                queued.Intensity,
                queued.Faction,
                queued.Subtype,
                target
            );
        }

        private FiringIncident ConvertToIncident(string type, float intensity, string faction, string subtype, IIncidentTarget target)
        {
            var diff = GetDifficulty();

            if (!diff.AllowThreats && ThreatEvents.Contains(type))
            {
                ClaudeLogger.LogEventSkipped($"Difficulty [{diff.Label}] blocks threat: {type}");
                return null;
            }

            if (!diff.AllowMajorThreats && MajorThreatEvents.Contains(type))
            {
                ClaudeLogger.LogEventSkipped($"Difficulty [{diff.Label}] blocks major threat: {type}");
                return null;
            }

            float clampedIntensity = Math.Max(diff.MinIntensity, Math.Min(intensity, diff.MaxIntensity));
            if (Math.Abs(clampedIntensity - intensity) > 0.01f)
            {
                ClaudeLogger.LogEntry("DIFFICULTY_CLAMP",
                    $"{type}: intensity {intensity:F2} clamped to {clampedIntensity:F2} [{diff.Label}]"
                );
            }

            IncidentDef incidentDef = DefDatabase<IncidentDef>.GetNamedSilentFail(type);
            if (incidentDef == null)
            {
                ClaudeLogger.LogEventSkipped($"Unknown incident def: {type}");
                return null;
            }

            var parms = StorytellerUtility.DefaultParmsNow(incidentDef.category, target);
            parms.points *= clampedIntensity;

            if (!string.IsNullOrEmpty(faction))
            {
                Faction factionObj = FindFactionByType(faction);
                if (factionObj != null)
                    parms.faction = factionObj;
            }

            if (!string.IsNullOrEmpty(subtype) && type.Contains("Raid"))
            {
                var strategy = GetRaidStrategy(subtype);
                if (strategy != null)
                    parms.raidStrategy = strategy;
            }

            return new FiringIncident(incidentDef, this, parms);
        }

        private Faction FindFactionByType(string factionType)
        {
            foreach (var fac in Find.FactionManager.AllFactions)
            {
                if (!fac.HostileTo(Faction.OfPlayer)) continue;

                if (factionType == "Tribal" && fac.def.techLevel <= TechLevel.Neolithic)
                    return fac;
                if (factionType == "Pirate" && fac.def == FactionDefOf.Pirate)
                    return fac;
                if (factionType == "Mechanoid" && fac.def == FactionDefOf.Mechanoid)
                    return fac;
            }
            return null;
        }

        private RaidStrategyDef GetRaidStrategy(string subtype)
        {
            switch (subtype.ToLower())
            {
                case "sapper":
                    return DefDatabase<RaidStrategyDef>.GetNamedSilentFail("ImmediateAttackSappers");
                case "siege":
                    return DefDatabase<RaidStrategyDef>.GetNamedSilentFail("Siege");
                case "drop_pods":
                    return DefDatabase<RaidStrategyDef>.GetNamedSilentFail("ImmediateAttackSmart");
                case "assault":
                default:
                    return DefDatabase<RaidStrategyDef>.GetNamedSilentFail("ImmediateAttack");
            }
        }
    }
}
