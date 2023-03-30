using System;
using Dalamud.Game;
using Dalamud.Game.Text;
using Dalamud.Logging;
using Dalamud.Plugin;

namespace NoClippy
{
    public class NoClippy : IDalamudPlugin
    {
        public string Name => "NoClippy";
        public static NoClippy Plugin { get; private set; }
        public static Configuration Config { get; private set; }

        public static bool April1 => DateTime.Now is { Month: 4, Day: 1 };

        public NoClippy(DalamudPluginInterface pluginInterface)
        {
            Plugin = this;
            DalamudApi.Initialize(this, pluginInterface);

            Config = (Configuration)DalamudApi.PluginInterface.GetPluginConfig() ?? new();
            Config.Initialize();

            try
            {
                Game.Initialize();

                DalamudApi.Framework.Update += Update;
                DalamudApi.PluginInterface.UiBuilder.Draw += PluginUI.Draw;
                DalamudApi.PluginInterface.UiBuilder.OpenConfigUi += ConfigUI.ToggleVisible;

                Modules.Modules.Initialize();
            }
            catch (Exception e)
            {
                PrintError("Failed to load!");
                PluginLog.LogError(e.ToString());
            }
        }

        [Command("/noclippy")]
        [HelpMessage("/noclippy [on|off|toggle|dry|help] - Toggles the config window if no option is specified.")]
        private void OnNoClippy(string command, string argument)
        {
            switch (argument)
            {
                case "on":
                case "toggle" when !Config.EnableAnimLockComp:
                case "t" when !Config.EnableAnimLockComp:
                    Config.EnableAnimLockComp = true;
                    Config.Save();
                    PrintEcho("Enabled animation lock compensation!");
                    break;
                case "off":
                case "toggle" when Config.EnableAnimLockComp:
                case "t" when Config.EnableAnimLockComp:
                    Config.EnableAnimLockComp = false;
                    Config.Save();
                    PrintEcho("Disabled animation lock compensation!");
                    break;
                case "dry":
                case "d":
                    PrintEcho($"Dry run is now {((Config.EnableDryRun = !Config.EnableDryRun) ? "enabled" : "disabled")}.");
                    Config.Save();
                    break;
                case "":
                    ConfigUI.ToggleVisible();
                    break;
                default:
                    PrintEcho("Invalid usage: Command must be \"/noclippy <option>\"." +
                        "\non / off / toggle - Enables or disables animation lock compensation." +
                        "\ndry - Toggles dry run (will not override the animation lock).");
                    break;
            }
        }

        public static void PrintEcho(string message) => DalamudApi.ChatGui.Print($"[NoClippy] {message}");
        public static void PrintError(string message) => DalamudApi.ChatGui.PrintError($"[NoClippy] {message}");

        public static void PrintLog(string message)
        {
            if (Config.LogToChat)
            {
                if (Config.LogChatType != XivChatType.None)
                {
                    DalamudApi.ChatGui.PrintChat(new XivChatEntry
                    {
                        Message = $"[NoClippy] {message}",
                        Type = Config.LogChatType
                    });
                }
                else
                {
                    PrintEcho(message);
                }
            }
            else
            {
                PluginLog.LogInformation(message);
            }
        }

        public static int F2MS(float f) => (int)Math.Round(f * 1000);

        private static void Update(Framework framework) => Game.Update();

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            Config.Save();

            DalamudApi.Framework.Update -= Update;
            DalamudApi.PluginInterface.UiBuilder.Draw -= PluginUI.Draw;
            DalamudApi.PluginInterface.UiBuilder.OpenConfigUi -= ConfigUI.ToggleVisible;

            Modules.Modules.Dispose();
            Game.Dispose();

            DalamudApi.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
