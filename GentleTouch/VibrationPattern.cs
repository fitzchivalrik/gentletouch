using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace GentleTouch
{
    internal record VibrationPattern
    {
        internal record Step (
            ushort LeftMotorPercentage,
            ushort RightMotorPercentage,
            ushort MillisecondsTillNextStep = 100
            );

        private IEnumerable<Step> Steps { get; init; }

        internal IEnumerator<Step?> GetEnumerator()
        {
            var nextTimeStep = 0L;
            foreach (var s in Steps)
            {
                if (s is null) yield break;
                while (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() < nextTimeStep)
                    yield return null;
                yield return s;
                nextTimeStep = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + s.MillisecondsTillNextStep;
            }
        }
    }
    
    
}