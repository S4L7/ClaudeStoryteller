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
            "Disease_Plague", "Disease_Flu", "Disease_Malaria", "Disease_GutWorms",
            "ToxicFallout", "PsychicDrone", "DefoliatorShipPartCrash",
            "ColdSnap", "HeatWave", "Flashstorm"
        };

        private static readonly HashSet<string> MajorThreatEvents = new HashSet<string>
        {
            "RaidEnemy", "Infestation", "MechCluster", "DefoliatorShipPartCrash"
        };

        // Fallback events when Claude's choice can't fire
        private static readonly List<string> MinorFallbacks = new List<string>
        {
            "ShipChunkDrop", "ResourcePodCrash", "WandererJoin", "TraderCaravanArrival",
            "VisitorGroup", "TravelerGroup", "OrbitalTraderArrival", "SelfTame"
        };

        private static readonly List<string> MajorFallbacks = new List<string>
        {
            "RaidEnemy", "TraderCaravanArrival", "ResourcePodCrash", "RefugeePodCrash",
            "WandererJoin", "TravelerGroup"
        };

        private static readonly Dictionary<string, string> EventNameMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Disease mappings
            {"Disease", "Disease_Plague"},
            {"Plague", "Disease_Plague"},
            {"Disease_Plague", "Disease_Plague"},
            {"Flu", "Disease_Flu"},
            {"Disease_Flu", "Disease_Flu"},
            {"Malaria", "Disease_Malaria"},
            {"Disease_Malaria", "Disease_Malaria"},
            {"GutWorms", "Disease_GutWorms"},
            {"Disease_GutWorms", "Disease_GutWorms"},
            {"MuscleParasites", "Disease_MuscleParasites"},
            {"Disease_MuscleParasites", "Disease_MuscleParasites"},
            {"FibrousMechanites", "Disease_FibrousMechanites"},
            {"Disease_FibrousMechanites", "Disease_FibrousMechanites"},
            {"SensoryMechanites", "Disease_SensoryMechanites"},
            {"Disease_SensoryMechanites", "Disease_SensoryMechanites"},
            
            // Raid mappings
            {"Raid", "RaidEnemy"},
            {"EnemyRaid", "RaidEnemy"},
            {"RaidEnemy", "RaidEnemy"},
            
            // Weather/Environment
            {"ToxicFallout", "ToxicFallout"},
            {"VolcanicWinter", "VolcanicWinter"},
            {"ColdSnap", "ColdSnap"},
            {"HeatWave", "HeatWave"},
            {"Flashstorm", "Flashstorm"},
            {"Eclipse", "Eclipse"},
            {"SolarFlare", "SolarFlare"},
            {"Aurora", "Aurora"},
            
            // Infestations
            {"Infestation", "Infestation"},
            {"DeepDrillInfestation", "DeepDrillInfestation"},
            
            // Positive events
            {"CargoDropPod", "ResourcePodCrash"},
            {"ResourcePod", "ResourcePodCrash"},
            {"ResourcePodCrash", "ResourcePodCrash"},
            {"ShipChunkDrop", "ShipChunkDrop"},
            {"WandererJoin", "WandererJoin"},
            {"WandererJoins", "WandererJoin"},
            {"Wanderer", "WandererJoin"},
            {"TraderArrival", "TraderCaravanArrival"},
            {"TraderCaravan", "TraderCaravanArrival"},
            {"TraderCaravanArrival", "TraderCaravanArrival"},
            {"Trader", "TraderCaravanArrival"},
            {"VisitorGroup", "VisitorGroup"},
            {"Visitors", "VisitorGroup"},
            {"TravelerGroup", "TravelerGroup"},
            {"Traveler", "TravelerGroup"},
            {"OrbitalTraderArrival", "OrbitalTraderArrival"},
            {"OrbitalTrader", "OrbitalTraderArrival"},
            
            // Animals
            {"ManhunterPack", "ManhunterPack"},
            {"Manhunter", "ManhunterPack"},
            {"ManhunterAmbush", "ManhunterPack"},
            {"AnimalInsanity", "AnimalInsanityMass"},
            {"AnimalInsanityMass", "AnimalInsanityMass"},
            {"AnimalInsanitySingle", "AnimalInsanitySingle"},
            {"HerdMigration", "HerdMigration"},
            {"Herd", "HerdMigration"},
            {"FarmAnimalsWanderIn", "FarmAnimalsWanderIn"},
            {"FarmAnimals", "FarmAnimalsWanderIn"},
            {"ThrumboPasses", "ThrumboPasses"},
            {"Thrumbo", "ThrumboPasses"},
            {"WildManWandersIn", "WildManWandersIn"},
            {"WildMan", "WildManWandersIn"},
            {"SelfTame", "SelfTame"},
            
            // Mechs
            {"MechCluster", "MechCluster"},
            {"MechanoidCluster", "MechCluster"},
            {"Mechanoid", "MechCluster"},
            
            // Ship parts
            {"Defoliator", "DefoliatorShipPartCrash"},
            {"DefoliatorShipPartCrash", "DefoliatorShipPartCrash"},
            {"DefoliatorShip", "DefoliatorShipPartCrash"},
            {"PsychicShip", "PsychicEmanatorShipPartCrash"},
            {"PsychicEmanator", "PsychicEmanatorShipPartCrash"},
            {"PsychicEmanatorShipPartCrash", "PsychicEmanatorShipPartCrash"},
            
            // Psychic
            {"PsychicDrone", "PsychicDrone"},
            {"PsychicSoothe", "PsychicSoothe"},
            
            // Misc threats
            {"ShortCircuit", "ShortCircuit"},
            {"CropBlight", "CropBlight"},
            {"Blight", "CropBlight"},
            {"Alphabeavers", "Alphabeavers"},
            {"Beavers", "Alphabeavers"},
            
            // Refugees/Pods
            {"RefugeePodCrash", "RefugeePodCrash"},
            {"RefugeePod", "RefugeePodCrash"},
            {"Refugee", "RefugeePodCrash"},
            {"TransportPodCrash", "RefugeePodCrash"},
            {"EscapeShuttleCrash", "RefugeePodCrash"},
            
            // Quests
            {"Quest", "GiveQuest"},
            {"QuestOffer", "GiveQuest"},
            {"GiveQuest", "GiveQuest"},
            
            // Party/Social
            {"Party", "Party"},
            {"Wedding", "Wedding"},
        };

        private static string ResolveEventName(string type)
        {
            if (string.IsNullOrEmpty(type)) return null;
            
            if (EventNameMapping.TryGetValue(type, out string mapped))
                return mapped;
            
            return type;
        }

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
                var incident = ConvertQueuedToIncidentWithFallback(queued, target);
                if (incident != null)
                {
                    yield return incident;
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

            var incident = ConvertToIncidentWithFallback(
                response.Event.Type,
                response.Event.Intensity,
                response.Event.Faction,
                response.Event.Subtype,
                target,
                source
            );
            
            if (incident != null)
            {
                yield return incident;
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

        private FiringIncident ConvertQueuedToIncidentWithFallback(QueuedEvent queued, IIncidentTarget target)
        {
            return ConvertToIncidentWithFallback(
                queued.EventType,
                queued.Intensity,
                queued.Faction,
                queued.Subtype,
                target,
                queued.SourceCycle
            );
        }

        private FiringIncident ConvertToIncidentWithFallback(string type, float intensity, string faction, string subtype, IIncidentTarget target, string source)
        {
            // Try the requested event first
            var incident = TryConvertToIncident(type, intensity, faction, subtype, target);
            if (incident != null)
            {
                ClaudeLogger.LogEventFired(type, intensity, faction, subtype, incident.parms?.points ?? 0);
                ColonyStateCollector.RecordEvent(type, "fired");
                return incident;
            }

            // Pick fallback list based on source
            List<string> fallbacks = (source == "major") ? MajorFallbacks : MinorFallbacks;
            
            ClaudeLogger.LogEntry("FALLBACK", $"Primary event {type} failed, trying fallbacks...");

            // Shuffle fallbacks for variety
            var shuffled = new List<string>(fallbacks);
            var rand = new Random();
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = rand.Next(i + 1);
                var temp = shuffled[i];
                shuffled[i] = shuffled[j];
                shuffled[j] = temp;
            }

            foreach (var fallbackType in shuffled)
            {
                incident = TryConvertToIncident(fallbackType, 1.0f, null, null, target);
                if (incident != null)
                {
                    ClaudeLogger.LogEntry("FALLBACK_FIRED", $"Fallback event {fallbackType} fired instead of {type}");
                    ColonyStateCollector.RecordEvent(fallbackType, "fallback");
                    return incident;
                }
            }

            ClaudeLogger.LogEventSkipped($"All fallbacks failed for {type}");
            return null;
        }

        private FiringIncident TryConvertToIncident(string type, float intensity, string faction, string subtype, IIncidentTarget target)
        {
            var diff = GetDifficulty();
            string resolvedType = ResolveEventName(type);
            
            if (!diff.AllowThreats && ThreatEvents.Contains(resolvedType))
            {
                ClaudeLogger.LogEventSkipped($"Difficulty [{diff.Label}] blocks threat: {type}");
                return null;
            }

            if (!diff.AllowMajorThreats && MajorThreatEvents.Contains(resolvedType))
            {
                ClaudeLogger.LogEventSkipped($"Difficulty [{diff.Label}] blocks major threat: {type}");
                return null;
            }

            float clampedIntensity = Math.Max(diff.MinIntensity, Math.Min(intensity, diff.MaxIntensity));

            IncidentDef incidentDef = DefDatabase<IncidentDef>.GetNamedSilentFail(resolvedType);
            if (incidentDef == null)
            {
                ClaudeLogger.LogEventSkipped($"Unknown incident def: {type} (resolved: {resolvedType})");
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

            if (!string.IsNullOrEmpty(subtype) && resolvedType.Contains("Raid"))
            {
                var strategy = GetRaidStrategy(subtype);
                if (strategy != null)
                    parms.raidStrategy = strategy;
            }

            // Check if the event can actually fire
            if (!incidentDef.Worker.CanFireNow(parms))
            {
                ClaudeLogger.LogEventSkipped($"CanFireNow false: {type} (resolved: {resolvedType})");
                return null;
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
