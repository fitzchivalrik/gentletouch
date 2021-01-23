using System;
using Newtonsoft.Json;

namespace GentleTouch
{
    public abstract class VibrationTrigger
    {
        [JsonIgnore] internal VibrationPattern Pattern = null!;
        public Guid PatternGuid;
        public int Priority;
        [JsonIgnore] internal bool ShouldBeTriggered;

        internal VibrationTrigger(int priority, VibrationPattern pattern)
        {
            (Priority, PatternGuid, Pattern) = (priority, pattern.Guid, pattern);
        }

        internal VibrationTrigger(int priority, Guid patternGuid)
        {
            (Priority, PatternGuid) = (priority, patternGuid);
        }
    }

    public class VibrationCooldownTrigger : VibrationTrigger
    {
        public int ActionCooldownGroup;
        public int ActionId;
        public string ActionName;
        public int JobId;


        // NOTE (Chiv) Used by NewtonSoft.
        // TODO (Chiv) Maybe write custom Serializer?
        public VibrationCooldownTrigger(int jobId, string actionName, int actionId, int actionCooldownGroup,
            int priority, Guid patternGuid) : base(priority, patternGuid)
        {
            (JobId, ActionName, ActionId, ActionCooldownGroup)
                = (jobId, actionName, actionId, actionCooldownGroup);
        }

        internal VibrationCooldownTrigger(int jobId, string actionName, int actionId, int actionCooldownGroup,
            int priority,
            VibrationPattern pattern) : base(priority, pattern)
        {
            (JobId, ActionName, ActionId, ActionCooldownGroup)
                = (jobId, actionName, actionId, actionCooldownGroup);
        }
    }
}