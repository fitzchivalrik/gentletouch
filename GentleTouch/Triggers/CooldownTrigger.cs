using System;

namespace GentleTouch.Triggers;

public class CooldownTrigger : VibrationTrigger
{
    public const int    GCDCooldownGroup = 58;
    public       int    ActionCooldownGroup;
    public       uint   ActionId;
    public       string ActionName;
    public       uint   JobId;


    // NOTE (Chiv) Used by NewtonSoft (or is it?)
    // TODO (Chiv) Maybe write custom Serializer?
    public CooldownTrigger(uint jobId, string actionName, uint actionId, int actionCooldownGroup, int priority, Guid patternGuid) :
        base(priority, patternGuid) =>
        (JobId, ActionName, ActionId, ActionCooldownGroup)
        = (jobId, actionName, actionId, actionCooldownGroup);

    internal CooldownTrigger(uint jobId, string actionName, uint actionId, int actionCooldownGroup, int priority, VibrationPattern pattern) : base(priority
      , pattern) =>
        (JobId, ActionName, ActionId, ActionCooldownGroup)
        = (jobId, actionName, actionId, actionCooldownGroup);
}