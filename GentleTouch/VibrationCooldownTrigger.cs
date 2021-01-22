using Newtonsoft.Json;

namespace GentleTouch
{
    internal class VibrationCooldownTrigger
    {
      internal string ActionName { get; init; }    
      internal int ActionId { get; init; }    
      internal int ActionCooldownGroup { get; init; }    
      internal int Priority { get; init; }    
      internal VibrationPattern Pattern { get; init; }

      [JsonIgnore] internal bool ShouldBeTriggered { get; set; } = false;  
      
      internal VibrationCooldownTrigger(string actionName, int actionId, int actionCooldownGroup, int priority,
          VibrationPattern pattern) =>
          (ActionName, ActionId, ActionCooldownGroup, Priority, Pattern)
          = (actionName, actionId, actionCooldownGroup, priority, pattern);
    }
}