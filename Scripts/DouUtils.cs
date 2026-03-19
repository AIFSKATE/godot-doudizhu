using System.Collections.Generic;

// 对应 utils.py
public static class DouUtils
{
    public const int Type0Pass = 0;
    public const int Type1Single = 1;
    public const int Type2Pair = 2;
    public const int Type3Triple = 3;
    public const int Type4Bomb = 4;
    public const int Type5KingBomb = 5;
    public const int Type631 = 6;
    public const int Type732 = 7;
    public const int Type8SerialSingle = 8;
    public const int Type9SerialPair = 9;
    public const int Type10SerialTriple = 10;
    public const int Type11Serial31 = 11;
    public const int Type12Serial32 = 12;
    public const int Type1342 = 13;
    public const int Type14422 = 14;
    public const int Type15Wrong = 15;

    public const int MinSingleCards = 5;
    public const int MinPairs = 3;
    public const int MinTriples = 2;

    // 对应 Python 的 itertools.combinations
    public static List<List<T>> Combinations<T>(List<T> list, int n)
    {
        var result = new List<List<T>>();
        CombineRecursive(list, n, 0, new List<T>(), result);
        return result;
    }

    private static void CombineRecursive<T>(List<T> list, int n, int start, List<T> current, List<List<T>> result)
    {
        if (current.Count == n)
        {
            result.Add(new List<T>(current));
            return;
        }
        for (int i = start; i < list.Count; i++)
        {
            current.Add(list[i]);
            CombineRecursive(list, n, i + 1, current, result);
            current.RemoveAt(current.Count - 1);
        }
    }
}