using System;
using System.Linq;
using Dalamud.Game.ClientState.Statuses;
using ImGuiNET;

namespace NoClippy
{
    public partial class Configuration
    {
        public bool PredictInstantCasts = false;
        public bool PredictDualcast = false;
    }
}

namespace NoClippy.Modules
{
    public class StatusPrediction : Module
    {
        public override int DrawOrder => 15;

        private ushort predictedStatus = 0;
        private byte predictedStatusStacks = 0;
        private byte predictedStatusParam = 0;
        private float timeRemaining = 0;

        private void SetPredictedStatus(ushort status, byte stacks, byte param)
        {
            predictedStatus = status;
            predictedStatusStacks = stacks;
            predictedStatusParam = param;
            timeRemaining = 0.8f;
        }

        private static bool TryGetFreeStatus(StatusList statuses, out IntPtr statusPtr)
        {
            for (int i = 0; i < statuses.Length; i++)
            {
                var status = statuses[i];
                if (status != null && IsStatusValid(status.Address)) continue;
                statusPtr = statuses.GetStatusAddress(i);
                return true;
            }

            statusPtr = IntPtr.Zero;
            return false;
        }

        private static unsafe void ApplyStatus(IntPtr statusPtr, ushort status, byte stacks, byte param)
        {
            *(ushort*)statusPtr = status;
            *(byte*)(statusPtr + 0x2) = stacks;
            *(byte*)(statusPtr + 0x3) = param;
        }

        private static unsafe bool IsStatusValid(IntPtr statusPtr)
        {
            var time = *(float*)(statusPtr + 0x4);
            var objectID = *(uint*)(statusPtr + 0x8);
            return time > 0 || objectID is not (0 or 0xE0000000); // Length - 7 seems to be the last one with objectID 0xE0000000?
        }

        private void UseActionLocation(IntPtr actionManager, uint actionType, uint actionID, long targetedActorID, IntPtr vectorLocation, uint param)
        {
            if (actionType != 1) return;

            switch (actionID)
            {
                case 7421 when NoClippy.Config.PredictInstantCasts: // Triplecast
                    SetPredictedStatus(1211, 3, 0);
                    break;
                case 7561 when NoClippy.Config.PredictInstantCasts: // Swiftcast
                    SetPredictedStatus(167, 0, 0);
                    break;
                //case 7383 when NoClippy.Config.PredictInstantCasts && DalamudApi.ClientState.LocalPlayer?.Level >= 78: // Requiescat
                //    SetPredictedStatus(1369, 0, 0);
                //    break;
                //case 23913 when NoClippy.Config.PredictInstantCasts: // Lost Chainspell
                //    SetPredictedStatus(2560, 0, 0);
                //    break;
                // Firestarter?
            }
        }

        public unsafe void Update()
        {
            if (predictedStatus == 0) return;

            // Status arrived, stop predicting
            if (DalamudApi.ClientState.LocalPlayer is not { } p || p.StatusList.Any(s => s.StatusId == predictedStatus && s.RemainingTime > 0))
            {
                predictedStatus = 0;
                timeRemaining = 0;
                return;
            }

            var remove = timeRemaining > 0 && (timeRemaining -= (float)DalamudApi.Framework.UpdateDelta.TotalSeconds) <= 0;
            var hasFreeStatus = TryGetFreeStatus(p.StatusList, out var statusPtr);
            if (!remove)
            {
                if (hasFreeStatus)
                    ApplyStatus(statusPtr, predictedStatus, predictedStatusStacks, predictedStatusParam);
            }
            else
            {
                if (hasFreeStatus && *(ushort*)statusPtr == predictedStatus) // Probably not needed to check status ID, but just in case
                    ApplyStatus(statusPtr, 0, 0, 0);

                predictedStatus = 0;
                timeRemaining = 0;
            }
        }

        public override void DrawConfig()
        {
            ImGui.Columns(2, null, false);

            if (ImGui.Checkbox("Predict Instant Casts", ref NoClippy.Config.PredictInstantCasts))
                NoClippy.Config.Save();
            PluginUI.SetItemTooltip("Removes the effects of lag on Swiftcast and Triplecast.");

            ImGui.Columns(1);
        }

        public override void Enable()
        {
            Game.OnUseActionLocation += UseActionLocation;
            Game.OnUpdate += Update;
        }

        public override void Disable()
        {
            Game.OnUseActionLocation -= UseActionLocation;
            Game.OnUpdate -= Update;
        }
    }
}
