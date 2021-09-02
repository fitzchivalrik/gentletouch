using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;

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
        private readonly Func<int> _getMaxAetherCurrentSenseDistanceSquared;
        private readonly Func<PlayerCharacter?> _getLocalPlayer;
        private readonly Func<ObjectTable> _getObjects;
        
        internal static AetherCurrentTrigger 
            CreateAetherCurrentTrigger(Func<int> getMaxAetherCurrentSenseDistance, Func<PlayerCharacter?> getLocalPlayer, Func<ObjectTable> getObjects) =>
            new(
                getMaxAetherCurrentSenseDistance, getLocalPlayer, getObjects,
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
            (Func<int> getMaxAetherCurrentSenseDistance, Func<PlayerCharacter?> getLocalPlayer, Func<ObjectTable> getObjects,
            int priority, VibrationPattern pattern)
            : base(priority, pattern) =>
            (_getMaxAetherCurrentSenseDistanceSquared, _getLocalPlayer, _getObjects) 
            = (getMaxAetherCurrentSenseDistance, getLocalPlayer, getObjects);
        

        protected internal override IEnumerator<VibrationPattern.Step?> GetEnumerator()
        {
            if (_enumerator is not null) return _enumerator;
            _enumerator = CreateEnumerator();
            return _enumerator;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe byte ReadByte(IntPtr ptr, int ofs) => *(byte*) (ptr + ofs);
        
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

                
                // NOTE (Chiv) This is faster then using LINQ (~0.0060ms)
                var distanceSquared = float.MaxValue;
                foreach (var a in _getObjects())
                {
                    
                    // NOTE (Chiv) ActorTable.GetEnumerator() checks for null
                    if (a.ObjectKind == ObjectKind.EventObj 
                        && _aetherCurrentNameWhitelist.Contains(a.Name.TextValue)
                        // NOTE: This byte is SET(!=0) if _invisible_ i.e. if the player already collected
                        &&  ReadByte(a.Address, 0x105)  == 0)
                    {
                        var d = Vector3.DistanceSquared(localPlayer.Position, a.Position);
                        if (d < distanceSquared)
                            distanceSquared = d;
                    }
                }

                if (distanceSquared > _getMaxAetherCurrentSenseDistanceSquared())
                {
                    yield return null;
                    continue;
                }
                var nextTimeStep = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 200L;
                yield return Pattern.Steps[0];
                while (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() < nextTimeStep)
                    yield return null;
                // Silence after the vibration depends on distance to Aether Current
                var msTillNextStep = Math.Max((long) (800L * (distanceSquared / _getMaxAetherCurrentSenseDistanceSquared())),
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