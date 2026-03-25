using System;
using System.Collections.Generic;
using System.Linq;

// 对应 game.py
// Game 类是整个斗地主的核心“裁判”。它负责维护游戏规则、玩家手牌、历史记录，以及判断胜负。
public class Game
{
    // 将环境内部的数字牌面转换为真实世界认识的字符串（调试用）
    // 3-10就是原牌，11=J, 12=Q, 13=K, 14=A, 17=2, 20=小王(X), 30=大王(D)
    public static readonly Dictionary<int, string> EnvCard2RealCard = new Dictionary<int, string>
    {
        { 3, "3" }, { 4, "4" }, { 5, "5" }, { 6, "6" }, { 7, "7" },
        { 8, "8" }, { 9, "9" }, { 10, "10" }, { 11, "J" }, { 12, "Q" },
        { 13, "K" }, { 14, "A" }, { 17, "2" }, { 20, "X" }, { 30, "D" }
    };

    public static readonly Dictionary<string, int> RealCard2EnvCard = new Dictionary<string, int>
    {
        { "3", 3 }, { "4", 4 }, { "5", 5 }, { "6", 6 }, { "7", 7 },
        { "8", 8 }, { "9", 9 }, { "10", 10 }, { "J", 11 }, { "Q", 12 },
        { "K", 13 }, { "A", 14 }, { "2", 17 }, { "X", 20 }, { "D", 30 }
    };

    // 预先定义好所有的炸弹组合（4张一样的，以及王炸），用于快速检测是否打出了炸弹
    public static readonly List<List<int>> Bombs = new List<List<int>>
    {
        new List<int> { 3, 3, 3, 3 }, new List<int> { 4, 4, 4, 4 }, new List<int> { 5, 5, 5, 5 }, new List<int> { 6, 6, 6, 6 },
        new List<int> { 7, 7, 7, 7 }, new List<int> { 8, 8, 8, 8 }, new List<int> { 9, 9, 9, 9 }, new List<int> { 10, 10, 10, 10 },
        new List<int> { 11, 11, 11, 11 }, new List<int> { 12, 12, 12, 12 }, new List<int> { 13, 13, 13, 13 }, new List<int> { 14, 14, 14, 14 },
        new List<int> { 17, 17, 17, 17 }, new List<int> { 20, 30 }
    };

    // 游戏全局历史出牌记录，按顺序保存每一手打出的牌（如果玩家Pass，则保存空列表 []）
    public List<List<int>> CardPlayActionSeq { get; set; }

    // 地主的三张底牌
    public List<int> ThreeLandlordCards { get; set; }

    // 游戏是否结束的标志位
    public bool GameOver { get; set; }

    // 当前轮到谁出牌了（"landlord", "landlord_up" 还是 "landlord_down"）
    public string ActingPlayerPosition { get; set; }

    // 记录每位玩家在游戏结束时的收益（赢了得正分，输了得负分）
    public Dictionary<string, int> PlayerUtilityDict { get; set; }

    // 【 DummyAgent 存储处 】
    // 这里的 Players 是由上层的 Env 在初始化时传进来的。裁判 Game 不管这些 DummyAgent 是怎么来的，
    // 裁判只管在轮到某人出牌时，向对应座位的 DummyAgent 伸手要牌。
    public Dictionary<string, DummyAgent> Players { get; set; }

    // 记录每位玩家“最近一次”的出牌动作（包含 Pass）
    public Dictionary<string, List<int>> LastMoveDict { get; set; }

    // 记录每位玩家“已经打出”的所有牌的集合
    public Dictionary<string, List<int>> PlayedCards { get; set; }

    // 全局中，最近一次的有效出牌（不包含 Pass），用于判断当前玩家需要压什么牌
    public List<int> LastMove { get; set; }

    // 最近两次的出牌记录（通常用于某些神经网络特征）
    public List<List<int>> LastTwoMoves { get; set; }

    // 记录各个阵营赢了多少局（统计用）
    public Dictionary<string, int> NumWins { get; set; }

    // 记录各个阵营总共赢了多少分（统计用，通常包含炸弹倍率）
    public Dictionary<string, int> NumScores { get; set; }

    // 针对每个玩家的【上帝视角信息集】。
    // 在斗地主里，我们不能把对手的牌直接告诉当前玩家，所以每个玩家都有一个属于自己的 InfoSet
    public Dictionary<string, InfoSet> InfoSets { get; set; }

    // 当前这局游戏打出了多少个炸弹（决定结算倍率）
    public int BombNum { get; set; }

    // 上一个打出“有效牌（非Pass）”的玩家身份标识
    public string LastPid { get; set; }

    // 当前正在行动的玩家，他所能看到的游戏状态（信息集）
    public InfoSet GameInfoset { get; set; }

    // 游戏最终的胜利阵营："landlord" 或 "farmer"
    private string Winner { get; set; }

    /// <summary>
    /// Game 构造函数，传入三个 DummyAgent 代理。
    /// </summary>
    public Game(Dictionary<string, DummyAgent> players)
    {
        Players = players;
        NumWins = new Dictionary<string, int> { { "landlord", 0 }, { "farmer", 0 } };
        NumScores = new Dictionary<string, int> { { "landlord", 0 }, { "farmer", 0 } };
        Reset();
    }

    /// <summary>
    /// 核心初始化方法：在发完牌后被调用，把牌分发给对应的玩家，并初始化地主底牌。
    /// </summary>
    public void CardPlayInit(Dictionary<string, List<int>> cardPlayData)
    {
        InfoSets["landlord"].PlayerHandCards = new List<int>(cardPlayData["landlord"]);
        InfoSets["landlord_up"].PlayerHandCards = new List<int>(cardPlayData["landlord_up"]);
        InfoSets["landlord_down"].PlayerHandCards = new List<int>(cardPlayData["landlord_down"]);
        ThreeLandlordCards = new List<int>(cardPlayData["three_landlord_cards"]);

        GetActingPlayerPosition(); // 初始化第一个行动的玩家（必定是地主）
        GameInfoset = GetInfoset(); // 获取地主当前的信息集
    }

    /// <summary>
    /// 检查游戏是否结束（只要有任何一个人的手牌数量变为 0，游戏就结束）
    /// </summary>
    public void GameDone()
    {
        if (InfoSets["landlord"].PlayerHandCards.Count == 0 ||
            InfoSets["landlord_up"].PlayerHandCards.Count == 0 ||
            InfoSets["landlord_down"].PlayerHandCards.Count == 0)
        {
            ComputePlayerUtility(); // 计算这局的基础得分
            UpdateNumWinsScores();  // 更新计分板
            GameOver = true;
        }
    }

    /// <summary>
    /// 计算每个阵营的基础输赢得分（地主赢了得 2 分，农民各输 1 分。反之同理）
    /// </summary>
    public void ComputePlayerUtility()
    {
        if (InfoSets["landlord"].PlayerHandCards.Count == 0)
        {
            PlayerUtilityDict = new Dictionary<string, int> { { "landlord", 2 }, { "farmer", -1 } };
        }
        else
        {
            PlayerUtilityDict = new Dictionary<string, int> { { "landlord", -2 }, { "farmer", 1 } };
        }
    }

    /// <summary>
    /// 根据本局的得分和炸弹数量，计算最终翻倍后的分值，并累加到总计分板中。
    /// </summary>
    public void UpdateNumWinsScores()
    {
        foreach (var kvp in PlayerUtilityDict)
        {
            string pos = kvp.Key;
            int utility = kvp.Value;
            int baseScore = pos == "landlord" ? 2 : 1;

            if (utility > 0)
            {
                NumWins[pos] += 1;
                Winner = pos; // 记录胜方
                // 每次炸弹翻倍 (2的 BombNum 次方)
                NumScores[pos] += baseScore * (int)Math.Pow(2, BombNum);
            }
            else
            {
                NumScores[pos] -= baseScore * (int)Math.Pow(2, BombNum);
            }
        }
    }

    public string GetWinner() { return Winner; }
    public int GetBombNum() { return BombNum; }

    /// <summary>
    /// 核心状态机推进：当前玩家执行一步出牌动作。
    /// </summary>
    public void Step()
    {
        // 【提取 Action 的时刻】
        // 这时，裁判（Game）向当前轮到的人的传声筒（DummyAgent）发问："你要出什么牌？"
        // 注意：在此刻之前，外部的 Env 已经调用过 DummyAgent.SetAction()，把外面算好的动作提前塞进去了。
        // 所以这里 DummyAgent.Act() 只是机械地把之前存进去的牌拿出来给裁判而已。
        var action = Players[ActingPlayerPosition].Act(GameInfoset);

        // 2. 如果出的不是 Pass，记录下出牌人的身份
        if (action.Count > 0) LastPid = ActingPlayerPosition;

        // 3. 检查是不是打出了炸弹或王炸，如果是，炸弹计数器 +1
        if (Bombs.Any(b => b.SequenceEqual(action))) BombNum++;

        // 4. 记录各种历史信息
        LastMoveDict[ActingPlayerPosition] = new List<int>(action);
        CardPlayActionSeq.Add(new List<int>(action));
        UpdateActingPlayerHandCards(action); // 从手牌里扣除打出去的牌
        PlayedCards[ActingPlayerPosition].AddRange(action); // 丢进已打出的牌堆

        // 5. 这是一个特性：如果是地主出牌，并且打出了地主的底牌，就把底牌从记录中划掉（用于特定的特征提取）
        if (ActingPlayerPosition == "landlord" && action.Count > 0 && ThreeLandlordCards.Count > 0)
        {
            foreach (var card in action)
            {
                if (ThreeLandlordCards.Count > 0)
                {
                    if (ThreeLandlordCards.Contains(card)) ThreeLandlordCards.Remove(card);
                }
                else break;
            }
        }

        // 6. 检查牌打完没有？
        GameDone();

        // 7. 如果游戏没结束，把行动权交给下一个人
        if (!GameOver)
        {
            GetActingPlayerPosition();
        }
        GameInfoset = GetInfoset(); // 生成下一个人的观察视野
    }

    /// <summary>
    /// 获取全局倒数第一次打出的有效牌（忽略Pass）
    /// </summary>
    public List<int> GetLastMove()
    {
        List<int> lastMove = new List<int>();
        if (CardPlayActionSeq.Count != 0)
        {
            if (CardPlayActionSeq.Last().Count == 0) // 如果最后一步是Pass
            {
                if (CardPlayActionSeq.Count >= 2)
                    lastMove = CardPlayActionSeq[CardPlayActionSeq.Count - 2];
            }
            else
            {
                lastMove = CardPlayActionSeq.Last();
            }
        }
        return lastMove;
    }

    /// <summary>
    /// 获取最近两次的出牌（包含Pass），不管是谁出的
    /// </summary>
    public List<List<int>> GetLastTwoMoves()
    {
        List<List<int>> lastTwoMoves = new List<List<int>> { new List<int>(), new List<int>() };
        var recentMoves = CardPlayActionSeq.Skip(Math.Max(0, CardPlayActionSeq.Count - 2)).ToList();

        foreach (var card in recentMoves)
        {
            lastTwoMoves.Insert(0, new List<int>(card));
            lastTwoMoves = lastTwoMoves.Take(2).ToList();
        }
        return lastTwoMoves;
    }

    /// <summary>
    /// 按照地主 -> 地主下家 -> 地主上家 -> 地主的顺序切换行动权
    /// </summary>
    public string GetActingPlayerPosition()
    {
        if (string.IsNullOrEmpty(ActingPlayerPosition))
            ActingPlayerPosition = "landlord";
        else
        {
            if (ActingPlayerPosition == "landlord") ActingPlayerPosition = "landlord_down";
            else if (ActingPlayerPosition == "landlord_down") ActingPlayerPosition = "landlord_up";
            else ActingPlayerPosition = "landlord";
        }
        return ActingPlayerPosition;
    }

    /// <summary>
    /// 从当前玩家的手牌列表中，移除他刚刚打出的那几张牌，并重新排序
    /// </summary>
    public void UpdateActingPlayerHandCards(List<int> action)
    {
        if (action != null && action.Count > 0)
        {
            foreach (var card in action)
            {
                InfoSets[ActingPlayerPosition].PlayerHandCards.Remove(card);
            }
            InfoSets[ActingPlayerPosition].PlayerHandCards.Sort();
        }
    }

    /// <summary>
    /// 【非常核心】计算当前玩家此时此刻，所有合法的出牌动作（合法解空间）
    /// 它会调用 MoveGenerator 生成手里的所有牌型，再调用 MoveSelector 过滤出能压过上一手牌的牌型。
    /// </summary>
    public List<List<int>> GetLegalCardPlayActions()
    {
        // 拿到当前玩家的手牌，扔进生成器
        var mg = new MoveGenerator(InfoSets[ActingPlayerPosition].PlayerHandCards);
        var actionSequence = CardPlayActionSeq;

        // 找对手上一手出的牌（rivalMove）
        List<int> rivalMove = new List<int>();
        if (actionSequence.Count != 0)
        {
            if (actionSequence.Last().Count == 0) // 上家如果 Pass，找上上家
            {
                if (actionSequence.Count >= 2)
                    rivalMove = actionSequence[actionSequence.Count - 2];
            }
            else
            {
                rivalMove = actionSequence.Last(); // 上家没 Pass，就是上家出的牌
            }
        }

        // 用 MoveDetector 分析对手出的是什么牌型
        var rivalTypeInfo = MoveDetector.GetMoveType(rivalMove);
        int rivalMoveType = rivalTypeInfo.Type;
        int rivalMoveLen = rivalTypeInfo.Len == 0 ? 1 : rivalTypeInfo.Len;

        List<List<int>> moves = new List<List<int>>();

        // 如果两人都 Pass 了，或者你是地主刚开局，那你爱出什么出什么（GenAllMoves）
        if (rivalMoveType == DouUtils.Type0Pass)
            moves = mg.GenAllMoves();
        else
        {
            // 否则，根据对手的牌型，筛选出同牌型且比他大的牌
            if (rivalMoveType == DouUtils.Type1Single) moves = MoveSelector.FilterType1Single(mg.GenType1Single(), rivalMove);
            else if (rivalMoveType == DouUtils.Type2Pair) moves = MoveSelector.FilterType2Pair(mg.GenType2Pair(), rivalMove);
            else if (rivalMoveType == DouUtils.Type3Triple) moves = MoveSelector.FilterType3Triple(mg.GenType3Triple(), rivalMove);
            else if (rivalMoveType == DouUtils.Type4Bomb) moves = MoveSelector.FilterType4Bomb(mg.GenType4Bomb().Concat(mg.GenType5KingBomb()).ToList(), rivalMove);
            else if (rivalMoveType == DouUtils.Type5KingBomb) moves = new List<List<int>>(); // 王炸没人压得住
            else if (rivalMoveType == DouUtils.Type631) moves = MoveSelector.FilterType631(mg.GenType6ThreeOne(), rivalMove);
            else if (rivalMoveType == DouUtils.Type732) moves = MoveSelector.FilterType732(mg.GenType7ThreeTwo(), rivalMove);
            else if (rivalMoveType == DouUtils.Type8SerialSingle) moves = MoveSelector.FilterType8SerialSingle(mg.GenType8SerialSingle(rivalMoveLen), rivalMove);
            else if (rivalMoveType == DouUtils.Type9SerialPair) moves = MoveSelector.FilterType9SerialPair(mg.GenType9SerialPair(rivalMoveLen), rivalMove);
            else if (rivalMoveType == DouUtils.Type10SerialTriple) moves = MoveSelector.FilterType10SerialTriple(mg.GenType10SerialTriple(rivalMoveLen), rivalMove);
            else if (rivalMoveType == DouUtils.Type11Serial31) moves = MoveSelector.FilterTypeSerialTripleWing(mg.GenType11Serial31(rivalMoveLen), rivalMove);
            else if (rivalMoveType == DouUtils.Type12Serial32) moves = MoveSelector.FilterTypeSerialTripleWing(mg.GenType12Serial32(rivalMoveLen), rivalMove);
            else if (rivalMoveType == DouUtils.Type1342) moves = MoveSelector.FilterType1342(mg.GenType1342(), rivalMove);
            else if (rivalMoveType == DouUtils.Type14422) moves = MoveSelector.FilterType14422(mg.GenType14422(), rivalMove);

            // 【斗地主规则】：不管对手出什么（除非也是炸），我都随时能扔个炸弹或者王炸出去
            if (rivalMoveType != DouUtils.Type0Pass &&
                rivalMoveType != DouUtils.Type4Bomb &&
                rivalMoveType != DouUtils.Type5KingBomb)
            {
                moves.AddRange(mg.GenType4Bomb());
                moves.AddRange(mg.GenType5KingBomb());
            }
        }

        // 如果不是自己首发出牌，永远可以选择 Pass (空列表 [])
        if (rivalMove.Count != 0)
        {
            moves.Add(new List<int>());
        }

        foreach (var m in moves) m.Sort();
        return moves;
    }

    /// <summary>
    /// 重置所有状态，清空历史，准备下一局
    /// </summary>
    public void Reset()
    {
        CardPlayActionSeq = new List<List<int>>();
        ThreeLandlordCards = null;
        GameOver = false;
        ActingPlayerPosition = null;
        PlayerUtilityDict = null;
        LastMoveDict = new Dictionary<string, List<int>> { { "landlord", new List<int>() }, { "landlord_up", new List<int>() }, { "landlord_down", new List<int>() } };
        PlayedCards = new Dictionary<string, List<int>> { { "landlord", new List<int>() }, { "landlord_up", new List<int>() }, { "landlord_down", new List<int>() } };
        LastMove = new List<int>();
        LastTwoMoves = new List<List<int>>();
        InfoSets = new Dictionary<string, InfoSet> { { "landlord", new InfoSet("landlord") }, { "landlord_up", new InfoSet("landlord_up") }, { "landlord_down", new InfoSet("landlord_down") } };
        BombNum = 0;
        LastPid = "landlord";
    }

    /// <summary>
    /// 把当前游戏的所有状态打包，组装成当前行动玩家视角的 InfoSet（信息集）
    /// 这个信息集会交给 Env，用来提取成神经网络需要的特征张量。
    /// </summary>
    public InfoSet GetInfoset()
    {
        InfoSets[ActingPlayerPosition].LastPid = LastPid;
        InfoSets[ActingPlayerPosition].LegalActions = GetLegalCardPlayActions(); // 生成可用的动作列表
        InfoSets[ActingPlayerPosition].BombNum = BombNum;
        InfoSets[ActingPlayerPosition].LastMove = GetLastMove();
        InfoSets[ActingPlayerPosition].LastTwoMoves = GetLastTwoMoves();
        InfoSets[ActingPlayerPosition].LastMoveDict = LastMoveDict;

        // 告诉玩家，现在其余三个人手里各还剩多少张牌（记牌器功能）
        InfoSets[ActingPlayerPosition].NumCardsLeftDict = new Dictionary<string, int>
        {
            { "landlord", InfoSets["landlord"].PlayerHandCards.Count },
            { "landlord_up", InfoSets["landlord_up"].PlayerHandCards.Count },
            { "landlord_down", InfoSets["landlord_down"].PlayerHandCards.Count }
        };

        // 把别人的牌合并起来（通常算番或者预测对手可能有的牌时会用到这个并集）
        InfoSets[ActingPlayerPosition].OtherHandCards = new List<int>();
        string[] positions = { "landlord", "landlord_up", "landlord_down" };
        foreach (var pos in positions)
        {
            if (pos != ActingPlayerPosition)
                InfoSets[ActingPlayerPosition].OtherHandCards.AddRange(InfoSets[pos].PlayerHandCards);
        }

        InfoSets[ActingPlayerPosition].PlayedCards = PlayedCards;
        InfoSets[ActingPlayerPosition].ThreeLandlordCards = ThreeLandlordCards;
        InfoSets[ActingPlayerPosition].CardPlayActionSeq = CardPlayActionSeq;
        InfoSets[ActingPlayerPosition].AllHandcards = new Dictionary<string, List<int>>
        {
            { "landlord", InfoSets["landlord"].PlayerHandCards },
            { "landlord_up", InfoSets["landlord_up"].PlayerHandCards },
            { "landlord_down", InfoSets["landlord_down"].PlayerHandCards }
        };

        // 返回深拷贝，防止外部无意篡改环境内部状态
        return InfoSets[ActingPlayerPosition].Clone();
    }
}

/// <summary>
/// InfoSet 包含了某一时刻、某一个玩家能获取到的【所有游戏状态】。
/// 它就像一张全息照片，记录了这一瞬间场上发生的一切。
/// </summary>
public class InfoSet
{
    public string PlayerPosition { get; set; }           // 我是谁 (地主、上家、下家)
    public List<int> PlayerHandCards { get; set; }       // 我手里的牌
    public Dictionary<string, int> NumCardsLeftDict { get; set; }// 三家剩余牌数 (记牌器)
    public List<int> ThreeLandlordCards { get; set; }    // 地主底牌
    public List<List<int>> CardPlayActionSeq { get; set; } // 全局出牌历史序列
    public List<int> OtherHandCards { get; set; }        // 外面还没出的牌（另外两家手牌并集）
    public List<List<int>> LegalActions { get; set; }    // 我当前这回合，所有可以出的合法动作
    public List<int> LastMove { get; set; }              // 对手刚才打出的那一手牌
    public List<List<int>> LastTwoMoves { get; set; }    // 最近的两手牌
    public Dictionary<string, List<int>> LastMoveDict { get; set; } // 记录每个人最近出的什么牌
    public Dictionary<string, List<int>> PlayedCards { get; set; }  // 每个人已经打出的牌堆
    public Dictionary<string, List<int>> AllHandcards { get; set; } // 所有人手里的牌（上帝视角，神经网络可能用来训练价值网络）
    public string LastPid { get; set; }                  // 最后一个没Pass的人
    public int BombNum { get; set; }                     // 这局出了几个炸弹

    public InfoSet(string playerPosition) { PlayerPosition = playerPosition; }

    // 提供深拷贝方法
    public InfoSet Clone()
    {
        return new InfoSet(PlayerPosition)
        {
            PlayerHandCards = PlayerHandCards == null ? null : new List<int>(PlayerHandCards),
            NumCardsLeftDict = NumCardsLeftDict == null ? null : new Dictionary<string, int>(NumCardsLeftDict),
            ThreeLandlordCards = ThreeLandlordCards == null ? null : new List<int>(ThreeLandlordCards),
            CardPlayActionSeq = CardPlayActionSeq?.Select(seq => new List<int>(seq)).ToList(),
            OtherHandCards = OtherHandCards == null ? null : new List<int>(OtherHandCards),
            LegalActions = LegalActions?.Select(seq => new List<int>(seq)).ToList(),
            LastMove = LastMove == null ? null : new List<int>(LastMove),
            LastTwoMoves = LastTwoMoves?.Select(seq => new List<int>(seq)).ToList(),
            LastMoveDict = DeepCopyDict(LastMoveDict),
            PlayedCards = DeepCopyDict(PlayedCards),
            AllHandcards = DeepCopyDict(AllHandcards),
            LastPid = LastPid,
            BombNum = BombNum
        };
    }

    private static Dictionary<string, List<int>> DeepCopyDict(Dictionary<string, List<int>> source)
    {
        if (source == null) return null;
        var copy = new Dictionary<string, List<int>>();
        foreach (var kvp in source)
            copy[kvp.Key] = kvp.Value == null ? new List<int>() : new List<int>(kvp.Value);
        return copy;
    }
}