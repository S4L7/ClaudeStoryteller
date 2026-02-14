using System.Collections.Generic;
using Verse;

namespace ClaudeStoryteller
{
    public class ArcLogEntry : IExposable
    {
        public string ArcName;
        public List<string> Events = new List<string>();
        public List<string> EventOutcomes = new List<string>(); // "fired", "skipped", "fallback"
        public string Outcome; // "completed", "partial", "interrupted"
        public int StartDay;
        public int EndDay;
        public int ColonistCount;
        public float Wealth;
        public string Phase;
        public string NarrativeState;

        public ArcLogEntry() { }

        public ArcLogEntry(string arcName, int startDay)
        {
            ArcName = arcName;
            StartDay = startDay;
            Events = new List<string>();
            EventOutcomes = new List<string>();
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref ArcName, "arcName", "");
            Scribe_Collections.Look(ref Events, "events", LookMode.Value);
            Scribe_Collections.Look(ref EventOutcomes, "eventOutcomes", LookMode.Value);
            Scribe_Values.Look(ref Outcome, "outcome", "unknown");
            Scribe_Values.Look(ref StartDay, "startDay", 0);
            Scribe_Values.Look(ref EndDay, "endDay", 0);
            Scribe_Values.Look(ref ColonistCount, "colonistCount", 0);
            Scribe_Values.Look(ref Wealth, "wealth", 0f);
            Scribe_Values.Look(ref Phase, "phase", "");
            Scribe_Values.Look(ref NarrativeState, "narrativeState", "");

            // Handle null lists after load
            if (Events == null) Events = new List<string>();
            if (EventOutcomes == null) EventOutcomes = new List<string>();
        }

        /// <summary>
        /// Returns the event sequence as a readable pattern like "TraderCaravanArrival → Disease_Plague → RaidEnemy"
        /// </summary>
        public string GetPattern()
        {
            if (Events == null || Events.Count == 0) return "empty";
            return string.Join(" → ", Events);
        }

        /// <summary>
        /// Returns just the first event (opener) of this arc
        /// </summary>
        public string GetOpener()
        {
            if (Events == null || Events.Count == 0) return null;
            return Events[0];
        }
    }
}
