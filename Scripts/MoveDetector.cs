using System;
using System.Collections.Generic;
using System.Linq;

// 对应 move_detector.py
public struct MoveResult
{
    public int Type;
    public int Rank;
    public int Len;
}

public static class MoveDetector
{
    public static bool IsContinuousSeq(List<int> move)
    {
        if (move.Count <= 1) return true;
        // 2(17) 和 王(20, 30) 不允许进入顺子逻辑
        if (move.Any(c => c == 17 || c >= 20)) return false;
        for (int i = 0; i < move.Count - 1; i++)
        {
            if (move[i + 1] - move[i] != 1) return false;
        }
        return true;
    }

    public static MoveResult GetMoveType(List<int> move)
    {
        int size = move.Count;
        if (size == 0) return new MoveResult { Type = DouUtils.Type0Pass };

        move.Sort();
        var moveDict = move.GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());
        var countDict = moveDict.GroupBy(kv => kv.Value).ToDictionary(g => g.Key, g => g.Count());

        if (size == 1) return new MoveResult { Type = DouUtils.Type1Single, Rank = move[0] };
        if (size == 2)
        {
            if (move[0] == move[1]) return new MoveResult { Type = DouUtils.Type2Pair, Rank = move[0] };
            if (move[0] == 20 && move[1] == 30) return new MoveResult { Type = DouUtils.Type5KingBomb };
            return new MoveResult { Type = DouUtils.Type15Wrong };
        }
        if (size == 3 && moveDict.Count == 1) return new MoveResult { Type = DouUtils.Type3Triple, Rank = move[0] };
        if (size == 4)
        {
            if (moveDict.Count == 1) return new MoveResult { Type = DouUtils.Type4Bomb, Rank = move[0] };
            if (moveDict.Count == 2 && countDict.GetValueOrDefault(3) == 1)
                return new MoveResult { Type = DouUtils.Type631, Rank = move[1] };
        }

        if (IsContinuousSeq(move)) return new MoveResult { Type = DouUtils.Type8SerialSingle, Rank = move[0], Len = size };
        if (size == 5 && moveDict.Count == 2 && countDict.GetValueOrDefault(3) == 1)
            return new MoveResult { Type = DouUtils.Type732, Rank = move[2] };

        if (size == 6 && countDict.GetValueOrDefault(4) == 1 && (countDict.GetValueOrDefault(2) == 1 || countDict.GetValueOrDefault(1) == 2))
            return new MoveResult { Type = DouUtils.Type1342, Rank = move[2] };

        if (size == 8 && ((countDict.GetValueOrDefault(4) == 1 && countDict.GetValueOrDefault(2) == 2) || countDict.GetValueOrDefault(4) == 2))
        {
            int maxRank = moveDict.Where(kv => kv.Value == 4).Max(kv => kv.Key);
            return new MoveResult { Type = DouUtils.Type14422, Rank = maxRank };
        }

        var keys = moveDict.Keys.OrderBy(k => k).ToList();
        if (moveDict.Values.All(v => v == 2) && IsContinuousSeq(keys))
            return new MoveResult { Type = DouUtils.Type9SerialPair, Rank = keys[0], Len = keys.Count };
        if (moveDict.Values.All(v => v == 3) && IsContinuousSeq(keys))
            return new MoveResult { Type = DouUtils.Type10SerialTriple, Rank = keys[0], Len = keys.Count };

        // 飞机复杂判定逻辑
        if (countDict.GetValueOrDefault(3, 0) >= DouUtils.MinTriples)
        {
            var s3 = moveDict.Where(kv => kv.Value == 3).Select(kv => kv.Key).OrderBy(k => k).ToList();
            if (IsContinuousSeq(s3))
            {
                int wings = size - s3.Count * 3;
                if (s3.Count == wings) return new MoveResult { Type = DouUtils.Type11Serial31, Rank = s3[0], Len = s3.Count };
                if (s3.Count == wings / 2 && moveDict.Values.All(v => v == 3 || v == 2))
                    return new MoveResult { Type = DouUtils.Type12Serial32, Rank = s3[0], Len = s3.Count };
            }
            // 对应 Python 的特殊处理：多出的三张充当单张翅膀
            else if (s3.Count == 4)
            {
                if (IsContinuousSeq(s3.GetRange(1, 3)) && size == 12) return new MoveResult { Type = DouUtils.Type11Serial31, Rank = s3[1], Len = 3 };
                if (IsContinuousSeq(s3.GetRange(0, 3)) && size == 12) return new MoveResult { Type = DouUtils.Type11Serial31, Rank = s3[0], Len = 3 };
            }
        }
        return new MoveResult { Type = DouUtils.Type15Wrong };
    }
}