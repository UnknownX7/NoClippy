using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Statuses;
using ImGuiNET;
using Status = FFXIVClientStructs.FFXIV.Client.Game.Status;

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

        private class PredictedStatusList
        {
            private readonly List<PredictedStatus> statuses = new();

            public void Add(ushort statusID = 0, byte stacks = 0, byte param = 0, bool replace = false, float timer = 0.75f)
            {
                var statusList = DalamudApi.ClientState.LocalPlayer!.StatusList;
                if (statusList.Any(status => status.StatusId == statusID)) return;

                var prev = statuses.FindIndex(s => s.status.StatusID == statusID);
                if (prev >= 0)
                {
                    statuses[prev].TryRemove(statusList);
                    statuses.RemoveAt(prev);
                }

                statuses.Add(new PredictedStatus
                {
                    status = new Status
                    {
                        StatusID = statusID,
                        StackCount = stacks,
                        Param = param
                    },
                    timer = timer,
                    replace = replace
                });
            }

            public void Update(float dt)
            {
                var statusList = DalamudApi.ClientState.LocalPlayer?.StatusList;
                var exists = statusList != null;

                for (int i = statuses.Count - 1; i >= 0; i--)
                {
                    var status = statuses[i];
                    if ((status.timer -= dt) > 0) continue;

                    if (exists)
                        status.TryRemove(statusList);

                    statuses.RemoveAt(i);
                }
            }

            public unsafe void Apply(StatusList statusList)
            {
                if (statuses.Count == 0) return;
                var currentIndex = 0;
                for (short i = 0; i < statusList.Length; i++)
                {
                    var statusPtr = (Status*)statusList.GetStatusAddress(i);
                    if (IsStatusValid(statusPtr)) continue;
                    statuses[currentIndex].Apply(statusPtr, i);
                    ++currentIndex;
                    if (statuses.Count == currentIndex) return;
                }

                // These statuses failed to find a free slot to apply
                for (int i = currentIndex; i < statuses.Count; i++)
                    statuses[i].currentSlot = -1;
            }

            public void CheckNewStatus(StatusList statusList, short slot, ushort statusID)
            {
                var reapply = false;
                for (int i = 0; i < statuses.Count; i++)
                {
                    var status = statuses[i];
                    var replaced = slot == status.currentSlot;
                    reapply = reapply || replaced;
                    if (statusID != status.status.StatusID) continue;

                    if (!replaced)
                        status.TryRemove(statusList);

                    statuses.RemoveAt(i);
                    break;
                }

                if (reapply)
                    Apply(DalamudApi.ClientState.LocalPlayer!.StatusList);
            }
        }

        private unsafe class PredictedStatus
        {
            public Status status = new();
            public float timer = 0;
            public bool replace = false;
            public short currentSlot = -1;

            public void Apply(Status* statusPtr, short slot)
            {
                statusPtr->StatusID = status.StatusID;
                statusPtr->StackCount = status.StackCount;
                statusPtr->Param = status.Param;
                currentSlot = slot;
            }

            public void TryRemove(StatusList statusList)
            {
                if (currentSlot < 0) return;
                var statusPtr = (Status*)statusList.GetStatusAddress(currentSlot);
                if (statusPtr->StatusID != status.StatusID || statusPtr->Param != status.Param || statusPtr->StackCount != status.StackCount) return;
                statusPtr->StatusID = 0;
                statusPtr->StackCount = 0;
                statusPtr->Param = 0;
                currentSlot = -1;
            }
        }

        private readonly PredictedStatusList predictedStatusList = new();

        private class StatusInfo
        {
            public ushort id = 0;
            public byte stacks = 0;
            public byte param = 0;
            public float timer = 0.75f;
            public bool replace = false;
        }

        private readonly Dictionary<uint, List<StatusInfo>> predictedStatuses = new()
        {
            [7421] = new() { new() { id = 1211, stacks = 3 } }, // Triplecast
            [7561] = new() { new() { id = 167 } }, // Swiftcast
            //[7383] = new() { new() { id = 1369 } }, // Requiescat
            //[23913] = new() { new() { id = 2560 } }, // Lost Chainspell
            // Firestarter?
        };

        // Length - 7 seems to be the last one with sourceID 0xE0000000?
        private static unsafe bool IsStatusValid(Status* statusPtr) => statusPtr->StatusID != 0 && (statusPtr->RemainingTime > 0 || statusPtr->SourceID is not (0 or 0xE0000000));

        private void UseActionLocation(IntPtr actionManager, uint actionType, uint actionID, long targetedActorID, IntPtr vectorLocation, uint param)
        {
            if (actionType != 1 || !predictedStatuses.TryGetValue(actionID, out var statuses)) return;

            foreach (var status in statuses)
                predictedStatusList.Add(status.id, status.stacks, status.param, status.replace, status.timer);

            predictedStatusList.Apply(DalamudApi.ClientState.LocalPlayer!.StatusList);
        }

        public void Update() => predictedStatusList.Update((float)DalamudApi.Framework.UpdateDelta.TotalSeconds);

        public void UpdateStatusList(StatusList statusList, short slot, ushort statusID, float remainingTime, ushort stackParam, uint sourceID)
        {
            if (slot < 0)
                predictedStatusList.Apply(statusList);
            else
                predictedStatusList.CheckNewStatus(statusList, slot, statusID);
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
            Game.OnUpdateStatusList += UpdateStatusList;
        }

        public override void Disable()
        {
            Game.OnUseActionLocation -= UseActionLocation;
            Game.OnUpdate -= Update;
            Game.OnUpdateStatusList -= UpdateStatusList;
        }
    }
}
