using System;
using System.IO;
using RimWorld;
using Verse;

namespace ClaudeStoryteller
{
    public static class ClaudeLogger
    {
        private static string logPath;
        private static readonly object fileLock = new object();

        public static void Initialize()
        {
            try
            {
                string configFolder = GenFilePaths.ConfigFolderPath;
                logPath = Path.Combine(configFolder, "ClaudeStoryteller.log");
                Log.Message($"[ClaudeStoryteller] Logging to: {logPath}");
            }
            catch (Exception ex)
            {
                Log.Warning($"[ClaudeStoryteller] Failed to initialize log path: {ex.Message}");
                logPath = null;
            }
        }

        public static void LogEntry(string category, string message)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            int gameTick = Find.TickManager?.TicksGame ?? 0;
            int gameDay = gameTick / GenDate.TicksPerDay;
            float gameHour = (gameTick % GenDate.TicksPerDay) / (float)GenDate.TicksPerHour;

            string entry = $"[{timestamp}] [Day {gameDay} Hour {gameHour:F1}] [{category}]\n{message}\n{"".PadRight(80, '-')}\n";

            Log.Message($"[ClaudeStoryteller] [{category}] {message.Split('\n')[0]}");

            lock (fileLock)
            {
                try
                {
                    if (string.IsNullOrEmpty(logPath)) Initialize();
                    if (!string.IsNullOrEmpty(logPath))
                    {
                        File.AppendAllText(logPath, entry);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[ClaudeStoryteller] Failed to write log: {ex.Message}");
                }
            }
        }

        public static void LogStateRequest(string stateJson)
        {
            LogEntry("STATE_SENT", $"Colony state sent to Claude API:\n{stateJson}");
        }

        public static void LogRawResponse(string rawResponse)
        {
            LogEntry("RAW_RESPONSE", $"Raw API response:\n{rawResponse}");
        }

        public static void LogParsedDecision(string decision, string eventType, string reasoning, string intent)
        {
            LogEntry("PARSED_DECISION",
                $"Decision: {decision}\n" +
                $"Event Type: {eventType ?? "none"}\n" +
                $"Reasoning: {reasoning}\n" +
                $"Intent: {intent}");
        }

        public static void LogEventFired(string eventType, float intensity, string faction, string subtype, float points)
        {
            LogEntry("EVENT_FIRED",
                $"Event: {eventType}\n" +
                $"Intensity: {intensity:F2}\n" +
                $"Faction: {faction ?? "default"}\n" +
                $"Subtype: {subtype ?? "default"}\n" +
                $"Points: {points:F0}");
        }

        public static void LogEventSkipped(string reason)
        {
            LogEntry("EVENT_SKIPPED", $"Event not fired: {reason}");
        }

        public static void LogApiError(string error, string details = null)
        {
            LogEntry("API_ERROR", $"Error: {error}\n{(details != null ? $"Details: {details}" : "")}");
        }

        public static void ClearLog()
        {
            lock (fileLock)
            {
                try
                {
                    if (!string.IsNullOrEmpty(logPath) && File.Exists(logPath))
                    {
                        File.WriteAllText(logPath, $"=== Claude Storyteller Log Started {DateTime.Now} ===\n\n");
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[ClaudeStoryteller] Failed to clear log: {ex.Message}");
                }
            }
        }

        public static string GetLogPath()
        {
            return logPath ?? "Not initialized";
        }
    }
}
