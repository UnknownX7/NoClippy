using System;
using Dalamud.Game;
using Dalamud.Logging;
using Dalamud.Plugin;

namespace NoClippy
{
    public class NoClippy : IDalamudPlugin
    {
        public string Name => "NoClippy";
        public static NoClippy Plugin { get; private set; }
        public static Configuration Config { get; private set; }

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

                if (!Config.Enable) return;

                TogglePlugin(true);
            }
            catch { PrintError("Failed to load!"); }
        }

        [Command("/noclippy")]
        [HelpMessage("/noclippy [on|off|toggle|dry|help] - Toggles the config window if no option is specified.")]
        private void OnNoClippy(string command, string argument)
        {
            switch (argument)
            {
                case "on":
                case "toggle" when !Config.Enable:
                case "t" when !Config.Enable:
                    TogglePlugin(Config.Enable = true);
                    Config.Save();
                    PrintEcho("Enabled!");
                    break;
                case "off":
                case "toggle" when Config.Enable:
                case "t" when Config.Enable:
                    TogglePlugin(Config.Enable = false);
                    Config.Save();
                    PrintEcho("Disabled!");
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
                        "\non / off / toggle - Enables or disables the plugin." +
                        "\ndry - Toggles dry run (will not override the animation lock).");
                    break;
            }
        }

        public static void PrintEcho(string message) => DalamudApi.ChatGui.Print($"[NoClippy] {message}");
        public static void PrintError(string message) => DalamudApi.ChatGui.PrintError($"[NoClippy] {message}");

        public static void PrintLog(string message)
        {
            if (Config.LogToChat)
                PrintEcho(message);
            else
                PluginLog.LogInformation(message);
        }

        public static int F2MS(float f) => (int)(f * 1000);

        public static void TogglePlugin(bool enable)
        {
            if (enable)
                Game.ReceiveActionEffectHook?.Enable();
            else
                Game.ReceiveActionEffectHook?.Disable();
        }

        private static void Update(Framework framework)
        {
            if (!Config.Enable) return;
            Stats.Update();
        }

        #region IDisposable Support
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            Config.Save();

            DalamudApi.Framework.Update -= Update;
            DalamudApi.PluginInterface.UiBuilder.Draw -= PluginUI.Draw;
            DalamudApi.PluginInterface.UiBuilder.OpenConfigUi -= ConfigUI.ToggleVisible;

            Game.Dispose();

            DalamudApi.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
