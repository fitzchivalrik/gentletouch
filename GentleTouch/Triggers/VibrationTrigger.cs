using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace GentleTouch.Triggers;

public abstract class VibrationTrigger
{
    [JsonIgnore] internal VibrationPattern Pattern = null!;
    [JsonIgnore] internal bool             ShouldBeTriggered;

    public Guid PatternGuid;
    public int  Priority;

    internal VibrationTrigger(int priority, VibrationPattern pattern) => (Priority, PatternGuid, Pattern) = (priority, pattern.Guid, pattern);

    internal VibrationTrigger(int priority, Guid patternGuid) => (Priority, PatternGuid) = (priority, patternGuid);

    protected internal virtual IEnumerator<VibrationPattern.Step?> GetEnumerator()
    {
        return Pattern.GetEnumerator();
    }
}