using System;
using System.Collections.Generic;
using Dalamud.Plugin;
using Newtonsoft.Json;

namespace GentleTouch
{
    public abstract class VibrationTrigger
    {
        public int Priority;
        public Guid PatternGuid;
        [JsonIgnore] internal bool ShouldBeTriggered;
        [JsonIgnore] internal VibrationPattern Pattern = null!;

        internal VibrationTrigger(int priority, VibrationPattern pattern)
        {
            (Priority, PatternGuid, Pattern) = (priority, pattern.Guid, pattern);
        }

        internal VibrationTrigger(int priority, Guid patternGuid)
        {
            (Priority, PatternGuid) = (priority, patternGuid);
        }

        protected internal virtual IEnumerator<VibrationPattern.Step?> GetEnumerator()
        {
            return Pattern.GetEnumerator();
        }
    }

    public class VibrationCooldownTrigger : VibrationTrigger
    {
        public const int GCDCooldownGroup = 58;
        public int ActionCooldownGroup;
        public uint ActionId;
        public string ActionName;
        public uint JobId;


        // NOTE (Chiv) Used by NewtonSoft (or is it?)
        // TODO (Chiv) Maybe write custom Serializer?
        public VibrationCooldownTrigger(uint jobId, string actionName, uint actionId, int actionCooldownGroup,
            int priority, Guid patternGuid) : base(priority, patternGuid)
        {
            (JobId, ActionName, ActionId, ActionCooldownGroup)
                = (jobId, actionName, actionId, actionCooldownGroup);
        }

        internal VibrationCooldownTrigger(uint jobId, string actionName, uint actionId, int actionCooldownGroup,
            int priority,
            VibrationPattern pattern) : base(priority, pattern)
        {
            (JobId, ActionName, ActionId, ActionCooldownGroup)
                = (jobId, actionName, actionId, actionCooldownGroup);
        }
    }
    
}