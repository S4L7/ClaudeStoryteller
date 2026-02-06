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
        private static List<PastEvent> eventHistory = new List<PastEvent>();
        private static int lastDeathTick = -999999;
        private static int lastDownedTick = -999999;
        private static int lastThreatTick = -999999;

        private static string lastArcName = null;
        private static string lastArcOutcome = null;
        private static int lastArcTick = -999999;

        public static void RecordEvent(string type, string outcome)
        {
            eventHistory.Insert(0, new PastEvent
            {
                Type = type,
                DaysAgo = 0,
                Outcome = outcome
            });

            if (eventHistory.Count > 15)
                eventHistory.RemoveAt(eventHistory.Count - 1);

            if (IsThreatEvent(type))
                lastThreatTick = Find.TickManager.TicksGame;
        }

        public static void RecordColonistDeath()
        {
            lastDeathTick = Find.TickManager.TicksGame;
        }

        public static void RecordColonistDowned()
        {
            lastDownedTick = Find.TickManager.TicksGame;
        }

        public static void RecordArcComplete(string arcName, string outcome)
        {
            lastArcName = arcName;
            lastArcOutcome = outcome;
            lastArcTick = Find.TickManager.TicksGame;
        }

        private static bool IsThreatEvent(string type)
        {
            return type.Contains("Raid") || type.Contains("Infestation") ||
                   type.Contains("Mech") || type.Contains("Manhunter");
        }

        public static ColonyState CollectState(Map map, string callType)
        {
            if (map == null) return null;

            var colonists = map.mapPawns.FreeColonists.ToList();
            var wealth = map.wealthWatcher.WealthTotal;
            var days = GenDate.DaysPassed;

            var state = new ColonyState
            {
                RequestId = Guid.NewGuid().ToString("N").Substring(0, 8),
                CallType = callType,
                Colony = CollectColonyInfo(map, colonists, wealth, days),
                RecentHistory = CollectRecentHistory(days),
                Cooldowns = CollectCooldowns(),
                AvailableFactions = CollectFactions(),
                DoNotRepeat = GetRecentEventTypes(),
                CurrentQueue = CollectQueueContext(),
                Difficulty = CollectDifficulty(),
                RandomSeed = Rand.Int
            };

            if (callType != "minor")
            {
                state.CombatReadiness = CollectCombatReadiness(map, colonists);
                state.Resources = CollectResources(map, colonists);
            }

            if (callType == "narrative")
            {
                state.LastArc = CollectNarrativeContext();
            }

            return state;
        }

        public static DifficultyInfo CollectDifficulty()
        {
            var diff = Find.Storyteller.difficulty;
            float threatScale = diff.threatScale;

            // Map RimWorld's difficulty index to labels and clamps
            // diff.difficulty: 0=Peaceful, 1=Community Builder, 2=Adventure Story,
            //                  3=Strive to Survive, 4=Blood and Dust, 5=Losing is Fun
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
                // Custom difficulty — derive from threat scale
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

        private static NarrativeContext CollectNarrativeContext()
        {
            if (lastArcName == null) return null;

            int daysSinceArc = (Find.TickManager.TicksGame - lastArcTick) / GenDate.TicksPerDay;
            return new NarrativeContext
            {
                ArcName = lastArcName,
                Outcome = lastArcOutcome ?? "unknown",
                DaysAgo = daysSinceArc
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
            int ticksSinceDeath = Find.TickManager.TicksGame - lastDeathTick;
            int daysSinceDeath = ticksSinceDeath / GenDate.TicksPerDay;

            float adaptation = Find.StoryWatcher.watcherAdaptation.AdaptDays;

            if (daysSinceDeath < 5) return "struggling";
            if (daysSinceDeath < 15 && adaptation < 30) return "recovering";
            if (adaptation > 80) return "snowballing";
            if (adaptation > 60) return "thriving";
            return "stable";
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

        private static RecentHistory CollectRecentHistory(int currentDay)
        {
            int daysSinceThreat = (Find.TickManager.TicksGame - lastThreatTick) / GenDate.TicksPerDay;
            int daysSinceDeath = (Find.TickManager.TicksGame - lastDeathTick) / GenDate.TicksPerDay;
            int daysSinceDowned = (Find.TickManager.TicksGame - lastDownedTick) / GenDate.TicksPerDay;

            return new RecentHistory
            {
                DaysSinceThreat = Math.Max(0, daysSinceThreat),
                DaysSinceColonistDeath = Math.Max(0, daysSinceDeath),
                DaysSinceColonistDowned = Math.Max(0, daysSinceDowned),
                LastEvents = eventHistory.Take(5).ToList()
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

        private static List<string> GetRecentEventTypes()
        {
            return eventHistory.Take(3).Select(e => e.Type).ToList();
        }
    }
}
