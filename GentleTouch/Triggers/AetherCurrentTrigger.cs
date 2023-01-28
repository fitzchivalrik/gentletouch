using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using Dalamud.Game.ClientState.Objects;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;

namespace GentleTouch.Triggers;

internal class AetherCurrentTrigger : VibrationTrigger
{
    private readonly HashSet<string> _aetherCurrentNameWhitelist = new()
    {
        "Aether Current", "Windätherquelle", "Vent éthéré", "風脈の泉"
    };

    // TODO (Chiv) Change to Get/Set and update on config change?
    private readonly Func<int>   _getMaxAetherCurrentSenseDistanceSquared;
    private readonly ObjectTable _objects;

    private IEnumerator<VibrationPattern.Step?>? _enumerator;

    private AetherCurrentTrigger(Func<int> getMaxAetherCurrentSenseDistance, ObjectTable objects, int priority, VibrationPattern pattern)
        : base(priority, pattern) =>
        (_getMaxAetherCurrentSenseDistanceSquared, _objects)
        = (getMaxAetherCurrentSenseDistance, objects);

    internal static AetherCurrentTrigger CreateAetherCurrentTrigger(
        Func<int>   getMaxAetherCurrentSenseDistance
      , ObjectTable objects
    )
    {
        return new AetherCurrentTrigger(
            getMaxAetherCurrentSenseDistance, objects,
            -1, new VibrationPattern
            {
                Infinite = true, Name = "AetherSense", Steps = new VibrationPattern.Step[]
                {
                    new(15, 15), new(0, 0)
                }
            }
        );
    }


    protected internal override IEnumerator<VibrationPattern.Step?> GetEnumerator()
    {
        if (_enumerator is not null) return _enumerator;
        _enumerator = CreateEnumerator();
        return _enumerator;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe byte ReadByte(nint ptr, int ofs)
    {
        return *(byte*)(ptr + ofs);
    }


    private unsafe float DistanceToClosestAetherCurrent()
    {
        // Method does not get called if local player is null.
        var localPlayer = (GameObject*)_objects.GetObjectAddress(0);
        var localPlayerPosition =
            new Vector3(localPlayer->Position.X, localPlayer->Position.Y, localPlayer->Position.Z);
        var distanceSquared = float.MaxValue;
        for (var i = 1; i < _objects.Length; i++)
        {
            var address = _objects.GetObjectAddress(i);
            if (address == nint.Zero)
            {
                continue;
            }

            var obj     = (GameObject*)address;
            var objKind = (ObjectKind)obj->ObjectKind;
            if (objKind == ObjectKind.EventObj && obj->GetIsTargetable())
            {
                var objName = GetName(obj);
                if (!_aetherCurrentNameWhitelist.Contains(objName)) continue;
                var objectPosition =
                    new Vector3(obj->Position.X, obj->Position.Y, obj->Position.Z);
                var d = Vector3.DistanceSquared(localPlayerPosition, objectPosition);
                if (d < distanceSquared)
                {
                    distanceSquared = d;
                }
            }
        }

        return distanceSquared;
    }

    private static unsafe string GetName(GameObject* gameObject)
    {
        var length      = 0;
        var currentByte = gameObject->Name;
        while (*currentByte != 0)
        {
            currentByte++;
            length++;
        }

        return Encoding.UTF8.GetString(gameObject->Name, length);
    }

    private IEnumerator<VibrationPattern.Step?> CreateEnumerator()
    {
        while (true)
        {
            if (_objects.GetObjectAddress(0) == nint.Zero)
            {
                yield return null;
                continue;
            }

            var distanceSquared = DistanceToClosestAetherCurrent();

            if (distanceSquared > _getMaxAetherCurrentSenseDistanceSquared())
            {
                yield return null;
                continue;
            }

            var nextTimeStep = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 200L;
            yield return Pattern.Steps[0];
            while (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() < nextTimeStep)
            {
                yield return null;
            }

            // Silence after the vibration depends on distance to Aether Current
            var msTillNextStep = Math.Max((long)(800L * (distanceSquared / _getMaxAetherCurrentSenseDistanceSquared())),
                10L);
            nextTimeStep = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + msTillNextStep;
            yield return Pattern.Steps[1];
            while (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() < nextTimeStep)
            {
                yield return null;
            }
        }
        // ReSharper disable once IteratorNeverReturns
    }
}