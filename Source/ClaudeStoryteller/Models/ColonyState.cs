using System.Collections.Generic;
namespace ClaudeStoryteller.Models
{
    public class ColonyState
    {
        public string RequestId { get; set; }
        public string CallType { get; set; }
        public ColonyInfo Colony { get; set; }
        public CombatReadiness CombatReadiness { get; set; }
        public Resources Resources { get; set; }
        public RecentHistory RecentHistory { get; set; }
        public Dictionary<string, int> Cooldowns { get; set; }
        public List<string> AvailableFactions { get; set; }
        public List<string> DoNotRepeat { get; set; }
        public QueueContext CurrentQueue { get; set; }
        public ArcHistorySummary ArcHistory { get; set; }
        public DifficultyInfo Difficulty { get; set; }
        public Dictionary<string, List<string>> AvailableEvents { get; set; }
        public EventDensity Density { get; set; }
        public string LastPosture { get; set; }
        public List<string> HighlightedEvents { get; set; }
        public List<string> ExcludedThisCall { get; set; }
        public string StorytellingMood { get; set; }
        public Dictionary<string, int> CategoryUsageLast5 { get; set; }
        public int RandomSeed { get; set; }
    }
    public class ColonyInfo
    {
        public string Name { get; set; }
        public int DaysSurvived { get; set; }
        public string Phase { get; set; }
        public string NarrativeState { get; set; }
        public int ColonistCount { get; set; }
        public float Wealth { get; set; }
        public float RaidPoints { get; set; }
        public float AdaptationScore { get; set; }
        public float ThreatScale { get; set; }
    }
    public class CombatReadiness
    {
        public float Score { get; set; }
        public string MeleeStrength { get; set; }
        public string RangedStrength { get; set; }
        public List<string> Defenses { get; set; }
        public List<string> Vulnerabilities { get; set; }
    }
    public class Resources
    {
        public int FoodDays { get; set; }
        public string Medicine { get; set; }
        public string Components { get; set; }
        public int Silver { get; set; }
    }
    public class RecentHistory
    {
        public int DaysSinceThreat { get; set; }
        public int DaysSinceColonistDeath { get; set; }
        public int DaysSinceColonistDowned { get; set; }
        public List<PastEvent> LastEvents { get; set; }
    }
    public class PastEvent
    {
        public string Type { get; set; }
        public int DaysAgo { get; set; }
        public string Outcome { get; set; }
    }
    public class QueueContext
    {
        public int PendingCount { get; set; }
        public List<string> QueuedTypes { get; set; }
        public string QueueSummary { get; set; }
    }
    public class ArcHistorySummary
    {
        public int TotalArcs { get; set; }
        public List<ArcSummaryEntry> Last3Arcs { get; set; }
        public List<string> OverusedEvents { get; set; }
        public List<string> OverusedOpeners { get; set; }
        public List<string> UnderusedEvents { get; set; }
        public string DominantPattern { get; set; }
        public string Instruction { get; set; }
    }
    public class ArcSummaryEntry
    {
        public string Name { get; set; }
        public List<string> Events { get; set; }
        public string Outcome { get; set; }
        public int Day { get; set; }
    }
    public class DifficultyInfo
    {
        public string Label { get; set; }
        public float ThreatScale { get; set; }
        public float MaxIntensity { get; set; }
        public float MinIntensity { get; set; }
        public bool AllowThreats { get; set; }
        public bool AllowMajorThreats { get; set; }
    }
}
