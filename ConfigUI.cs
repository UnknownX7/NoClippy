using System;
using System.Diagnostics;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility;
using Dalamud.Bindings.ImGui;

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
            AddDonationHeader(1);
            Modules.Modules.Draw();
            ImGui.End();
        }

        // TODO: Remove when Hypostasis is added
        private class HeaderIconOptions
        {
            public int Position { get; init; } = 1;
            public Vector2 Offset { get; init; } = Vector2.Zero;
            public ImGuiMouseButton MouseButton { get; init; } = ImGuiMouseButton.Left;
            public string Tooltip { get; init; } = string.Empty;
            public uint Color { get; init; } = 0xFFFFFFFF;
            public bool ToastTooltipOnClick { get; init; } = false;
            public ImGuiMouseButton ToastTooltipOnClickButton { get; init; } = ImGuiMouseButton.Left;
        }

        private static bool AddHeaderIcon(string id, FontAwesomeIcon icon, HeaderIconOptions options)
        {
            if (ImGui.IsWindowCollapsed()) return false;

            var scale = ImGuiHelpers.GlobalScale;
            var prevCursorPos = ImGui.GetCursorPos();
            var buttonSize = new Vector2(20 * scale);
            var buttonPos = new Vector2(ImGui.GetWindowWidth() - buttonSize.X - 17 * options.Position * scale - ImGui.GetStyle().FramePadding.X * 2, 0) + options.Offset;
            ImGui.SetCursorPos(buttonPos);
            var drawList = ImGui.GetWindowDrawList();
            drawList.PushClipRectFullScreen();

            var pressed = false;
            ImGui.InvisibleButton(id, buttonSize);
            var itemMin = ImGui.GetItemRectMin();
            var itemMax = ImGui.GetItemRectMax();
            var halfSize = ImGui.GetItemRectSize() / 2;
            var center = itemMin + halfSize;
            if (ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(itemMin, itemMax, false))
            {
                if (!string.IsNullOrEmpty(options.Tooltip))
                    ImGui.SetTooltip(options.Tooltip);
                ImGui.GetWindowDrawList().AddCircleFilled(center, halfSize.X, ImGui.GetColorU32(ImGui.IsMouseDown(ImGuiMouseButton.Left) ? ImGuiCol.ButtonActive : ImGuiCol.ButtonHovered));
                if (ImGui.IsMouseReleased(options.MouseButton))
                    pressed = true;
                if (options.ToastTooltipOnClick && ImGui.IsMouseReleased(options.ToastTooltipOnClickButton))
                    DalamudApi.ShowNotification(options.Tooltip!, NotificationType.Info);
            }

            ImGui.SetCursorPos(buttonPos);
            ImGui.PushFont(UiBuilder.IconFont);
            var iconString = icon.ToIconString();
            drawList.AddText(UiBuilder.IconFont, ImGui.GetFontSize(), itemMin + halfSize - ImGui.CalcTextSize(iconString) / 2 + Vector2.One, options.Color, iconString);
            ImGui.PopFont();

            ImGui.PopClipRect();
            ImGui.SetCursorPos(prevCursorPos);

            return pressed;
        }

        private static void AddDonationHeader(int position)
        {
            if (!AddHeaderIcon("_Donate", FontAwesomeIcon.Heart, new HeaderIconOptions { Position = position, Color = 0xFF3030D0, MouseButton = ImGuiMouseButton.Right, Tooltip = "Right click to open the donation page.", ToastTooltipOnClick = true })) return;
            StartProcess(@"https://ko-fi.com/unknownx7");
        }

        private static bool StartProcess(string process, bool admin = false)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = process,
                    UseShellExecute = true,
                    Verb = admin ? "runas" : string.Empty
                });
                return true;
            }
            catch (Exception e)
            {
                DalamudApi.LogError("Failed to start process!", e);
                return false;
            }

        }

    }
}
