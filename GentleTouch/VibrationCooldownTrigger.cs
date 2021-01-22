using Newtonsoft.Json;

namespace GentleTouch
{
    public class VibrationCooldownTrigger
    {
        public string ActionName;
        public int ActionId;
        public int ActionCooldownGroup;
        public int Priority;
        public VibrationPattern Pattern;

        [JsonIgnore] internal bool ShouldBeTriggered;  
      
      public VibrationCooldownTrigger(string actionName, int actionId, int actionCooldownGroup, int priority,
          VibrationPattern pattern) =>
          (ActionName, ActionId, ActionCooldownGroup, Priority, Pattern)
          = (actionName, actionId, actionCooldownGroup, priority, pattern);
    }
}