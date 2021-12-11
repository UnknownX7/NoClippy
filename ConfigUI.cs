using System.Numerics;
using Dalamud.Interface;
using ImGuiNET;

namespace NoClippy
{
    public static class ConfigUI
    {
        public static bool isVisible = false;
        public static void ToggleVisible() => isVisible ^= true;

        public static void Draw()
        {
            if (!isVisible) return;

            //ImGui.SetNextWindowSizeConstraints(new Vector2(400, 200) * ImGuiHelpers.GlobalScale, new Vector2(10000));
            ImGui.SetNextWindowSize(new Vector2(400, 0) * ImGuiHelpers.GlobalScale);
            ImGui.Begin("NoClippy Configuration", ref isVisible, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse);
            Modules.Modules.Draw();
            ImGui.End();
        }
    }
}
