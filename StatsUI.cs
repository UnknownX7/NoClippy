using System.Collections.Generic;
using System.Numerics;
using Dalamud.Interface;
using ImGuiNET;
using ImPlotNET;

namespace NoClippy
{
    public static class StatsUI
    {
        public static bool isVisible = false;

        private static List<float> _gcdClipPlotPoints = new() {0.1f, 0.3f, 0.45f, 0.45f, 0.45f, 0.5f, 0.9f};
        private static List<float> _wastedGCDPlotPoints = new();

        public static void Draw()
        {
            if (!isVisible) return;

            //ImGui.SetNextWindowSizeConstraints(new Vector2(300, 500) * ImGuiHelpers.GlobalScale, new Vector2(10000));
            ImGui.SetNextWindowSize(new Vector2(400, 400) * ImGuiHelpers.GlobalScale);
            ImGui.Begin("NoClippy Statistics", ref isVisible, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse);

            // Currently broken in Dalamud
            ImPlot.SetNextPlotLimitsX(0, _gcdClipPlotPoints.Count);
            ImPlot.SetNextPlotLimitsY(0, GetLastIndexOrDefault(_gcdClipPlotPoints, 0) + 0.1f, ImGuiCond.Always);
            if (ImPlot.BeginPlot("hello", string.Empty, string.Empty, new Vector2(-1, 0), 0))
            {
                var arr = _gcdClipPlotPoints.ToArray();
                ImPlot.PlotShaded(string.Empty, ref arr[0], _gcdClipPlotPoints.Count, 0, 1, 0);
                ImPlot.EndPlot();
            }

            ImGui.End();
        }

        private static T GetLastIndexOrDefault<T>(IReadOnlyList<T> array, T def)
        {
            var lastIndex = array.Count - 1;
            return lastIndex >= 0 ? array[lastIndex] : def;
        }

        public static void AddGCDClip(float clip) => _gcdClipPlotPoints.Add(GetLastIndexOrDefault(_gcdClipPlotPoints, 0) + clip);
        public static void AddWastedGCD(float waste) => _wastedGCDPlotPoints.Add(GetLastIndexOrDefault(_wastedGCDPlotPoints, 0) + waste);

        public static void ResetStats()
        {
            _gcdClipPlotPoints.Clear();
            _wastedGCDPlotPoints.Clear();
        }
    }
}