using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ClaudeStoryteller
{
    public class QueuedEvent
    {
        public string EventType { get; set; }
        public string Category { get; set; }
        public string Subtype { get; set; }
        public string Faction { get; set; }
        public float Intensity { get; set; }
        public int FireAtTick { get; set; }
        public string SourceCycle { get; set; } // "minor", "major", "narrative"
        public string ArcName { get; set; }     // null unless part of a narrative arc
        public string Note { get; set; }

        public QueuedEvent()
        {
            Intensity = 1.0f;
        }
    }

    public static class EventQueue
    {
        private static readonly List<QueuedEvent> queue = new List<QueuedEvent>();
        private static readonly object lockObj = new object();

        // Conflict window: events of the same type within this many ticks get deduplicated
        private const int CONFLICT_WINDOW_TICKS = 4 * GenDate.TicksPerHour;

        public static void Enqueue(QueuedEvent evt)
        {
            lock (lockObj)
            {
                // Narrative arc events take priority — remove conflicting non-narrative events
                if (evt.SourceCycle == "narrative")
                {
                    queue.RemoveAll(q =>
                        q.SourceCycle != "narrative" &&
                        q.EventType == evt.EventType &&
                        Math.Abs(q.FireAtTick - evt.FireAtTick) < CONFLICT_WINDOW_TICKS
                    );
                }
                // Non-narrative events get skipped if a narrative event is already there
                else
                {
                    bool narrativeConflict = queue.Any(q =>
                        q.SourceCycle == "narrative" &&
                        q.EventType == evt.EventType &&
                        Math.Abs(q.FireAtTick - evt.FireAtTick) < CONFLICT_WINDOW_TICKS
                    );
                    if (narrativeConflict)
                    {
                        ClaudeLogger.LogEventSkipped(
                            $"Dedup: {evt.EventType} from {evt.SourceCycle} conflicts with queued narrative event"
                        );
                        return;
                    }
                }

                queue.Add(evt);
                queue.Sort((a, b) => a.FireAtTick.CompareTo(b.FireAtTick));

                ClaudeLogger.LogEntry("QUEUE_ADD",
                    $"Queued {evt.EventType} from {evt.SourceCycle} at tick {evt.FireAtTick}" +
                    (evt.ArcName != null ? $" (arc: {evt.ArcName})" : "")
                );
            }
        }

        public static void EnqueueDelayed(QueuedEvent evt, float delayHours)
        {
            evt.FireAtTick = Find.TickManager.TicksGame + (int)(delayHours * GenDate.TicksPerHour);
            Enqueue(evt);
        }

        public static List<QueuedEvent> PopReady(int currentTick)
        {
            lock (lockObj)
            {
                var ready = queue.Where(e => e.FireAtTick <= currentTick).ToList();
                foreach (var evt in ready)
                {
                    queue.Remove(evt);
                }
                return ready;
            }
        }

        public static List<QueuedEvent> PeekAll()
        {
            lock (lockObj)
            {
                return queue.ToList();
            }
        }

        public static int Count
        {
            get { lock (lockObj) { return queue.Count; } }
        }

        public static void Clear()
        {
            lock (lockObj)
            {
                queue.Clear();
            }
        }

        public static void ClearBySource(string sourceCycle)
        {
            lock (lockObj)
            {
                queue.RemoveAll(e => e.SourceCycle == sourceCycle);
            }
        }

        public static List<string> GetQueuedTypes()
        {
            lock (lockObj)
            {
                return queue.Select(e => e.EventType).Distinct().ToList();
            }
        }

        public static string GetQueueSummary()
        {
            lock (lockObj)
            {
                if (queue.Count == 0) return "empty";
                return string.Join(", ", queue.Select(e =>
                    $"{e.EventType}@tick{e.FireAtTick}({e.SourceCycle})"
                ));
            }
        }
    }
}
