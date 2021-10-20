using System;
using Dalamud.Configuration;

#pragma warning disable CS0612, CA1041

namespace NoClippy
{
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; }

        [Obsolete] public bool Enable { internal get; set; } = true;
        public bool EnableAnimLockComp = true;
        public bool EnableLogging = false;
        public bool EnableEncounterStats = false;
        public bool EnableEncounterStatsLogging = false;
        public bool EnableDryRun = false;
        public bool LogToChat = false;
        public float QueueThreshold = 0.5f;

        public void Initialize()
        {
            // Obsolete setting, to be removed post api4
            if (!Enable)
                EnableAnimLockComp = false;
        }

        public void Save()
        {
            Modules.Modules.CheckModules();
            DalamudApi.PluginInterface.SavePluginConfig(this);
        }
    }
}
