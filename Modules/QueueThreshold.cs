using System;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Hooking;
using ImGuiNET;

namespace NoClippy
{
    public partial class Configuration
    {
        public float QueueThreshold = 0.5f;
        public bool EnableDynamicThreshold = false;
    }
}

namespace NoClippy.Modules
{
    public class QueueThreshold : Module
    {
        public override bool IsEnabled
        {
            get => NoClippy.Config.EnableDynamicThreshold || NoClippy.Config.QueueThreshold != 0.5f;
            set
            {
                NoClippy.Config.QueueThreshold = value ? 1 : 0.5f;
                NoClippy.Config.EnableDynamicThreshold = false;
            }
        }

        public override int DrawOrder => 10;

        private nint queueThresholdPtr = nint.Zero;
        private AsmHook queueThresholdHook;

        private ushort lastSequence = 0;

        private unsafe float Threshold
        {
            get => queueThresholdPtr == nint.Zero ? 0.5f : *(float*)queueThresholdPtr;
            set
            {
                if (queueThresholdPtr == nint.Zero) return;
                *(float*)queueThresholdPtr = value < 2.5f ? value : 10;
            }
        }

        private delegate byte CanQueueDelegate(nint actionManager, uint actionType, uint actionID);
        private Hook<CanQueueDelegate> CanQueueHook;
        private unsafe byte CanQueueDetour(nint actionManager, uint actionType, uint actionID)
        {
            if (NoClippy.Config.EnableDynamicThreshold && Game.actionManager->currentSequence != lastSequence)
            {
                lastSequence = Game.actionManager->currentSequence;
                Threshold = Game.actionManager->isCasting ? Math.Max(Game.actionManager->gcdRecastTime - Game.actionManager->castTime + 0.5f, 0.5f) : NoClippy.Config.QueueThreshold;
            }

            return CanQueueHook.Original(actionManager, actionType, actionID);
        }

        private void SetupQueueThreshold()
        {
            queueThresholdPtr = Marshal.AllocHGlobal(sizeof(float));

            var ptrStr = BitConverter.GetBytes(queueThresholdPtr).Reverse()
                .Aggregate(string.Empty, (current, b) => current + b.ToString("X2")) + "h";
            var asm = new[]
            {
                "use64",
                $"mov rax, {ptrStr}",
                "comiss xmm1, [rax]"
            };

            queueThresholdHook = new(DalamudApi.SigScanner.ScanModule("0F 2F 0D ?? ?? ?? ?? 76 1B 32 C0"), asm, "QueueThresholdHook", AsmHookBehaviour.DoNotExecuteOriginal);
            queueThresholdHook.Enable();
        }

        public override void DrawConfig()
        {
            ImGui.TextUnformatted("An improved Queue Threshold is now available via ReAction.\nThis feature will be removed in an upcoming update.");

            var _ = NoClippy.Config.QueueThreshold != 0.5f;
            if (ImGui.Checkbox("##QueueThresholdIsEnabled", ref _))
            {
                NoClippy.Config.QueueThreshold = _ ? 1 : 0.5f;
                NoClippy.Config.Save();
            }

            ImGui.SameLine();

            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X / 1.5f);
            if (ImGui.SliderFloat("Queue Threshold", ref NoClippy.Config.QueueThreshold, 0, 2.5f, "%.1f"))
            {
                Threshold = NoClippy.Config.QueueThreshold;
                NoClippy.Config.Save();
            }
            PluginUI.SetItemTooltip("Max time left on the GCD before you can queue another GCD." +
                "\nDefault is 0.5, set it to 2.5 to always allow queuing.");

            if (NoClippy.Config.QueueThreshold == 0)
                ImGui.TextUnformatted(":worry:");

            if (ImGui.Checkbox("Dynamic Cast Threshold", ref NoClippy.Config.EnableDynamicThreshold))
            {
                if (!NoClippy.Config.EnableDynamicThreshold)
                    Threshold = NoClippy.Config.QueueThreshold;
                NoClippy.Config.Save();
            }
            PluginUI.SetItemTooltip("Allows queuing during the slidecast window.");
        }

        public override void Enable()
        {
            if (queueThresholdHook == null)
                SetupQueueThreshold();

            Threshold = NoClippy.Config.QueueThreshold;
            CanQueueHook = new Hook<CanQueueDelegate>(DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 84 C0 74 37 8B 84 24 ?? ?? 00 00"), CanQueueDetour);
            CanQueueHook.Enable();
        }

        public override void Disable()
        {
            queueThresholdHook?.Dispose();
            queueThresholdHook = null;
            Marshal.FreeHGlobal(queueThresholdPtr);
            CanQueueHook?.Dispose();
        }
    }
}
