using System.Collections.Generic;
namespace ClaudeStoryteller.Models
{
    // ========== Unified Response (one API call handles everything) ==========

    public class UnifiedResponse
    {
        // Narrative arc (optional — only when Claude wants to start one)
        public NarrativeArcDecision Arc { get; set; }

        // Scattered events — the world happening around/during the arc
        // 0 to 15+ events, Claude decides how many based on difficulty and colony state
        public List<ScatteredEvent> ScatteredEvents { get; set; }

        // Timer adjustments (clamped by code before applying)
        public TimerAdjustment AdjustTimers { get; set; }

        // Claude's current storytelling style blend
        public StorytellingPosture Posture { get; set; }

        // How many days until the next unified call
        public float NextCallDays { get; set; }

        // Overall reasoning for this call's decisions
        public string Reasoning { get; set; }
    }

    public class ScatteredEvent
    {
        public ScatteredEvent()
        {
            Intensity = 1.0f;
            DelayHours = 0;
        }
        public float DelayHours { get; set; }
        public string Type { get; set; }
        public string Subtype { get; set; }
        public string Faction { get; set; }
        public float Intensity { get; set; }
        public string Animal { get; set; }
        public string Note { get; set; }
    }

    public class EventDecision
    {
        public string Decision { get; set; } // "fire_event", "wait", "send_help"
        public EventChoice Event { get; set; }
        public string Reasoning { get; set; }
        public string NarrativeIntent { get; set; }
    }

    public class NarrativeArcDecision
    {
        public string Decision { get; set; } // "start_arc", "continue", "skip"
        public string ArcName { get; set; }
        public List<ArcEvent> Events { get; set; }
        public string Reasoning { get; set; }
    }

    public class StorytellingPosture
    {
        public string CurrentBlend { get; set; }  // e.g. "cassandra-heavy with randy spice"
        public string Reasoning { get; set; }
        public string NextPostureHint { get; set; } // what might change next
    }

    // ========== Event Types (unchanged) ==========

    public class EventChoice
    {
        public EventChoice()
        {
            Intensity = 1.0f;
            DelayHours = 0;
        }
        public string Category { get; set; }
        public string Type { get; set; }
        public string Subtype { get; set; }
        public string Faction { get; set; }
        public float Intensity { get; set; }
        public int DelayHours { get; set; }
        public string Animal { get; set; }
        public string Note { get; set; }
    }

    public class ArcEvent
    {
        public ArcEvent()
        {
            Intensity = 1.0f;
            DelayHours = 0;
        }
        public float DelayHours { get; set; }
        public string Type { get; set; }
        public string Subtype { get; set; }
        public string Faction { get; set; }
        public float Intensity { get; set; }
        public string Animal { get; set; }
        public string Note { get; set; }
    }

    // ========== Timer Adjustment (clamped in code) ==========

    public class TimerAdjustment
    {
        public float MinorMinHours { get; set; }
        public float MinorMaxHours { get; set; }
        public float MajorMinDays { get; set; }
        public float MajorMaxDays { get; set; }
        public float NarrativeMinDays { get; set; }
        public float NarrativeMaxDays { get; set; }
    }

    // ========== Density Metrics (sent to Claude for awareness) ==========

    public class EventDensity
    {
        public int EventsLast7Days { get; set; }
        public int EventsLast15Days { get; set; }
        public int ThreatsLast7Days { get; set; }
        public int ThreatsLast15Days { get; set; }
        public int DiseasesLast30Days { get; set; }
        public int DaysSinceLastDisease { get; set; }
        public int DaysSinceArcCompleted { get; set; }
        public string ActiveArc { get; set; } // null if no arc running
        public int ActiveArcEventsRemaining { get; set; }
    }

    // ========== Vanilla Reference (static data for Claude) ==========

    public class VanillaReference
    {
        public VanillaStoryteller Cassandra { get; set; }
        public VanillaStoryteller Phoebe { get; set; }
        public VanillaStoryteller Randy { get; set; }
    }

    public class VanillaStoryteller
    {
        public string Style { get; set; }
        public float MiscMtbDays { get; set; }
        public float ThreatCycleDays { get; set; }
        public string ThreatsPerCycle { get; set; }
        public float RestPeriodDays { get; set; }
        public float MinThreatSpacingDays { get; set; }
        public float DiseaseApproxMtbDays { get; set; }
    }

    // ========== Legacy models kept for backwards compat ==========

    public class ClaudeResponse
    {
        public string Decision { get; set; }
        public EventChoice Event { get; set; }
        public string Reasoning { get; set; }
        public string NarrativeIntent { get; set; }
        public TimerAdjustment AdjustTimers { get; set; }
    }

    public class NarrativeArcResponse
    {
        public string ArcName { get; set; }
        public List<ArcEvent> Events { get; set; }
        public string Reasoning { get; set; }
        public TimerAdjustment AdjustTimers { get; set; }
    }
}
