using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace GentleTouch
{
    public class VibrationPattern
    {
        public class Step
        {
            public int LeftMotorPercentage;
            public int RightMotorPercentage;
            public int MillisecondsTillNextStep;
            public Step(
                int leftMotorPercentage,
                int rightMotorPercentage,
                int millisecondsTillNextStep = 100) =>
            (LeftMotorPercentage, RightMotorPercentage, MillisecondsTillNextStep)
                = (leftMotorPercentage, rightMotorPercentage, millisecondsTillNextStep);
            
        };

        public IList<Step> Steps = new List<Step>();
        public int Cycles = 1;
        public readonly Guid Guid = Guid.NewGuid();
        public string Name = "Nameless";
        public bool Infinite;
        
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