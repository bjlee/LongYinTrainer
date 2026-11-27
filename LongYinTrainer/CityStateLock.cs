using System;
using System.Collections.Generic;

// Session locks are bound to both the world and the particular AreaData
// instance, so identical city IDs in another save never inherit a lock.
internal static class CityStateLock
{
    private sealed class Entry
    {
        internal IntPtr Pointer;
        internal float[] Values = Array.Empty<float>();
    }
    private static readonly Dictionary<int, Entry> Locks = new();
    private static IntPtr _world;
    private static double _nextPoll;
    internal static bool HasLocks => Locks.Count != 0;

    internal static float[] Read(AreaData area) => new[] { area.safe, area.support, area.defence, area.people, area.maxPeople };
    internal static void Validate(float[] values)
    {
        if (values.Length != 5) throw new ArgumentException("도시 속성 개수가 잘못되었습니다.");
        for (var i = 0; i < 5; i++)
            if (!float.IsFinite(values[i]) || values[i] < 0 || values[i] > (i < 3 ? 100 : 1000000000))
                throw new ArgumentException(i < 3 ? "치안, 민심, 성방은 0–100이어야 합니다." : "인구와 상한은 0–10억 사이의 유한한 숫자여야 합니다.");
        if (values[4] < 1 || values[3] > values[4]) throw new ArgumentException("인구 상한은 1 이상이고 현재 인구보다 작을 수 없습니다.");
    }
    internal static void Clear() { Locks.Clear(); _world = IntPtr.Zero; _nextPoll = 0; }
    // Native mutation hooks enforce immediately. Direct field writes only need
    // a bounded fallback, never a full native lookup per locked city per frame.
    internal static void Poll(WorldData? world, double now)
    {
        if (!HasLocks || !Matches(world)) return;
        if (now < _nextPoll) return;
        _nextPoll = now + 0.5;
        Enforce(world);
    }
    private static bool Matches(WorldData? world)
    {
        if (world == null || world.Pointer != _world) { Clear(); return false; }
        return true;
    }
    internal static bool IsLocked(WorldData? world, AreaData area)
    {
        if (!Matches(world) || !Locks.TryGetValue(area.areaID, out var entry)) return false;
        if (entry.Pointer == area.Pointer) return true;
        Locks.Remove(area.areaID);
        return false;
    }
    private static void CheckTarget(WorldData world, AreaData area)
    {
        if (world.GetArea(area.areaID)?.Pointer != area.Pointer)
            throw new InvalidOperationException("도시 데이터가 바뀌었습니다. 도시 페이지를 다시 여세요.");
    }
    internal static void Apply(WorldData world, AreaData area, float[] values, bool lockValues)
    {
        CheckTarget(world, area);
        Validate(values); // Validate everything before changing any game data.
        var keepLock = IsLocked(world, area) || lockValues;
        Write(area, values);
        if (keepLock)
        {
            _world = world.Pointer;
            Locks[area.areaID] = new Entry { Pointer = area.Pointer, Values = (float[])values.Clone() };
        }
    }
    internal static void Unlock(WorldData world, AreaData area)
    {
        if (Matches(world)) Locks.Remove(area.areaID);
    }
    private static void Write(AreaData area, float[] v)
    {
        if (area.safe == v[0] && area.support == v[1] && area.defence == v[2] && area.people == v[3] && area.maxPeople == v[4]) return;
        area.maxPeople = v[4]; area.people = v[3];
        area.safe = v[0]; area.support = v[1]; area.defence = v[2];
        area.areaInfoDirty = true; area.areaDetailDirty = true;
    }
    internal static void Enforce(WorldData? world, AreaData? changed = null)
    {
        if (Locks.Count == 0 || !Matches(world)) return;
        try
        {
            if (changed != null)
            {
                if (!Locks.TryGetValue(changed.areaID, out var entry)) return;
                if (entry.Pointer != changed.Pointer || world!.GetArea(changed.areaID)?.Pointer != changed.Pointer) { Locks.Remove(changed.areaID); return; }
                Write(changed, entry.Values);
                return;
            }
            List<int>? stale = null;
            foreach (var pair in Locks)
            {
                var area = world!.GetArea(pair.Key);
                if (area == null || area.Pointer != pair.Value.Pointer) (stale ??= new List<int>()).Add(pair.Key);
                else Write(area, pair.Value.Values);
            }
            if (stale != null) foreach (var id in stale) Locks.Remove(id);
        }
        catch (Exception ex)
        {
            Clear();
            LongYinTrainerPlugin.Logger.LogWarning("도시 잠금 해제됨: " + ex.Message);
        }
    }
}
