using System;
using System.Collections.Generic;
using System.Linq;

// 对应 move_selector.py
public static class MoveSelector
{
    private static List<List<int>> CommonHandle(List<List<int>> moves, List<int> rival, int rankIdx)
    {
        var result = new List<List<int>>();
        rival.Sort();
        foreach (var m in moves)
        {
            m.Sort();
            if (m[rankIdx] > rival[rankIdx]) result.Add(m);
        }
        return result;
    }

    public static List<List<int>> FilterType1Single(List<List<int>> m, List<int> r) => CommonHandle(m, r, 0);
    public static List<List<int>> FilterType2Pair(List<List<int>> m, List<int> r) => CommonHandle(m, r, 0);
    public static List<List<int>> FilterType3Triple(List<List<int>> m, List<int> r) => CommonHandle(m, r, 0);
    public static List<List<int>> FilterType4Bomb(List<List<int>> m, List<int> r) => CommonHandle(m, r, 0);
    public static List<List<int>> FilterType631(List<List<int>> m, List<int> r) => CommonHandle(m, r, 1);
    public static List<List<int>> FilterType732(List<List<int>> m, List<int> r) => CommonHandle(m, r, 2);
    public static List<List<int>> FilterType8SerialSingle(List<List<int>> m, List<int> r) => CommonHandle(m, r, 0);
    public static List<List<int>> FilterType9SerialPair(List<List<int>> m, List<int> r) => CommonHandle(m, r, 0);
    public static List<List<int>> FilterType10SerialTriple(List<List<int>> m, List<int> r) => CommonHandle(m, r, 0);
    public static List<List<int>> FilterTypeSerialTripleWing(List<List<int>> m, List<int> r)
    {
        int rRank = r.GroupBy(c => c).Where(g => g.Count() == 3).Max(g => g.Key);
        return m.Where(x => x.GroupBy(c => c).Where(g => g.Count() == 3).Max(g => g.Key) > rRank).ToList();
    }
    public static List<List<int>> FilterType1342(List<List<int>> m, List<int> r) => CommonHandle(m, r, 2);
    public static List<List<int>> FilterType14422(List<List<int>> moves, List<int> rival)
    {
        int rRank = rival.GroupBy(c => c).First(g => g.Count() == 4).Key;
        return moves.Where(m => m.GroupBy(c => c).First(g => g.Count() == 4).Key > rRank).ToList();
    }
}