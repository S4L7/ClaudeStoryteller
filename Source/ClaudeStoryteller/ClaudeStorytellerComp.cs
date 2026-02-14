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
        // Unified call timer
        private int lastUnifiedCallTick = 0;
        private int nextUnifiedIntervalTicks = 0;
        private bool unifiedCallInProgress = false;

        // Legacy timers kept for the settings sliders — Claude adjusts these via response
        private int lastMinorTick = 0;
        private int lastMajorTick = 0;
        private int lastNarrativeTick = 0;

        private static readonly object lockObj = new object();
        private bool initialized = false;

        private UnifiedResponse pendingUnifiedResponse = null;

        private DifficultyInfo cachedDifficulty = null;
        private int lastDifficultyCheckTick = 0;
        private const int DIFFICULTY_CHECK_INTERVAL = 2500;

        // ========== Call Timing ==========
        private const float UNIFIED_CALL_MIN_DAYS = 2f;
        private const float UNIFIED_CALL_MAX_DAYS = 7f;

        // Minimum hours between events within a narrative arc (just enough to not fire same tick)
        private const float ARC_EVENT_MIN_SPACING_HOURS = 2f;

        // Disease hard cooldown in days (enforced in ColonyStateCollector + here as safety net)
        private const int DISEASE_COOLDOWN_DAYS = 25;

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

        private static readonly HashSet<string> DiseaseEvents = new HashSet<string>
        {
            "Disease_Plague", "Disease_Flu", "Disease_Malaria", "Disease_GutWorms",
            "Disease_FibrousMechanites", "Disease_SensoryMechanites", "Disease_MuscleParasites"
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

            lastUnifiedCallTick = currentTick;
            lastMinorTick = currentTick;
            lastMajorTick = currentTick;
            lastNarrativeTick = currentTick;

            // Clear any stale state from previous game
            pendingUnifiedResponse = null;
            unifiedCallInProgress = false;
            cachedDifficulty = null;
            lastDifficultyCheckTick = 0;

            // Default: first unified call after 3 game days
            nextUnifiedIntervalTicks = (int)(3f * GenDate.TicksPerDay);

            initialized = true;
            ClaudeLogger.LogEntry("SCHEDULER", "Unified call system initialized. First call in ~3 game days.");
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

            // Detect new game or game load — tick reset means fresh state needed
            if (!initialized || currentTick < lastUnifiedCallTick)
            {
                InitializeTimers(currentTick);
            }

            // ========== Process pending unified response ==========
            UnifiedResponse unifiedResp = null;
            lock (lockObj)
            {
                if (pendingUnifiedResponse != null)
                {
                    unifiedResp = pendingUnifiedResponse;
                    pendingUnifiedResponse = null;
                }
            }

            if (unifiedResp != null)
            {
                foreach (var incident in ProcessUnifiedResponse(unifiedResp, target))
                    yield return incident;
            }

            // ========== Fire queued events ==========
            var readyEvents = EventQueue.PopReady(currentTick);
            foreach (var queued in readyEvents)
            {
                // Disease safety net — block even if it somehow got queued
                if (DiseaseEvents.Contains(ResolveEventName(queued.EventType)))
                {
                    if (!ColonyStateCollector.CanFireDisease())
                    {
                        ClaudeLogger.LogEventSkipped($"Disease blocked by cooldown: {queued.EventType}");
                        if (queued.SourceCycle == "narrative")
                        {
                            var comp = StorytellerGameComponent.Get();
                            comp?.RecordArcEvent(queued.EventType, "blocked_disease_cooldown");
                        }
                        continue;
                    }
                }

                var incident = ConvertQueuedToIncidentWithFallback(queued, target);
                if (incident != null)
                {
                    // Track narrative arc events in the GameComponent
                    if (queued.SourceCycle == "narrative")
                    {
                        var comp = StorytellerGameComponent.Get();
                        comp?.RecordArcEvent(queued.EventType, "fired");
                    }
                    yield return incident;
                }
                else if (queued.SourceCycle == "narrative")
                {
                    var comp = StorytellerGameComponent.Get();
                    comp?.RecordArcEvent(queued.EventType, "failed");
                }
            }

            // ========== Check unified timer ==========
            if (currentTick - lastUnifiedCallTick >= nextUnifiedIntervalTicks && !unifiedCallInProgress)
            {
                lastUnifiedCallTick = currentTick;
                StartUnifiedCall(map);
            }
        }

        // ========== Unified API Call ==========

        private async void StartUnifiedCall(Map map)
        {
            lock (lockObj) { if (unifiedCallInProgress) return; unifiedCallInProgress = true; }

            try
            {
                if (!ClaudeApiClient.CanMakeCall())
                {
                    ClaudeLogger.LogEventSkipped("Unified: Rate limited");
                    return;
                }

                var state = ColonyStateCollector.CollectState(map, "unified");
                if (state == null) { ClaudeLogger.LogApiError("Unified: CollectState returned null"); return; }

                var client = new ClaudeApiClient(ClaudeStorytellerMod.settings.ApiKey);
                var response = await client.GetUnifiedDecision(state);

                lock (lockObj)
                {
                    if (response != null)
                        pendingUnifiedResponse = response;
                }
            }
            catch (Exception ex) { ClaudeLogger.LogApiError("Unified call failed", ex.Message); }
            finally { lock (lockObj) { unifiedCallInProgress = false; } }
        }

        // ========== Process Unified Response ==========

        private IEnumerable<FiringIncident> ProcessUnifiedResponse(UnifiedResponse response, IIncidentTarget target)
        {
            // Apply posture
            if (response.Posture != null && !string.IsNullOrEmpty(response.Posture.CurrentBlend))
            {
                var comp = StorytellerGameComponent.Get();
                if (comp != null)
                {
                    comp.LastPosture = response.Posture.CurrentBlend;
                    ClaudeLogger.LogEntry("POSTURE",
                        $"Blend: {response.Posture.CurrentBlend}. " +
                        $"Reason: {response.Posture.Reasoning}. " +
                        $"Next hint: {response.Posture.NextPostureHint}"
                    );
                }
            }

            // Set next unified call interval from Claude's response
            float nextCallDays = Math.Max(UNIFIED_CALL_MIN_DAYS,
                Math.Min(response.NextCallDays > 0 ? response.NextCallDays : 3f, UNIFIED_CALL_MAX_DAYS));

            // If we just started an arc with events, push the next call out
            if (response.Arc != null && response.Arc.Decision == "start_arc" &&
                response.Arc.Events != null && response.Arc.Events.Count > 0)
            {
                nextCallDays = Math.Max(nextCallDays, 5f);
            }

            nextUnifiedIntervalTicks = (int)(nextCallDays * GenDate.TicksPerDay);
            ClaudeLogger.LogEntry("SCHEDULER", $"Next unified call in {nextCallDays:F1} game days");

            // Process narrative arc
            if (response.Arc != null && response.Arc.Decision == "start_arc" &&
                response.Arc.Events != null && response.Arc.Events.Count > 0)
            {
                ProcessNarrativeArc(response.Arc);
            }

            // Process scattered events — the world being alive around the arc
            if (response.ScatteredEvents != null && response.ScatteredEvents.Count > 0)
            {
                ClaudeLogger.LogEntry("SCATTERED",
                    $"Queueing {response.ScatteredEvents.Count} scattered events across next {nextCallDays:F1} days"
                );

                foreach (var scattered in response.ScatteredEvents)
                {
                    if (string.IsNullOrEmpty(scattered.Type)) continue;

                    // Disease safety net
                    string resolvedType = ResolveEventName(scattered.Type);
                    if (DiseaseEvents.Contains(resolvedType) && !ColonyStateCollector.CanFireDisease())
                    {
                        ClaudeLogger.LogEventSkipped($"Scattered disease blocked by cooldown: {scattered.Type}");
                        continue;
                    }

                    if (scattered.DelayHours > 0)
                    {
                        var queued = new QueuedEvent
                        {
                            EventType = scattered.Type,
                            Subtype = scattered.Subtype,
                            Faction = scattered.Faction,
                            Intensity = scattered.Intensity,
                            SourceCycle = "scattered",
                            Note = scattered.Note
                        };
                        EventQueue.EnqueueDelayed(queued, scattered.DelayHours);
                    }
                    else
                    {
                        // Fire immediately
                        var incident = ConvertToIncidentWithFallback(
                            scattered.Type, scattered.Intensity, scattered.Faction,
                            scattered.Subtype, target, "scattered"
                        );
                        if (incident != null) yield return incident;
                    }
                }
            }

            // Log overall reasoning
            if (!string.IsNullOrEmpty(response.Reasoning))
            {
                ClaudeLogger.LogEntry("UNIFIED_REASONING", response.Reasoning);
            }
        }

        private void ProcessNarrativeArc(NarrativeArcDecision arc)
        {
            ClaudeLogger.LogEntry("NARRATIVE_ARC",
                $"Scheduling arc: {arc.ArcName} with {arc.Events.Count} events\n{arc.Reasoning}"
            );

            EventQueue.ClearBySource("narrative");

            // Register the arc with the GameComponent for persistent tracking
            var comp = StorytellerGameComponent.Get();
            comp?.StartArc(arc.ArcName, arc.Events.Count);

            // Enforce minimum spacing between arc events
            float lastDelayHours = 0;
            bool hasDiseaseInArc = false;

            foreach (var arcEvent in arc.Events)
            {
                float delayHours = arcEvent.DelayHours;

                // Enforce minimum spacing from previous event
                if (delayHours < lastDelayHours + ARC_EVENT_MIN_SPACING_HOURS && lastDelayHours > 0)
                {
                    delayHours = lastDelayHours + ARC_EVENT_MIN_SPACING_HOURS;
                    ClaudeLogger.LogEntry("ARC_SPACING",
                        $"Bumped {arcEvent.Type} from {arcEvent.DelayHours}h to {delayHours}h (minimum {ARC_EVENT_MIN_SPACING_HOURS}h gap)"
                    );
                }

                // Block multiple diseases in same arc
                string resolvedType = ResolveEventName(arcEvent.Type);
                if (DiseaseEvents.Contains(resolvedType))
                {
                    if (hasDiseaseInArc)
                    {
                        ClaudeLogger.LogEntry("ARC_DISEASE_BLOCKED",
                            $"Blocked second disease in arc: {arcEvent.Type}. Skipping."
                        );
                        continue;
                    }

                    // Also check global disease cooldown
                    if (!ColonyStateCollector.CanFireDisease())
                    {
                        ClaudeLogger.LogEntry("ARC_DISEASE_BLOCKED",
                            $"Disease on cooldown: {arcEvent.Type}. Skipping."
                        );
                        continue;
                    }

                    hasDiseaseInArc = true;
                }

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

                EventQueue.EnqueueDelayed(queued, delayHours);
                lastDelayHours = delayHours;
            }
        }

        // ========== Event Conversion ==========

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

            // Disease safety net
            if (DiseaseEvents.Contains(resolvedType) && !ColonyStateCollector.CanFireDisease())
            {
                ClaudeLogger.LogEventSkipped($"Disease cooldown active, blocking: {type}");
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
