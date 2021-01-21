using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace GentleTouch
{
    public record VibrationPattern
    {
        public record Step (
            ushort LeftMotorPercentage,
            ushort RightMotorPercentage,
            ushort MillisecondsTillNextStep = 100);

        public IEnumerable<Step> Steps { get; init; }
        public int Cycles { get; init; } = 1;
        public Guid Guid { get; init; } = Guid.NewGuid();
        public string Name { get; init; } = "Nameless";
        public bool Infinite { get; init; } = false;
        
        internal IEnumerator<Step?> GetEnumerator()
        {
            var nextTimeStep = 0L;
            for (var i = 0; Infinite || i < Cycles; i++)
            {
                foreach (var s in Steps)
                {
                    while (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() < nextTimeStep)
                        yield return null;
                    nextTimeStep = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + s.MillisecondsTillNextStep;
                    yield return s;
                }
                while (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() < nextTimeStep)
                    yield return null;
            }
        }
    }
    
    
}