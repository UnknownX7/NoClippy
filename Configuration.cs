using Dalamud.Configuration;

namespace NoClippy
{
    public partial class Configuration : IPluginConfiguration
    {
        public int Version { get; set; }

        public bool LogToChat = false;
        public float QueueThreshold = 0.5f;

        public void Initialize() { }

        public void Save()
        {
            Modules.Modules.CheckModules();
            DalamudApi.PluginInterface.SavePluginConfig(this);
        }
    }
}
