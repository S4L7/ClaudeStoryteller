using System;
using System.Text;
using Verse;
using UnityEngine;

namespace ClaudeStoryteller
{
    public class ClaudeStorytellerSettings : ModSettings
    {
        private string encryptedApiKey = "";
        private string cachedDecryptedKey = null;
        public bool enabled = true;

        public float minorMinHours = 36f;
        public float minorMaxHours = 72f;
        public float majorMinDays = 3f;
        public float majorMaxDays = 7f;
        public float narrativeMinDays = 4f;
        public float narrativeMaxDays = 8f;

        public static readonly float MINOR_FLOOR_HOURS = 6f;
        public static readonly float MINOR_CEILING_HOURS = 72f;
        public static readonly float MAJOR_FLOOR_DAYS = 1f;
        public static readonly float MAJOR_CEILING_DAYS = 14f;
        public static readonly float NARRATIVE_FLOOR_DAYS = 2f;
        public static readonly float NARRATIVE_CEILING_DAYS = 15f;

        private static readonly byte[] ObfuscationKey = { 0x43, 0x6C, 0x61, 0x75, 0x64, 0x65, 0x41, 0x49 };

        public string ApiKey
        {
            get
            {
                if (cachedDecryptedKey == null && !string.IsNullOrEmpty(encryptedApiKey))
                {
                    cachedDecryptedKey = Deobfuscate(encryptedApiKey);
                }
                return cachedDecryptedKey ?? "";
            }
            set
            {
                cachedDecryptedKey = value;
                encryptedApiKey = string.IsNullOrEmpty(value) ? "" : Obfuscate(value);
            }
        }

        public bool HasApiKey => !string.IsNullOrEmpty(ApiKey);

        public string GetMaskedApiKey()
        {
            string key = ApiKey;
            if (string.IsNullOrEmpty(key)) return "";
            if (key.Length <= 10) return "••••••••";
            return key.Substring(0, 7) + "•••••••••••••" + key.Substring(key.Length - 4);
        }

        private string Obfuscate(string plaintext)
        {
            byte[] data = Encoding.UTF8.GetBytes(plaintext);
            for (int i = 0; i < data.Length; i++)
            {
                data[i] ^= ObfuscationKey[i % ObfuscationKey.Length];
            }
            return Convert.ToBase64String(data);
        }

        private string Deobfuscate(string encoded)
        {
            try
            {
                byte[] data = Convert.FromBase64String(encoded);
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] ^= ObfuscationKey[i % ObfuscationKey.Length];
                }
                return Encoding.UTF8.GetString(data);
            }
            catch
            {
                return "";
            }
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref encryptedApiKey, "apiKey", "");
            Scribe_Values.Look(ref enabled, "enabled", true);
            Scribe_Values.Look(ref minorMinHours, "minorMinHours", 36f);
            Scribe_Values.Look(ref minorMaxHours, "minorMaxHours", 72f);
            Scribe_Values.Look(ref majorMinDays, "majorMinDays", 3f);
            Scribe_Values.Look(ref majorMaxDays, "majorMaxDays", 7f);
            Scribe_Values.Look(ref narrativeMinDays, "narrativeMinDays", 4f);
            Scribe_Values.Look(ref narrativeMaxDays, "narrativeMaxDays", 8f);
            
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                cachedDecryptedKey = null;
            }
            
            base.ExposeData();
        }

        public float GetMinorInterval()
        {
            return Rand.Range(minorMinHours, minorMaxHours);
        }

        public float GetMajorInterval()
        {
            return Rand.Range(majorMinDays, majorMaxDays) * 24f;
        }

        public float GetNarrativeInterval()
        {
            return Rand.Range(narrativeMinDays, narrativeMaxDays) * 24f;
        }

        public void ApplyTimerAdjustment(Models.TimerAdjustment adj)
        {
            if (adj == null) return;

            if (adj.MinorMinHours > 0 && adj.MinorMaxHours > 0)
            {
                minorMinHours = Mathf.Clamp(adj.MinorMinHours, MINOR_FLOOR_HOURS, MINOR_CEILING_HOURS);
                minorMaxHours = Mathf.Clamp(adj.MinorMaxHours, MINOR_FLOOR_HOURS, MINOR_CEILING_HOURS);
                if (minorMinHours > minorMaxHours) minorMaxHours = minorMinHours;
            }

            if (adj.MajorMinDays > 0 && adj.MajorMaxDays > 0)
            {
                majorMinDays = Mathf.Clamp(adj.MajorMinDays, MAJOR_FLOOR_DAYS, MAJOR_CEILING_DAYS);
                majorMaxDays = Mathf.Clamp(adj.MajorMaxDays, MAJOR_FLOOR_DAYS, MAJOR_CEILING_DAYS);
                if (majorMinDays > majorMaxDays) majorMaxDays = majorMinDays;
            }

            if (adj.NarrativeMinDays > 0 && adj.NarrativeMaxDays > 0)
            {
                narrativeMinDays = Mathf.Clamp(adj.NarrativeMinDays, NARRATIVE_FLOOR_DAYS, NARRATIVE_CEILING_DAYS);
                narrativeMaxDays = Mathf.Clamp(adj.NarrativeMaxDays, NARRATIVE_FLOOR_DAYS, NARRATIVE_CEILING_DAYS);
                if (narrativeMinDays > narrativeMaxDays) narrativeMaxDays = narrativeMinDays;
            }

            ClaudeLogger.LogEntry("TIMER_ADJUST",
                $"Timers updated — Minor: {minorMinHours:F1}-{minorMaxHours:F1}h, " +
                $"Major: {majorMinDays:F1}-{majorMaxDays:F1}d, " +
                $"Narrative: {narrativeMinDays:F1}-{narrativeMaxDays:F1}d"
            );
        }
    }

    public class ClaudeStorytellerMod : Mod
    {
        public static ClaudeStorytellerSettings settings;
        private string testResult = "";
        private bool testInProgress = false;
        private string inputBuffer = "";
        private bool showKeyEntry = false;

        public ClaudeStorytellerMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<ClaudeStorytellerSettings>();
            Log.Message("[ClaudeStoryteller] Mod loaded successfully!");
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.Label("Claude API Key:");
            
            if (settings.HasApiKey && !showKeyEntry)
            {
                // Key exists and we're not editing - show masked version
                listing.Label($"  {settings.GetMaskedApiKey()}");
                listing.Label("API key format: Valid");
                
                if (listing.ButtonText("Change API Key"))
                {
                    showKeyEntry = true;
                    inputBuffer = "";
                }
                
                if (testInProgress)
                {
                    listing.Label("Testing connection...");
                }
                else
                {
                    if (listing.ButtonText("Test API Connection"))
                    {
                        TestApiConnection();
                    }

                    if (!string.IsNullOrEmpty(testResult))
                    {
                        listing.Label($"Result: {testResult}");
                    }
                }
            }
            else
            {
                // No key or actively editing - show entry field
                listing.Label("Enter your API key from console.anthropic.com:");
                inputBuffer = listing.TextEntry(inputBuffer);
                
                bool validFormat = ClaudeApiClient.ValidateApiKey(inputBuffer);
                if (!string.IsNullOrEmpty(inputBuffer))
                {
                    listing.Label(validFormat ? "Format: Valid" : "Format: Invalid (should start with sk-ant-)");
                }
                
                if (validFormat)
                {
                    if (listing.ButtonText("Save API Key"))
                    {
                        settings.ApiKey = inputBuffer;
                        inputBuffer = "";
                        showKeyEntry = false;
                        testResult = "";
                    }
                }
                
                if (settings.HasApiKey)
                {
                    if (listing.ButtonText("Cancel"))
                    {
                        inputBuffer = "";
                        showKeyEntry = false;
                    }
                }
            }

            listing.Gap();
            listing.CheckboxLabeled("Enable Claude Storyteller", ref settings.enabled);

            listing.Gap();
            listing.Label("Minor Events (weather, animals, visitors)");
            listing.Label($"  Interval: {settings.minorMinHours:F0} - {settings.minorMaxHours:F0} game hours");
            settings.minorMinHours = listing.Slider(settings.minorMinHours, 6f, 72f);
            settings.minorMaxHours = listing.Slider(settings.minorMaxHours, 6f, 72f);
            if (settings.minorMinHours > settings.minorMaxHours)
                settings.minorMaxHours = settings.minorMinHours;

            listing.Gap();
            listing.Label("Major Events (raids, infestations, mechs)");
            listing.Label($"  Interval: {settings.majorMinDays:F1} - {settings.majorMaxDays:F1} game days");
            settings.majorMinDays = listing.Slider(settings.majorMinDays, 1f, 14f);
            settings.majorMaxDays = listing.Slider(settings.majorMaxDays, 1f, 14f);
            if (settings.majorMinDays > settings.majorMaxDays)
                settings.majorMaxDays = settings.majorMinDays;

            listing.Gap();
            listing.Label("Narrative Arcs (multi-event story sequences)");
            listing.Label($"  Interval: {settings.narrativeMinDays:F1} - {settings.narrativeMaxDays:F1} game days");
            settings.narrativeMinDays = listing.Slider(settings.narrativeMinDays, 2f, 15f);
            settings.narrativeMaxDays = listing.Slider(settings.narrativeMaxDays, 2f, 15f);
            if (settings.narrativeMinDays > settings.narrativeMaxDays)
                settings.narrativeMaxDays = settings.narrativeMinDays;

            listing.Gap();
            listing.Gap();
            listing.Label($"Log: {ClaudeLogger.GetLogPath()}");

            listing.End();
            base.DoSettingsWindowContents(inRect);
        }

        private async void TestApiConnection()
        {
            testInProgress = true;
            testResult = "";

            try
            {
                testResult = await ClaudeApiClient.TestConnection(settings.ApiKey);
            }
            catch (Exception ex)
            {
                testResult = $"Error: {ex.Message}";
            }
            finally
            {
                testInProgress = false;
            }
        }

        public override string SettingsCategory()
        {
            return "Claude Storyteller";
        }
    }
}
