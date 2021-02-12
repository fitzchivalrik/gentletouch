using System;
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
    }

    public class VibrationCooldownTrigger : VibrationTrigger
    {
        public const int GCDCooldownGroup = 58;
        public int ActionCooldownGroup;
        public int ActionId;
        public string ActionName;
        public int JobId;


        // NOTE (Chiv) Used by NewtonSoft (or is it?)
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