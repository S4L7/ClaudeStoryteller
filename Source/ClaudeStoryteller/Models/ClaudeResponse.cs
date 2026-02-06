using System.Collections.Generic;

namespace ClaudeStoryteller.Models
{
    // Used by minor and major cycles (single event response)
    public class ClaudeResponse
    {
        public string Decision { get; set; }
        public EventChoice Event { get; set; }
        public string Reasoning { get; set; }
        public string NarrativeIntent { get; set; }
        public TimerAdjustment AdjustTimers { get; set; }
    }

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

    // Used by narrative arc cycle (multi-event response)
    public class NarrativeArcResponse
    {
        public string ArcName { get; set; }
        public List<ArcEvent> Events { get; set; }
        public string Reasoning { get; set; }
        public TimerAdjustment AdjustTimers { get; set; }
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

    // Included in any response — Claude adjusts its own pacing
    public class TimerAdjustment
    {
        public float MinorMinHours { get; set; }
        public float MinorMaxHours { get; set; }
        public float MajorMinDays { get; set; }
        public float MajorMaxDays { get; set; }
        public float NarrativeMinDays { get; set; }
        public float NarrativeMaxDays { get; set; }
    }
}
