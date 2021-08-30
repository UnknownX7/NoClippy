using Dalamud.Configuration;

namespace NoClippy
{
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; }

        public bool Enable = true;
        public bool EnableLogging = false;
        public bool EnableEncounterStats = false;
        public bool EnableEncounterStatsLogging = false;
        public bool EnableDryRun = false;
        public bool LogToChat = false;
        public float QueueThreshold = 0.5f;

        public void Initialize() { }

        public void Save() => DalamudApi.PluginInterface.SavePluginConfig(this);
    }
}
