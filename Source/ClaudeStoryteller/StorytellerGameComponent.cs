using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ClaudeStoryteller
{
    public class StorytellerGameComponent : GameComponent
    {
        // Persistent arc log — survives save/load
        private List<ArcLogEntry> arcLog = new List<ArcLogEntry>();

        // Currently active arc tracking
        private string activeArcName;
        private List<string> activeArcEvents = new List<string>();
        private List<string> activeArcOutcomes = new List<string>();
        private int activeArcStartDay;
        private int activeArcTotalEvents;

        // Persistent event history
        private List<string> eventHistoryTypes = new List<string>();
        private List<string> eventHistoryOutcomes = new List<string>();
        private List<int> eventHistoryTicks = new List<int>();

        // Death/downed/threat tracking
        private int lastDeathTick = -999999;
        private int lastDownedTick = -999999;
        private int lastThreatTick = -999999;

        // Disease cooldown tracking
        private int lastDiseaseTick = -999999;

        // Arc completion tracking
        private int lastArcCompletedTick = -999999;

        // Storytelling posture (Claude's current blend)
        private string lastPosture = "";

        private const int MAX_EVENT_HISTORY = 30;
        private const int MAX_ARC_LOG = 50;

        public StorytellerGameComponent(Game game) { }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Collections.Look(ref arcLog, "arcLog", LookMode.Deep);
            Scribe_Values.Look(ref activeArcName, "activeArcName");
            Scribe_Collections.Look(ref activeArcEvents, "activeArcEvents", LookMode.Value);
            Scribe_Collections.Look(ref activeArcOutcomes, "activeArcOutcomes", LookMode.Value);
            Scribe_Values.Look(ref activeArcStartDay, "activeArcStartDay", 0);
            Scribe_Values.Look(ref activeArcTotalEvents, "activeArcTotalEvents", 0);

            Scribe_Collections.Look(ref eventHistoryTypes, "eventHistoryTypes", LookMode.Value);
            Scribe_Collections.Look(ref eventHistoryOutcomes, "eventHistoryOutcomes", LookMode.Value);
            Scribe_Collections.Look(ref eventHistoryTicks, "eventHistoryTicks", LookMode.Value);

            Scribe_Values.Look(ref lastDeathTick, "lastDeathTick", -999999);
            Scribe_Values.Look(ref lastDownedTick, "lastDownedTick", -999999);
            Scribe_Values.Look(ref lastThreatTick, "lastThreatTick", -999999);
            Scribe_Values.Look(ref lastDiseaseTick, "lastDiseaseTick", -999999);
            Scribe_Values.Look(ref lastArcCompletedTick, "lastArcCompletedTick", -999999);
            Scribe_Values.Look(ref lastPosture, "lastPosture", "");

            // Null safety after load
            if (arcLog == null) arcLog = new List<ArcLogEntry>();
            if (activeArcEvents == null) activeArcEvents = new List<string>();
            if (activeArcOutcomes == null) activeArcOutcomes = new List<string>();
            if (eventHistoryTypes == null) eventHistoryTypes = new List<string>();
            if (eventHistoryOutcomes == null) eventHistoryOutcomes = new List<string>();
            if (eventHistoryTicks == null) eventHistoryTicks = new List<int>();
            if (lastPosture == null) lastPosture = "";
        }

        // ========== Arc Lifecycle ==========

        /// <summary>
        /// Called when a new narrative arc is scheduled.
        /// </summary>
        public void StartArc(string arcName, int eventCount)
        {
            // If there was an active arc that never completed, log it as interrupted
            if (!string.IsNullOrEmpty(activeArcName))
            {
                FinalizeArc("interrupted");
            }

            activeArcName = arcName;
            activeArcEvents = new List<string>();
            activeArcOutcomes = new List<string>();
            activeArcStartDay = GenDate.DaysPassed;
            activeArcTotalEvents = eventCount;

            ClaudeLogger.LogEntry("ARC_START", $"Started arc: {arcName} with {eventCount} planned events");
        }

        /// <summary>
        /// Called when a narrative arc event fires (or fails).
        /// </summary>
        public void RecordArcEvent(string eventType, string outcome)
        {
            if (string.IsNullOrEmpty(activeArcName)) return;

            activeArcEvents.Add(eventType);
            activeArcOutcomes.Add(outcome);

            // Check if arc is complete
            if (activeArcEvents.Count >= activeArcTotalEvents)
            {
                FinalizeArc("completed");
            }
        }

        /// <summary>
        /// Finalize the current arc and add it to the persistent log.
        /// </summary>
        public void FinalizeArc(string outcome)
        {
            if (string.IsNullOrEmpty(activeArcName)) return;

            var entry = new ArcLogEntry
            {
                ArcName = activeArcName,
                Events = new List<string>(activeArcEvents),
                EventOutcomes = new List<string>(activeArcOutcomes),
                Outcome = outcome,
                StartDay = activeArcStartDay,
                EndDay = GenDate.DaysPassed,
                ColonistCount = PawnsFinder.AllMaps_FreeColonists.Count(),
                Wealth = Find.CurrentMap?.wealthWatcher?.WealthTotal ?? 0f,
                Phase = ColonyStateCollector.GetCurrentPhase(),
                NarrativeState = ColonyStateCollector.GetCurrentNarrativeState()
            };

            arcLog.Add(entry);

            // Cap the log
            while (arcLog.Count > MAX_ARC_LOG)
                arcLog.RemoveAt(0);

            // Record arc completion time
            lastArcCompletedTick = Find.TickManager.TicksGame;

            ClaudeLogger.LogEntry("ARC_COMPLETE",
                $"Arc '{activeArcName}' {outcome}. Events: {string.Join(", ", activeArcEvents)}. " +
                $"Total arcs logged: {arcLog.Count}"
            );

            // Clear active arc
            activeArcName = null;
            activeArcEvents = new List<string>();
            activeArcOutcomes = new List<string>();
            activeArcTotalEvents = 0;
        }

        // ========== Event History ==========

        public void RecordEvent(string type, string outcome)
        {
            eventHistoryTypes.Insert(0, type);
            eventHistoryOutcomes.Insert(0, outcome);
            eventHistoryTicks.Insert(0, Find.TickManager.TicksGame);

            while (eventHistoryTypes.Count > MAX_EVENT_HISTORY)
            {
                eventHistoryTypes.RemoveAt(eventHistoryTypes.Count - 1);
                eventHistoryOutcomes.RemoveAt(eventHistoryOutcomes.Count - 1);
                eventHistoryTicks.RemoveAt(eventHistoryTicks.Count - 1);
            }

            if (ColonyStateCollector.IsThreatEvent(type))
                lastThreatTick = Find.TickManager.TicksGame;
        }

        public void RecordColonistDeath() { lastDeathTick = Find.TickManager.TicksGame; }
        public void RecordColonistDowned() { lastDownedTick = Find.TickManager.TicksGame; }

        // ========== Disease Tracking ==========

        public void RecordDiseaseFired()
        {
            lastDiseaseTick = Find.TickManager.TicksGame;
            ClaudeLogger.LogEntry("DISEASE_COOLDOWN", $"Disease fired. Next disease blocked until day {GenDate.DaysPassed + 25}+");
        }

        public int DaysSinceLastDisease
        {
            get
            {
                if (lastDiseaseTick < 0) return 999;
                return (Find.TickManager.TicksGame - lastDiseaseTick) / GenDate.TicksPerDay;
            }
        }

        // ========== Arc Completion Tracking ==========

        public int DaysSinceArcCompleted
        {
            get
            {
                if (lastArcCompletedTick < 0) return 999;
                return (Find.TickManager.TicksGame - lastArcCompletedTick) / GenDate.TicksPerDay;
            }
        }

        public int ActiveArcEventsRemaining
        {
            get
            {
                if (string.IsNullOrEmpty(activeArcName)) return 0;
                return System.Math.Max(0, activeArcTotalEvents - activeArcEvents.Count);
            }
        }

        // ========== Posture ==========

        public string LastPosture
        {
            get => lastPosture ?? "";
            set => lastPosture = value ?? "";
        }

        // ========== Accessors ==========

        public List<ArcLogEntry> GetArcLog() => arcLog;
        public int ArcCount => arcLog.Count;
        public string ActiveArcName => activeArcName;
        public int LastDeathTick => lastDeathTick;
        public int LastDownedTick => lastDownedTick;
        public int LastThreatTick => lastThreatTick;

        public List<Models.PastEvent> GetRecentEvents(int count)
        {
            var result = new List<Models.PastEvent>();
            int currentTick = Find.TickManager.TicksGame;

            for (int i = 0; i < count && i < eventHistoryTypes.Count; i++)
            {
                result.Add(new Models.PastEvent
                {
                    Type = eventHistoryTypes[i],
                    DaysAgo = (currentTick - eventHistoryTicks[i]) / GenDate.TicksPerDay,
                    Outcome = eventHistoryOutcomes[i]
                });
            }

            return result;
        }

        public List<string> GetRecentEventTypes(int count)
        {
            return eventHistoryTypes.Take(count).ToList();
        }

        // ========== Static Accessor ==========

        public static StorytellerGameComponent Get()
        {
            return Current.Game?.GetComponent<StorytellerGameComponent>();
        }
    }
}
