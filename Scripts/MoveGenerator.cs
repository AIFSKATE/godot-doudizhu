using System;
using System.Collections.Generic;
using System.Linq;

// 对应 move_generator.py
public class MoveGenerator
{
    private List<int> _cards;
    private Dictionary<int, int> _dict;

    public MoveGenerator(List<int> cards)
    {
        _cards = new List<int>(cards);
        _cards.Sort();
        _dict = _cards.GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());
    }

    public List<List<int>> GenSerialMoves(List<int> distinct, int minLen, int repeat, int repeatNum = 0)
    {
        var result = new List<List<int>>();
        var sorted = distinct.OrderBy(c => c).ToList();
        for (int i = 0; i < sorted.Count; i++)
        {
            int targetLen = repeatNum > 0 ? repeatNum : minLen;
            for (int len = targetLen; i + len <= sorted.Count; len++)
            {
                var sub = sorted.GetRange(i, len);
                if (MoveDetector.IsContinuousSeq(sub))
                {
                    var move = new List<int>();
                    foreach (var c in sub) for (int r = 0; r < repeat; r++) move.Add(c);
                    result.Add(move);
                    if (repeatNum > 0) break;
                }
                else break;
            }
        }
        return result;
    }

    public List<List<int>> GenType1Single() => _dict.Keys.Select(k => new List<int> { k }).ToList();
    public List<List<int>> GenType2Pair() => _dict.Where(kv => kv.Value >= 2).Select(kv => new List<int> { kv.Key, kv.Key }).ToList();
    public List<List<int>> GenType3Triple() => _dict.Where(kv => kv.Value >= 3).Select(kv => new List<int> { kv.Key, kv.Key, kv.Key }).ToList();
    public List<List<int>> GenType4Bomb() => _dict.Where(kv => kv.Value == 4).Select(kv => new List<int> { kv.Key, kv.Key, kv.Key, kv.Key }).ToList();
    public List<List<int>> GenType5KingBomb() => (_cards.Contains(20) && _cards.Contains(30)) ? new List<List<int>> { new List<int> { 20, 30 } } : new List<List<int>>();

    public List<List<int>> GenType6ThreeOne()
    {
        var res = new List<List<int>>();
        var t3 = GenType3Triple();
        var t1 = GenType1Single();
        foreach (var tri in t3)
            foreach (var s in t1) if (s[0] != tri[0]) res.Add(tri.Concat(s).ToList());
        return res;
    }

    public List<List<int>> GenType7ThreeTwo()
    {
        var res = new List<List<int>>();
        var t3 = GenType3Triple();
        var t2 = GenType2Pair();
        foreach (var tri in t3)
            foreach (var p in t2) if (p[0] != tri[0]) res.Add(tri.Concat(p).ToList());
        return res;
    }

    public List<List<int>> GenType8SerialSingle(int repeatNum = 0) => GenSerialMoves(_dict.Keys.ToList(), 5, 1, repeatNum);
    public List<List<int>> GenType9SerialPair(int repeatNum = 0) => GenSerialMoves(_dict.Where(kv => kv.Value >= 2).Select(k => k.Key).ToList(), 3, 2, repeatNum);
    public List<List<int>> GenType10SerialTriple(int repeatNum = 0) => GenSerialMoves(_dict.Where(kv => kv.Value >= 3).Select(k => k.Key).ToList(), 2, 3, repeatNum);

    public List<List<int>> GenType11Serial31(int repeatNum = 0)
    {
        var res = new List<List<int>>();
        var s3 = GenType10SerialTriple(repeatNum);
        foreach (var body in s3)
        {
            var bodySet = body.Distinct().ToList();
            var remain = _cards.Where(c => !bodySet.Contains(c)).ToList();
            if (remain.Count >= bodySet.Count)
            {
                foreach (var w in DouUtils.Combinations(remain, bodySet.Count))
                    res.Add(body.Concat(w).OrderBy(x => x).ToList());
            }
        }
        return res.GroupBy(m => string.Join(",", m)).Select(g => g.First().ToList()).ToList();
    }

    public List<List<int>> GenType12Serial32(int repeatNum = 0)
    {
        var res = new List<List<int>>();
        var s3 = GenType10SerialTriple(repeatNum);
        foreach (var body in s3)
        {
            var bodySet = body.Distinct().ToList();
            var pCands = _dict.Where(kv => kv.Value >= 2 && !bodySet.Contains(kv.Key)).Select(k => k.Key).ToList();
            if (pCands.Count >= bodySet.Count)
            {
                foreach (var w in DouUtils.Combinations(pCands, bodySet.Count))
                {
                    var m = new List<int>(body);
                    foreach (var p in w) { m.Add(p); m.Add(p); }
                    res.Add(m.OrderBy(x => x).ToList());
                }
            }
        }
        return res;
    }

    public List<List<int>> GenType1342()
    {
        var res = new List<List<int>>();
        var fours = _dict.Where(kv => kv.Value == 4).Select(k => k.Key).ToList();
        foreach (var f in fours)
        {
            var remain = _cards.Where(c => c != f).ToList();
            foreach (var w in DouUtils.Combinations(remain, 2))
                res.Add(new List<int> { f, f, f, f }.Concat(w).OrderBy(x => x).ToList());
        }
        return res.GroupBy(m => string.Join(",", m)).Select(g => g.First().ToList()).ToList();
    }

    public List<List<int>> GenType14422()
    {
        var res = new List<List<int>>();
        var fours = _dict.Where(kv => kv.Value == 4).Select(k => k.Key).ToList();
        var pairs = _dict.Where(kv => kv.Value >= 2).Select(k => k.Key).ToList();
        foreach (var f in fours)
        {
            var pCands = pairs.Where(p => p != f).ToList();
            foreach (var combo in DouUtils.Combinations(pCands, 2))
                res.Add(new List<int> { f, f, f, f, combo[0], combo[0], combo[1], combo[1] }.OrderBy(x => x).ToList());
        }
        return res;
    }

    public List<List<int>> GenAllMoves()
    {
        var all = new List<List<int>>();
        all.AddRange(GenType1Single());
        all.AddRange(GenType2Pair());
        all.AddRange(GenType3Triple());
        all.AddRange(GenType4Bomb());
        all.AddRange(GenType5KingBomb());
        all.AddRange(GenType6ThreeOne());
        all.AddRange(GenType7ThreeTwo());
        all.AddRange(GenType8SerialSingle());
        all.AddRange(GenType9SerialPair());
        all.AddRange(GenType10SerialTriple());
        all.AddRange(GenType11Serial31());
        all.AddRange(GenType12Serial32());
        all.AddRange(GenType1342());
        all.AddRange(GenType14422());
        return all;
    }
}