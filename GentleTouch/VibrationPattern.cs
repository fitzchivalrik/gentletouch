using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
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

        internal IEnumerable<Step> Steps { get; init; }
        internal int Cycles { get; init; } = 1;

        internal IEnumerator<Step?> GetEnumerator()
        {
            var nextTimeStep = 0L;

            for (var i = 0; i < Cycles; i++)
            {
                foreach (var s in Steps)
                {
                    while (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() < nextTimeStep)
                        yield return null;
                    yield return s;
                    nextTimeStep = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + s.MillisecondsTillNextStep;
                }
                while (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() < nextTimeStep)
                    yield return null;
            }
        }

        internal async IAsyncEnumerator<Step> GetAsyncEnumerator(CancellationToken token)
        {
            for (var i = 0; i < Cycles; i++)
            {
                foreach (var s in Steps)
                {
                    yield return s;
                    await Task.Delay(s.MillisecondsTillNextStep, token);
                }
            }
        } 
    }
    
    
}