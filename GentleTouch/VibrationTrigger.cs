using System;
using Newtonsoft.Json;

namespace GentleTouch
{
    public abstract class VibrationTrigger
    {
        public Guid PatternGuid;
        [JsonIgnore] internal VibrationPattern Pattern;
        [JsonIgnore] internal bool ShouldBeTriggered;
    }
    public class VibrationCooldownTrigger: VibrationTrigger
    {
        public string ActionName;
        public int JobId;
        public int ActionId;
        public int ActionCooldownGroup;
        public int Priority;
        //public Guid PatternGuid;
        //[JsonIgnore] public VibrationPattern Pattern;

          
        public VibrationCooldownTrigger(int jobId, string actionName, int actionId, int actionCooldownGroup, int priority, Guid patternGuid) =>
            (JobId, ActionName, ActionId, ActionCooldownGroup, Priority, PatternGuid)
            = (jobId, actionName, actionId, actionCooldownGroup, priority, patternGuid);
      
        internal VibrationCooldownTrigger(int jobId, string actionName, int actionId, int actionCooldownGroup, int priority,
          VibrationPattern pattern) =>
          (JobId, ActionName, ActionId, ActionCooldownGroup, Priority, Pattern, PatternGuid)
          = (JobId, actionName, actionId, actionCooldownGroup, priority, pattern, pattern.Guid);
    }
}