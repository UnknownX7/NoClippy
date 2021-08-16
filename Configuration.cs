using Dalamud.Configuration;
using Dalamud.Plugin;
using Newtonsoft.Json;

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

        [JsonIgnore] private DalamudPluginInterface pluginInterface;

        public void Initialize(DalamudPluginInterface p)
        {
            pluginInterface = p;
        }

        public void Save()
        {
            pluginInterface.SavePluginConfig(this);
        }
    }
}
