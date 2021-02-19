using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Game.ClientState.Actors;
using Dalamud.Game.ClientState.Actors.Types;

namespace GentleTouch.Triggers
{
    internal class AetherCurrentTrigger: VibrationTrigger
    {
        private readonly HashSet<string> _aetherCurrentNameWhitelist = new()
        {
            "Aether Current",
            "Windätherquelle",
            "Vent éthéré",
            "風脈の泉"
        };
        private IEnumerator<VibrationPattern.Step?>? _enumerator;
        private readonly Func<int> _getMaxAetherCurrentSenseDistance;
        private readonly Func<PlayerCharacter?> _getLocalPlayer;
        private readonly Func<ActorTable> _getActors;
        
        internal static AetherCurrentTrigger 
            CreateAetherCurrentTrigger(Func<int> getMaxAetherCurrentSenseDistance, Func<PlayerCharacter?> getLocalPlayer, Func<ActorTable> getActors) =>
            new(
                getMaxAetherCurrentSenseDistance, getLocalPlayer, getActors,
                -1, new VibrationPattern()
            {
                Infinite = true,
                Name = "AetherSense",
                Steps = new VibrationPattern.Step[]
                {
                    new(15, 15),
                    new(0, 0)
                }
            }
            );

        private AetherCurrentTrigger
            (Func<int> getMaxAetherCurrentSenseDistance, Func<PlayerCharacter?> getLocalPlayer, Func<ActorTable> getActors,
            int priority, VibrationPattern pattern)
            : base(priority, pattern) =>
            (_getMaxAetherCurrentSenseDistance, _getLocalPlayer, _getActors) 
            = (getMaxAetherCurrentSenseDistance, getLocalPlayer, getActors);
        

        protected internal override IEnumerator<VibrationPattern.Step?> GetEnumerator()
        {
            if (_enumerator is not null) return _enumerator;
            _enumerator = CreateEnumerator();
            return _enumerator;
        }

        private IEnumerator<VibrationPattern.Step?> CreateEnumerator()
        {
            while (true)
            {
                var localPlayer = _getLocalPlayer();
                if (localPlayer is null)
                {
                    yield return null;
                    continue;
                }
                
                var distance = (
                    from Actor a in _getActors()
                    where a is not null
                        && a.ObjectKind == ObjectKind.EventObj
                        && _aetherCurrentNameWhitelist.Contains(Encoding.UTF8.GetString(Encoding.Default.GetBytes(a.Name)))
                        // TODO Change back to ==0 after testing before release
                        // NOTE: This byte is SET(!=0) if _invisible_ i.e. if the player already collected
                        && Marshal.ReadByte(a.Address, 0x105) != 0
                    select (float?) Math.Sqrt(Math.Pow(localPlayer.Position.X - a.Position.X, 2)
                                              + Math.Pow(localPlayer.Position.Y - a.Position.Y, 2)
                                              + Math.Pow(localPlayer.Position.Z - a.Position.Z, 2))
                ).Min() ?? float.MaxValue;
                if (distance > _getMaxAetherCurrentSenseDistance())
                {
                    yield return null;
                    continue;
                }
                var nextTimeStep = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 200L;
                yield return Pattern.Steps[0];
                while (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() < nextTimeStep)
                    yield return null;
                // Silence after the vibration depends on distance to Aether Current
                var msTillNextStep = Math.Max((long) (800L * (distance / _getMaxAetherCurrentSenseDistance())),
                    10L);
                nextTimeStep = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + msTillNextStep;
                yield return Pattern.Steps[1];
                while (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() < nextTimeStep)
                    yield return null;
            }
            // ReSharper disable once IteratorNeverReturns
        }
    }
}