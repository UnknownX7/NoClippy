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
    }
}

namespace NoClippy.Modules
{
    public class QueueThreshold : Module
    {
        private IntPtr queueThresholdPtr = IntPtr.Zero;
        private AsmHook queueThresholdHook;

        public override bool IsEnabled
        {
            get => NoClippy.Config.QueueThreshold != 0.5f;
            set => NoClippy.Config.QueueThreshold = value ? 1 : 0.5f;
        }

        public override int DrawOrder => 10;

        private unsafe float Threshold
        {
            get => queueThresholdPtr == IntPtr.Zero ? 0.5f : *(float*)queueThresholdPtr;
            set
            {
                if (queueThresholdPtr == IntPtr.Zero) return;
                *(float*)queueThresholdPtr = value < 2.5f ? value : 10;
            }
        }

        private void SetupQueueThreshold()
        {
            queueThresholdPtr = Marshal.AllocHGlobal(sizeof(float));

            var ptrStr = BitConverter.GetBytes(queueThresholdPtr.ToInt64()).Reverse()
                .Aggregate(string.Empty, (current, b) => current + b.ToString("X2")) + "h";
            var asm = new[]
            {
                "use64",
                $"mov rax, {ptrStr}",
                "comiss xmm1, [rax]"
            };

            queueThresholdHook = new(DalamudApi.SigScanner.ScanModule("0F 2F 0D ?? ?? ?? ?? 76 1B"), asm, "QueueThresholdHook", AsmHookBehaviour.DoNotExecuteOriginal);
            queueThresholdHook.Enable();
        }

        public override void DrawConfig()
        {
            if (ImGui.SliderFloat("Queue Threshold", ref NoClippy.Config.QueueThreshold, 0, 2.5f, "%.1f"))
            {
                Threshold = NoClippy.Config.QueueThreshold;
                NoClippy.Config.Save();
            }
            PluginUI.SetItemTooltip("Max time left on the GCD before you can queue another GCD." +
                "\nDefault is 0.5, set it to 2.5 to always allow queuing.");
        }

        public override void Enable()
        {
            if (queueThresholdHook == null)
                SetupQueueThreshold();

            Threshold = NoClippy.Config.QueueThreshold;
        }

        public override void Disable()
        {
            queueThresholdHook?.Dispose();
            queueThresholdHook = null;
            Marshal.FreeHGlobal(queueThresholdPtr);
        }
    }
}
