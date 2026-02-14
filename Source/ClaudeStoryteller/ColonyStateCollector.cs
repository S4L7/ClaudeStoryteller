using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using ClaudeStoryteller.Models;

namespace ClaudeStoryteller
{
    public static class ColonyStateCollector
    {
        // Event categories for Claude to understand what's available
        private static readonly HashSet<string> WeatherEvents = new HashSet<string>
        {
            "ColdSnap", "HeatWave", "ToxicFallout", "VolcanicWinter", "Flashstorm", "Eclipse", "SolarFlare", "Aurora"
        };

        private static readonly HashSet<string> ThreatEventSet = new HashSet<string>
        {
            "RaidEnemy", "Infestation", "MechCluster", "ManhunterPack", "DefoliatorShipPartCrash",
            "PsychicEmanatorShipPartCrash", "PsychicDrone"
        };

        private static readonly HashSet<string> PositiveEvents = new HashSet<string>
        {
            "TraderCaravanArrival", "OrbitalTraderArrival", "WandererJoin", "ResourcePodCrash",
            "RefugeePodCrash", "TravelerGroup", "VisitorGroup", "SelfTame", "FarmAnimalsWanderIn",
            "ThrumboPasses", "WildManWandersIn", "ShipChunkDrop", "GiveQuest"
        };

        private static readonly HashSet<string> DiseaseEvents = new HashSet<string>
        {
            "Disease_Plague", "Disease_Flu", "Disease_Malaria", "Disease_GutWorms",
            "Disease_FibrousMechanites", "Disease_SensoryMechanites", "Disease_MuscleParasites"
        };

        // All known events for underused detection
        private static readonly List<string> AllKnownEvents = new List<string>();

        static ColonyStateCollector()
        {
            AllKnownEvents.AddRange(WeatherEvents);
            AllKnownEvents.AddRange(ThreatEventSet);
            AllKnownEvents.AddRange(PositiveEvents);
            AllKnownEvents.AddRange(DiseaseEvents);
            AllKnownEvents.Add("HerdMigration");
            AllKnownEvents.Add("AmbrosiaSprout");
            AllKnownEvents.Add("CropBlight");
            AllKnownEvents.Add("ShortCircuit");
            AllKnownEvents.Add("Alphabeavers");
            AllKnownEvents.Add("AnimalInsanityMass");
        }

        // ========== Delegate to GameComponent ==========

        public static void RecordEvent(string type, string outcome)
        {
            var comp = StorytellerGameComponent.Get();
            comp?.RecordEvent(type, outcome);

            // Track disease separately for hard cooldown
            if (DiseaseEvents.Contains(type))
                comp?.RecordDiseaseFired();
        }

        public static void RecordColonistDeath()
        {
            var comp = StorytellerGameComponent.Get();
            comp?.RecordColonistDeath();
        }

        public static void RecordColonistDowned()
        {
            var comp = StorytellerGameComponent.Get();
            comp?.RecordColonistDowned();
        }

        public static bool IsWeatherEvent(string eventType)
        {
            return WeatherEvents.Contains(eventType);
        }

        public static bool IsThreatEvent(string eventType)
        {
            return ThreatEventSet.Contains(eventType);
        }

        public static bool IsDiseaseEvent(string eventType)
        {
            return DiseaseEvents.Contains(eventType);
        }

        /// <summary>
        /// Returns true if a disease event is allowed (cooldown has elapsed).
        /// Hard minimum of 25 days between any two disease events.
        /// </summary>
        public static bool CanFireDisease()
        {
            var comp = StorytellerGameComponent.Get();
            if (comp == null) return true;

            int daysSinceDisease = comp.DaysSinceLastDisease;
            return daysSinceDisease >= 25;
        }

        // ========== Phase/State (public for GameComponent access) ==========

        public static string GetCurrentPhase()
        {
            if (Find.CurrentMap == null) return "early";
            int days = GenDate.DaysPassed;
            float wealth = Find.CurrentMap.wealthWatcher.WealthTotal;
            int colonists = Find.CurrentMap.mapPawns.FreeColonists.Count();
            return DeterminePhase(days, wealth, colonists);
        }

        public static string GetCurrentNarrativeState()
        {
            if (Find.CurrentMap == null) return "stable";
            int colonists = Find.CurrentMap.mapPawns.FreeColonists.Count();
            return DetermineNarrativeState(GenDate.DaysPassed, colonists);
        }

        // ========== Available Events ==========

        public static List<string> GetAvailableEvents(Map map)
        {
            var available = new List<string>();

            var eventsToCheck = new List<string>(AllKnownEvents);

            foreach (var eventName in eventsToCheck)
            {
                var def = DefDatabase<IncidentDef>.GetNamedSilentFail(eventName);
                if (def == null) continue;

                try
                {
                    var parms = StorytellerUtility.DefaultParmsNow(def.category, map);
                    if (def.Worker.CanFireNow(parms))
                    {
                        available.Add(eventName);
                    }
                }
                catch
                {
                    // Skip events that error on CanFireNow check
                }
            }

            return available;
        }

        public static Dictionary<string, List<string>> GetAvailableEventsByCategory(Map map)
        {
            var available = GetAvailableEvents(map);
            var categorized = new Dictionary<string, List<string>>
            {
                { "weather", new List<string>() },
                { "threats", new List<string>() },
                { "positive", new List<string>() },
                { "disease", new List<string>() },
                { "other", new List<string>() }
            };

            foreach (var evt in available)
            {
                if (WeatherEvents.Contains(evt))
                    categorized["weather"].Add(evt);
                else if (ThreatEventSet.Contains(evt))
                    categorized["threats"].Add(evt);
                else if (PositiveEvents.Contains(evt))
                    categorized["positive"].Add(evt);
                else if (DiseaseEvents.Contains(evt))
                    categorized["disease"].Add(evt);
                else
                    categorized["other"].Add(evt);
            }

            // Remove diseases entirely if cooldown hasn't elapsed
            if (!CanFireDisease())
            {
                categorized["disease"].Clear();
            }

            // Shuffle each category to prevent LLM position bias
            var rng = new Random(GenTicks.TicksGame);
            foreach (var key in categorized.Keys.ToList())
            {
                var list = categorized[key];
                for (int i = list.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    var temp = list[i];
                    list[i] = list[j];
                    list[j] = temp;
                }
            }

            return categorized;
        }

        // ========== Variance: Highlighted Events ==========

        /// <summary>
        /// Picks 3-5 random events from the available pool and suggests them to Claude.
        /// Different highlights each call breaks the LLM's tendency to pick the same "safe" events.
        /// </summary>
        public static List<string> GenerateHighlightedEvents(Dictionary<string, List<string>> availableByCategory)
        {
            var allAvailable = new List<string>();
            foreach (var kvp in availableByCategory)
                allAvailable.AddRange(kvp.Value);

            if (allAvailable.Count == 0) return new List<string>();

            var rng = new Random(GenTicks.TicksGame + 7919); // different seed from shuffle
            int count = Math.Min(rng.Next(3, 6), allAvailable.Count);

            var highlighted = new List<string>();
            var pool = new List<string>(allAvailable);

            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                int idx = rng.Next(pool.Count);
                highlighted.Add(pool[idx]);
                pool.RemoveAt(idx);
            }

            return highlighted;
        }

        // ========== Variance: Random Exclusion List ==========

        /// <summary>
        /// Randomly removes 2-3 events from the available pool each call.
        /// Forces Claude to pick from a different subset every time.
        /// Returns the excluded event names so Claude knows they're unavailable.
        /// Also removes them from the availableByCategory dict in place.
        /// </summary>
        public static List<string> GenerateExclusionList(Dictionary<string, List<string>> availableByCategory)
        {
            var allAvailable = new List<string>();
            foreach (var kvp in availableByCategory)
                allAvailable.AddRange(kvp.Value);

            if (allAvailable.Count <= 5) return new List<string>(); // don't exclude if pool is tiny

            var rng = new Random(GenTicks.TicksGame + 4217);
            int count = rng.Next(2, 4); // 2-3 exclusions
            count = Math.Min(count, allAvailable.Count / 3); // never exclude more than 1/3 of pool

            var excluded = new List<string>();
            var pool = new List<string>(allAvailable);

            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                int idx = rng.Next(pool.Count);
                string evt = pool[idx];
                excluded.Add(evt);
                pool.RemoveAt(idx);

                // Remove from the actual category dict
                foreach (var kvp in availableByCategory)
                {
                    kvp.Value.Remove(evt);
                }
            }

            return excluded;
        }

        // ========== Variance: Storytelling Mood ==========

        private static readonly string[] MoodThemes = new string[]
        {
            "isolation and the wild frontier",
            "calm before the storm",
            "nature's indifference to human ambition",
            "the fragility of civilization",
            "unexpected visitors and strange omens",
            "scarcity and resourcefulness",
            "hubris and overconfidence punished",
            "the bonds forged in hardship",
            "creeping dread from the horizon",
            "a season of strange weather",
            "the land itself turns hostile",
            "false security shattered",
            "whispers of distant threats",
            "abundance attracting predators",
            "mechanical menace in the deep",
            "pestilence carried on the wind",
            "the kindness of strangers",
            "fire and ruin",
            "a test of the colony's resolve",
            "the rhythm of survival breaking down",
            "paranoia after a long peace",
            "the wilderness reclaiming what was built",
            "desperate measures in desperate times",
            "an eerie quiet that begs to be broken"
        };

        /// <summary>
        /// Returns a random storytelling mood phrase to shift Claude's creative framing each call.
        /// </summary>
        public static string GenerateStorytellingMood()
        {
            var rng = new Random(GenTicks.TicksGame + 6271);
            return MoodThemes[rng.Next(MoodThemes.Length)];
        }

        // ========== Variance: Category Usage Tracking ==========

        /// <summary>
        /// Counts how many of the last N events came from each category.
        /// Tells Claude what it's been over/under-using.
        /// </summary>
        public static Dictionary<string, int> CollectCategoryUsage(StorytellerGameComponent comp, int count)
        {
            var usage = new Dictionary<string, int>
            {
                { "weather", 0 },
                { "threats", 0 },
                { "positive", 0 },
                { "disease", 0 },
                { "other", 0 }
            };

            if (comp == null) return usage;

            var recent = comp.GetRecentEventTypes(count);
            foreach (var evt in recent)
            {
                if (WeatherEvents.Contains(evt))
                    usage["weather"]++;
                else if (ThreatEventSet.Contains(evt))
                    usage["threats"]++;
                else if (PositiveEvents.Contains(evt))
                    usage["positive"]++;
                else if (DiseaseEvents.Contains(evt))
                    usage["disease"]++;
                else
                    usage["other"]++;
            }

            return usage;
        }

        // ========== Vanilla Reference (static, never changes) ==========

        private static VanillaReference _vanillaRef = null;

        public static VanillaReference GetVanillaReference()
        {
            if (_vanillaRef != null) return _vanillaRef;

            _vanillaRef = new VanillaReference
            {
                Cassandra = new VanillaStoryteller
                {
                    Style = "structured tension curve with on/off cycles",
                    MiscMtbDays = 3.0f,
                    ThreatCycleDays = 10.6f,
                    ThreatsPerCycle = "1-2",
                    RestPeriodDays = 6.0f,
                    MinThreatSpacingDays = 1.9f,
                    DiseaseApproxMtbDays = 18.0f
                },
                Phoebe = new VanillaStoryteller
                {
                    Style = "long peace periods with hard singular hits",
                    MiscMtbDays = 3.0f,
                    ThreatCycleDays = 16.0f,
                    ThreatsPerCycle = "1",
                    RestPeriodDays = 8.0f,
                    MinThreatSpacingDays = 12.5f,
                    DiseaseApproxMtbDays = 22.0f
                },
                Randy = new VanillaStoryteller
                {
                    Style = "pure weighted chaos, no schedule, no guaranteed rest",
                    MiscMtbDays = 1.13f,
                    ThreatCycleDays = 11.0f,
                    ThreatsPerCycle = "random",
                    RestPeriodDays = 0f,
                    MinThreatSpacingDays = 0f,
                    DiseaseApproxMtbDays = 15.0f
                }
            };

            return _vanillaRef;
        }

        // ========== Event Density ==========

        public static EventDensity CollectDensity(StorytellerGameComponent comp)
        {
            if (comp == null)
            {
                return new EventDensity
                {
                    EventsLast7Days = 0,
                    EventsLast15Days = 0,
                    ThreatsLast7Days = 0,
                    ThreatsLast15Days = 0,
                    DiseasesLast30Days = 0,
                    DaysSinceLastDisease = 999,
                    DaysSinceArcCompleted = 999,
                    ActiveArc = null,
                    ActiveArcEventsRemaining = 0
                };
            }

            var recentAll = comp.GetRecentEvents(30);
            int currentTick = Find.TickManager.TicksGame;

            int events7 = 0, events15 = 0;
            int threats7 = 0, threats15 = 0;
            int diseases30 = 0;

            foreach (var evt in recentAll)
            {
                if (evt.DaysAgo <= 7)
                {
                    events7++;
                    if (ThreatEventSet.Contains(evt.Type)) threats7++;
                }
                if (evt.DaysAgo <= 15)
                {
                    events15++;
                    if (ThreatEventSet.Contains(evt.Type)) threats15++;
                }
                if (evt.DaysAgo <= 30 && DiseaseEvents.Contains(evt.Type))
                {
                    diseases30++;
                }
            }

            return new EventDensity
            {
                EventsLast7Days = events7,
                EventsLast15Days = events15,
                ThreatsLast7Days = threats7,
                ThreatsLast15Days = threats15,
                DiseasesLast30Days = diseases30,
                DaysSinceLastDisease = comp.DaysSinceLastDisease,
                DaysSinceArcCompleted = comp.DaysSinceArcCompleted,
                ActiveArc = comp.ActiveArcName,
                ActiveArcEventsRemaining = comp.ActiveArcEventsRemaining
            };
        }

        // ========== Main State Collection ==========

        public static ColonyState CollectState(Map map, string callType)
        {
            if (map == null) return null;

            var comp = StorytellerGameComponent.Get();
            var colonists = map.mapPawns.FreeColonists.ToList();
            var wealth = map.wealthWatcher.WealthTotal;
            var days = GenDate.DaysPassed;

            var availableEvents = GetAvailableEventsByCategory(map);

            var state = new ColonyState
            {
                RequestId = Guid.NewGuid().ToString("N").Substring(0, 8),
                CallType = callType,
                Colony = CollectColonyInfo(map, colonists, wealth, days),
                CombatReadiness = CollectCombatReadiness(map, colonists),
                Resources = CollectResources(map, colonists),
                RecentHistory = CollectRecentHistory(comp),
                Cooldowns = CollectCooldowns(),
                AvailableFactions = CollectFactions(),
                DoNotRepeat = comp?.GetRecentEventTypes(3) ?? new List<string>(),
                CurrentQueue = CollectQueueContext(),
                Difficulty = CollectDifficulty(),
                AvailableEvents = availableEvents,
                Density = CollectDensity(comp),
                LastPosture = comp?.LastPosture ?? "none — first call",
                HighlightedEvents = GenerateHighlightedEvents(availableEvents),
                ExcludedThisCall = GenerateExclusionList(availableEvents),
                StorytellingMood = GenerateStorytellingMood(),
                CategoryUsageLast5 = CollectCategoryUsage(comp, 5),
                RandomSeed = Rand.Int
            };

            // Always include arc history for unified calls
            var allAvailable = GetAvailableEvents(map);
            var arcLog = comp?.GetArcLog() ?? new List<ArcLogEntry>();
            state.ArcHistory = ArcSummarizer.Summarize(arcLog, allAvailable);

            return state;
        }

        public static DifficultyInfo CollectDifficulty()
        {
            var diff = Find.Storyteller.difficulty;
            float threatScale = diff.threatScale;

            float diffLevel = diff.threatScale;
            string label;
            float maxIntensity;
            float minIntensity;
            bool allowThreats;
            bool allowMajorThreats;

            if (diffLevel <= 0.1f)
            {
                label = "Peaceful";
                maxIntensity = 0f;
                minIntensity = 0f;
                allowThreats = false;
                allowMajorThreats = false;
            }
            else if (diffLevel <= 0.5f)
            {
                label = "Community Builder";
                maxIntensity = 0.6f;
                minIntensity = 0.2f;
                allowThreats = true;
                allowMajorThreats = false;
            }
            else if (diffLevel <= 0.8f)
            {
                label = "Adventure Story";
                maxIntensity = 1.0f;
                minIntensity = 0.3f;
                allowThreats = true;
                allowMajorThreats = true;
            }
            else if (diffLevel <= 1.2f)
            {
                label = "Strive to Survive";
                maxIntensity = 1.3f;
                minIntensity = 0.4f;
                allowThreats = true;
                allowMajorThreats = true;
            }
            else if (diffLevel <= 1.6f)
            {
                label = "Blood and Dust";
                maxIntensity = 1.5f;
                minIntensity = 0.5f;
                allowThreats = true;
                allowMajorThreats = true;
            }
            else if (diffLevel <= 2.0f)
            {
                label = "Losing is Fun";
                maxIntensity = 2.0f;
                minIntensity = 0.6f;
                allowThreats = true;
                allowMajorThreats = true;
            }
            else
            {
                label = "Custom";
                maxIntensity = Math.Max(0.5f, threatScale * 1.5f);
                minIntensity = Math.Max(0.2f, threatScale * 0.3f);
                allowThreats = threatScale > 0f;
                allowMajorThreats = threatScale > 0.3f;
            }

            return new DifficultyInfo
            {
                Label = label,
                ThreatScale = threatScale,
                MaxIntensity = maxIntensity,
                MinIntensity = minIntensity,
                AllowThreats = allowThreats,
                AllowMajorThreats = allowMajorThreats
            };
        }

        private static QueueContext CollectQueueContext()
        {
            return new QueueContext
            {
                PendingCount = EventQueue.Count,
                QueuedTypes = EventQueue.GetQueuedTypes(),
                QueueSummary = EventQueue.GetQueueSummary()
            };
        }

        private static ColonyInfo CollectColonyInfo(Map map, List<Pawn> colonists, float wealth, int days)
        {
            var phase = DeterminePhase(days, wealth, colonists.Count);
            var narrativeState = DetermineNarrativeState(days, colonists.Count);

            float raidPoints = StorytellerUtility.DefaultThreatPointsNow(map);
            float adaptation = Find.StoryWatcher.watcherAdaptation.AdaptDays;
            float threatScale = Find.Storyteller.difficulty.threatScale;

            return new ColonyInfo
            {
                Name = map.Parent.Label ?? "Colony",
                DaysSurvived = days,
                Phase = phase,
                NarrativeState = narrativeState,
                ColonistCount = colonists.Count,
                Wealth = wealth,
                RaidPoints = raidPoints,
                AdaptationScore = Math.Min(100, adaptation),
                ThreatScale = threatScale
            };
        }

        private static string DeterminePhase(int days, float wealth, int colonists)
        {
            if (days < 30 || wealth < 30000) return "early";
            if (days < 90 || wealth < 80000) return "establishing";
            if (days < 200 || wealth < 200000) return "mid-game";
            return "late-game";
        }

        private static string DetermineNarrativeState(int days, int colonists)
        {
            var comp = StorytellerGameComponent.Get();
            int lastDeathTick = comp?.LastDeathTick ?? -999999;

            int ticksSinceDeath = Find.TickManager.TicksGame - lastDeathTick;
            int daysSinceDeath = ticksSinceDeath / GenDate.TicksPerDay;

            float adaptation = Find.StoryWatcher.watcherAdaptation.AdaptDays;

            if (daysSinceDeath < 5) return "struggling";
            if (daysSinceDeath < 15 && adaptation < 30) return "recovering";
            if (adaptation > 80) return "snowballing";
            if (adaptation > 60) return "thriving";
            return "stable";
        }

        private static RecentHistory CollectRecentHistory(StorytellerGameComponent comp)
        {
            int lastThreatTick = comp?.LastThreatTick ?? -999999;
            int lastDeathTick = comp?.LastDeathTick ?? -999999;
            int lastDownedTick = comp?.LastDownedTick ?? -999999;

            int daysSinceThreat = (Find.TickManager.TicksGame - lastThreatTick) / GenDate.TicksPerDay;
            int daysSinceDeath = (Find.TickManager.TicksGame - lastDeathTick) / GenDate.TicksPerDay;
            int daysSinceDowned = (Find.TickManager.TicksGame - lastDownedTick) / GenDate.TicksPerDay;

            return new RecentHistory
            {
                DaysSinceThreat = Math.Max(0, daysSinceThreat),
                DaysSinceColonistDeath = Math.Max(0, daysSinceDeath),
                DaysSinceColonistDowned = Math.Max(0, daysSinceDowned),
                LastEvents = comp?.GetRecentEvents(5) ?? new List<PastEvent>()
            };
        }

        private static CombatReadiness CollectCombatReadiness(Map map, List<Pawn> colonists)
        {
            var defenses = new List<string>();
            var vulnerabilities = new List<string>();

            int turretCount = map.listerBuildings.AllBuildingsColonistOfClass<Building_Turret>().Count();
            if (turretCount > 5) defenses.Add("turrets");

            bool hasPerimeter = map.listerBuildings.allBuildingsColonist
                .Any(b => b.def.building != null && b.def.fillPercent >= 1f);
            if (hasPerimeter) defenses.Add("perimeter_wall");

            int trapCount = map.listerBuildings.allBuildingsColonist
                .Count(b => b.def.building != null && b.def.building.isTrap);
            if (trapCount > 10) defenses.Add("trap_corridor");

            float avgMelee = 0f;
            float avgRanged = 0f;
            if (colonists.Count > 0)
            {
                avgMelee = (float)colonists.Average(p => p.skills.GetSkill(SkillDefOf.Melee).Level);
                avgRanged = (float)colonists.Average(p => p.skills.GetSkill(SkillDefOf.Shooting).Level);
            }

            string meleeStr = avgMelee < 6 ? "low" : avgMelee < 12 ? "medium" : "high";
            string rangedStr = avgRanged < 6 ? "low" : avgRanged < 12 ? "medium" : "high";

            bool hasWood = map.listerBuildings.allBuildingsColonist
                .Any(b => b.Stuff != null && b.Stuff.IsStuff && b.Stuff.stuffProps.categories.Any(c => c.defName == "Woody"));
            if (hasWood) vulnerabilities.Add("wooden_structures");

            if (turretCount == 0) vulnerabilities.Add("no_turrets");

            bool hasEmp = colonists.Any(p => p.equipment?.Primary?.def?.Verbs != null &&
                p.equipment.Primary.def.Verbs.Any(v => v.defaultProjectile?.projectile?.damageDef == DamageDefOf.EMP));
            if (!hasEmp) vulnerabilities.Add("no_emp");

            float score = 0.5f;
            score += turretCount * 0.02f;
            score += trapCount * 0.01f;
            score += (avgMelee + avgRanged) / 40f * 0.2f;
            score = Math.Min(1f, Math.Max(0f, score));

            return new CombatReadiness
            {
                Score = score,
                MeleeStrength = meleeStr,
                RangedStrength = rangedStr,
                Defenses = defenses,
                Vulnerabilities = vulnerabilities
            };
        }

        private static Resources CollectResources(Map map, List<Pawn> colonists)
        {
            float totalNutrition = map.resourceCounter.GetCountIn(ThingRequestGroup.FoodSourceNotPlantOrTree) * 0.05f;
            float dailyNeed = colonists.Count * 1.6f;
            int foodDays = dailyNeed > 0 ? (int)(totalNutrition / dailyNeed) : 999;

            int medCount = map.resourceCounter.GetCount(ThingDefOf.MedicineIndustrial) +
                          map.resourceCounter.GetCount(ThingDefOf.MedicineHerbal) +
                          map.resourceCounter.GetCount(ThingDefOf.MedicineUltratech) * 2;
            string medicine = medCount < 5 ? "none" : medCount < 15 ? "low" : medCount < 40 ? "adequate" : "abundant";

            int compCount = map.resourceCounter.GetCount(ThingDefOf.ComponentIndustrial);
            string components = compCount < 5 ? "none" : compCount < 15 ? "low" : compCount < 40 ? "adequate" : "abundant";

            int silver = map.resourceCounter.GetCount(ThingDefOf.Silver);

            return new Resources
            {
                FoodDays = foodDays,
                Medicine = medicine,
                Components = components,
                Silver = silver
            };
        }

        private static Dictionary<string, int> CollectCooldowns()
        {
            var cooldowns = new Dictionary<string, int>();
            cooldowns["RaidEnemy"] = 0;
            cooldowns["Infestation"] = 0;
            cooldowns["MechCluster"] = 0;
            cooldowns["ManhunterPack"] = 0;
            cooldowns["ToxicFallout"] = 0;
            cooldowns["ColdSnap"] = 0;
            cooldowns["HeatWave"] = 0;
            return cooldowns;
        }

        private static List<string> CollectFactions()
        {
            var factions = new List<string>();

            foreach (var faction in Find.FactionManager.AllFactions)
            {
                if (faction.HostileTo(Faction.OfPlayer) && !faction.defeated)
                {
                    if (faction.def.techLevel <= TechLevel.Neolithic)
                        factions.Add("Tribal");
                    else if (faction.def == FactionDefOf.Mechanoid)
                        factions.Add("Mechanoid");
                    else if (faction.def == FactionDefOf.Pirate)
                        factions.Add("Pirate");
                    else
                        factions.Add(faction.Name);
                }
            }

            return factions.Distinct().ToList();
        }
    }
}
