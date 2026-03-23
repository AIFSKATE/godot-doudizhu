using System;
using System.Collections.Generic;
using System.Linq;

// 对应 env.py
// Env 类是强化学习环境的包装器。它把 Game（游戏底层裁判）和外部的神经网络（AI）连接起来。
// 它负责把游戏状态转换成矩阵矩阵，接收 AI 的动作输入并推进游戏进程。
public class Env
{
    // 用于将牌值映射到神经网络特征矩阵的固定列上 (13列，代表 3,4..K,A,2)
    public static readonly Dictionary<int, int> Card2Column = new Dictionary<int, int>
    {
        { 3, 0 }, { 4, 1 }, { 5, 2 }, { 6, 3 }, { 7, 4 }, { 8, 5 }, { 9, 6 }, { 10, 7 },
        { 11, 8 }, { 12, 9 }, { 13, 10 }, { 14, 11 }, { 17, 12 }
    };

    // 用于将手里持有多少张同点数的牌，转化为 One-hot（独热码）编码。
    // 例如有两张 3，就会映射为 [1, 1, 0, 0]
    public static readonly Dictionary<int, float[]> NumOnes2Array = new Dictionary<int, float[]>
    {
        { 0, new float[] { 0, 0, 0, 0 } },
        { 1, new float[] { 1, 0, 0, 0 } },
        { 2, new float[] { 1, 1, 0, 0 } },
        { 3, new float[] { 1, 1, 1, 0 } },
        { 4, new float[] { 1, 1, 1, 1 } }
    };

    // 一副完整的标准扑克牌（54张），包含了所有的值映射。静态初始化。
    public static readonly List<int> Deck;

    static Env()
    {
        Deck = new List<int>();
        for (int i = 3; i < 15; i++)
        {
            for (int j = 0; j < 4; j++) Deck.Add(i); // 3到A各四张
        }
        for (int j = 0; j < 4; j++) Deck.Add(17); // 四张2
        Deck.Add(20); // 小王
        Deck.Add(30); // 大王
    }

    // AI的训练目标类型，比如 "adp"(考虑炸弹倍率)、"logadp" 等
    public string Objective { get; set; }

    // 三个虚拟玩家代理。因为我们要隔离外部AI和内部游戏，外部AI通过控制这些DummyAgent来出牌。
    public Dictionary<string, DummyAgent> Players { get; set; }

    // 底层真正的游戏逻辑裁判
    private Game _game;

    // 当前全局的信息集
    public InfoSet Infoset { get; set; }

    public Env(string objective)
    {
        Objective = objective;

        // 【 DummyAgent 的 Position 在这里生成 / 赋予 】
        // 游戏程序一启动（new Env 时），就会把这三个“无情传声筒”实例化出来，并固定给它们贴好标签
        // "landlord" = 地主座位，"landlord_up" = 地主上家座位，"landlord_down" = 地主下家座位。
        // 从此之后，它们的 Position 永远不变。
        Players = new Dictionary<string, DummyAgent>
        {
            { "landlord", new DummyAgent("landlord") },
            { "landlord_up", new DummyAgent("landlord_up") },
            { "landlord_down", new DummyAgent("landlord_down") }
        };

        // 把这三个带有身份标签的传声筒，交给底层裁判 Game 备用。
        _game = new Game(Players);
        Infoset = null;
    }

    /// <summary>
    /// 重置环境：新开一局游戏，洗牌、发牌，并返回第一步的初始观测状态（Observation）。
    /// </summary>
    public Observation Reset()
    {
        _game.Reset(); // 清空裁判板记录

        var deckCopy = new List<int>(Deck);
        Shuffle(deckCopy); // 调用严格的 Fisher-Yates 洗牌算法打乱整副牌

        // 按顺序发牌（切片）
        var cardPlayData = new Dictionary<string, List<int>>
        {
            { "landlord", deckCopy.GetRange(0, 20) },           // 地主 20 张
            { "landlord_up", deckCopy.GetRange(20, 17) },       // 上家 17 张
            { "landlord_down", deckCopy.GetRange(37, 17) },     // 下家 17 张
            { "three_landlord_cards", deckCopy.GetRange(17, 3) }// 提取底牌作为记录
        };

        // 理牌：把每个人的手牌从小到大排序好
        foreach (var key in cardPlayData.Keys.ToList())
            cardPlayData[key].Sort();

        _game.CardPlayInit(cardPlayData); // 让裁判正式开局
        Infoset = GameInfoset;

        // 【关键点】把此时地主的信息集，翻译成神经网络需要的数据结构 Observation 并返回
        return EnvHelper.GetObs(Infoset);
    }

    /// <summary>
    /// 步进方法：外部 AI 算出动作后，通过这个参数把 action 传进来，并推动一回合的游戏。
    /// </summary>
    public (Observation Obs, float Reward, bool Done, Dictionary<string, object> Info) Step(List<int> action)
    {
        // 【 DummyAgent 的 Action 在这里生成 / 赋予 】
        // 这里的 action 参数不是 DummyAgent 自己想出来的！而是【游戏外面的 AI（或者随机算法）】看完了局势后告诉你的。
        // Env 作为环境的主接口，拿到外面给出的 action 后，立刻把这个动作“硬塞”给当前行动座位的 DummyAgent。
        // 相当于你对着一个戴耳机的替身玩家大喊：“这把给我出一对3！”
        Players[ActingPlayerPosition].SetAction(action);

        // 当我们确信替身玩家已经把指令（Action）记在脑子里后，我们转头让裁判去执行下一步逻辑。
        // 此时裁判（_game.Step）在内部就会去问这个 DummyAgent："你要出什么？"
        // DummyAgent 就会乖乖地把刚才记住的 Action 交给裁判。
        _game.Step();

        Infoset = GameInfoset; // 裁判处理完这轮出牌后，刷新局势面板

        bool done = false;
        float reward = 0.0f;
        Observation obs = null;

        // 3. 判断游戏结束没有
        if (GameOver)
        {
            done = true;
            reward = GetReward(); // 如果结束了，计算这局挣了多少分
        }
        else
        {
            obs = EnvHelper.GetObs(Infoset); // 如果没结束，生成下一个人需要的神经网络视野，扔给外部AI准备思考下一步
        }

        return (obs, reward, done, new Dictionary<string, object>());
    }

    /// <summary>
    /// 游戏终局时被调用，根据训练目标（Objective）计算实际的 RL 奖励值
    /// </summary>
    private float GetReward()
    {
        string winner = GameWinner;
        int bombNum = GameBombNum;

        if (winner == "landlord")
        {
            // adp 意味着指数翻倍 (2^n)
            if (Objective == "adp") return (float)Math.Pow(2.0, bombNum);
            // logadp 是平滑翻倍对数版
            else if (Objective == "logadp") return bombNum + 1.0f;
            else return 1.0f; // wp 是纯胜率模式，不管炸弹
        }
        else
        {
            // 农民赢了地主输分，逻辑与上面相反
            if (Objective == "adp") return -(float)Math.Pow(2.0, bombNum);
            else if (Objective == "logadp") return -bombNum - 1.0f;
            else return -1.0f;
        }
    }

    // 一些只读属性快捷访问代理
    public InfoSet GameInfoset => _game.GameInfoset;
    public int GameBombNum => _game.GetBombNum();
    public string GameWinner => _game.GetWinner();
    public string ActingPlayerPosition => _game.ActingPlayerPosition;
    public bool GameOver => _game.GameOver;

    private static Random _rng = new Random();

    /// <summary>
    /// 标准的 Fisher-Yates 洗牌算法。
    /// 数学证明在每次抽取时都做到了绝对概率均等，且无多余内存分配。
    /// </summary>
    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int k = _rng.Next(i + 1);
            T temp = list[i];
            list[i] = list[k];
            list[k] = temp;
        }
    }
}

/// <summary>
/// DummyAgent（虚拟代理 / 替身）
/// 它就是一个纯粹的“信息中转站”或“传声筒”。
/// 它的生命周期是这样的：
/// 1. 程序启动时，它被分配好固定的座位（Position，比如 "landlord"）。
/// 2. 当轮到它出牌时，外部 AI 通过 Env.Step(action)，调用它的 SetAction()，强行把决定好的动作写进它脑子里。
/// 3. 紧接着 Game(裁判) 执行逻辑，调用它的 Act() 询问它出什么，它就原封不动地把刚才被塞进来的 Action 交出去。
/// 为什么要设计这么个多余的东西？
/// 因为在强化学习里，我们需要把【内部死板的裁判引擎】和【外部聪明的神经网络算法】完全隔离开，
/// DummyAgent 就是连接内外的“接口槽”。
/// </summary>
public class DummyAgent
{
    // 我是坐在哪个座位上的？(landlord/landlord_up/landlord_down)
    public string Position { get; set; }

    // 我当前这回合，被别人命令要打出的牌。
    public List<int> Action { get; set; }

    public DummyAgent(string position) { Position = position; }

    // Game 裁判要出牌时调用这个
    public List<int> Act(InfoSet infoset) { return Action; }

    // Env 外部强塞指令时调用这个
    public void SetAction(List<int> action) { Action = action; }
}

/// <summary>
/// 最终喂给强化学习模型（如 DouZero）的特征结构体。
/// 这个类是把人类能看懂的手牌，变成了计算机擅长处理的多维矩阵。
/// </summary>
public class Observation
{
    public string Position { get; set; }     // 当前视角的身份
    public float[][] XBatch { get; set; }    // 【重点】二维矩阵：记录你所有的合法动作，如果这步有 5 种出法，就是 5 行特征向量
    public float[][][] ZBatch { get; set; }  // 三维矩阵：专门编码最近的“出牌历史流”
    public List<List<int>> LegalActions { get; set; } // 这一步到底能出哪些牌的具体列表
    public float[] XNoAction { get; set; }   // 不包含当前可选动作状态的纯全局视野特征
    public float[][] Z { get; set; }         // 基础的历史序列特征
}

/// <summary>
/// 专门用来将 InfoSet 转化为神经网络多维张量(Tensor/Matrix)的辅助工具类。
/// 核心逻辑大量运用了 One-hot (独热) 编码技巧。
/// </summary>
public static class EnvHelper
{
    /// <summary>
    /// 提取总入口：自动判断当前是哪方阵营，调用对应的特定特征提取方法。
    /// </summary>
    public static Observation GetObs(InfoSet infoset)
    {
        if (infoset.PlayerPosition == "landlord") return GetObsLandlord(infoset);
        if (infoset.PlayerPosition == "landlord_up") return GetObsLandlordUp(infoset);
        if (infoset.PlayerPosition == "landlord_down") return GetObsLandlordDown(infoset);
        throw new ArgumentException("Invalid player position");
    }

    /// <summary>
    /// 将【某人手里剩多少张牌】转换为 One-hot 数组。
    /// 比如手里剩 17 张牌，就把索引 [16] 标为 1，其他全是 0。这使得神经网络更容易识别离散数字。
    /// </summary>
    public static float[] GetOneHotArray(int numLeftCards, int maxNumCards)
    {
        float[] oneHot = new float[maxNumCards];
        if (numLeftCards - 1 >= 0 && numLeftCards - 1 < maxNumCards)
        {
            oneHot[numLeftCards - 1] = 1;
        }
        return oneHot;
    }

    /// <summary>
    /// 将一叠任意的手牌转换为一个固定长度 54 的浮点向量 (长度54代表54张牌)。
    /// 比如一个对3，它在 54 个特征位里，代表 3 的地方会有两个 1。
    /// </summary>
    public static float[] Cards2Array(List<int> listCards)
    {
        float[] res = new float[54];
        if (listCards == null || listCards.Count == 0) return res;

        // 4行13列的矩阵代表除了王之外的普通牌（4种花色等效处理，13个点数）
        float[,] matrix = new float[4, 13];
        float[] jokers = new float[2];

        var counter = listCards.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        foreach (var kvp in counter)
        {
            int card = kvp.Key;
            int numTimes = kvp.Value;

            if (card < 20)
            {
                int col = Env.Card2Column[card]; // 把真实卡牌点数映射到 0~12 的列里
                for (int i = 0; i < numTimes; i++) matrix[i, col] = 1;
            }
            else if (card == 20) jokers[0] = 1; // 小王
            else if (card == 30) jokers[1] = 1; // 大王
        }

        // 把这个矩阵拍扁（Flatten）成一个一维数组塞进 res
        int idx = 0;
        for (int c = 0; c < 13; c++)
        {
            for (int r = 0; r < 4; r++)
                res[idx++] = matrix[r, c];
        }
        res[52] = jokers[0];
        res[53] = jokers[1];
        return res;
    }

    /// <summary>
    /// 处理历史动作序列：把历史出牌列表包装成一个 5 步（每步包含三家出牌）共计 5x162 的矩阵。
    /// 用于送给 LSTM 网络处理时间序列特征。
    /// </summary>
    public static float[][] ActionSeqList2Array(List<List<int>> actionSeqList)
    {
        float[][] actionSeqArray = new float[actionSeqList.Count][];
        for (int row = 0; row < actionSeqList.Count; row++)
            actionSeqArray[row] = Cards2Array(actionSeqList[row]);

        float[][] reshaped = new float[5][];
        for (int i = 0; i < 5; i++)
        {
            reshaped[i] = new float[162]; // 3家 * 54 = 162
            Array.Copy(actionSeqArray[i * 3], 0, reshaped[i], 0, 54);
            Array.Copy(actionSeqArray[i * 3 + 1], 0, reshaped[i], 54, 54);
            Array.Copy(actionSeqArray[i * 3 + 2], 0, reshaped[i], 108, 54);
        }
        return reshaped;
    }

    /// <summary>
    /// 截取最近 15 手历史出牌。不够的用空数组填充 (Padding)。
    /// </summary>
    public static List<List<int>> ProcessActionSeq(List<List<int>> sequence, int length = 15)
    {
        var seq = sequence.Skip(Math.Max(0, sequence.Count - length)).ToList();
        if (seq.Count < length)
        {
            var emptySequence = new List<List<int>>();
            for (int i = 0; i < length - seq.Count; i++)
                emptySequence.Add(new List<int>());
            emptySequence.AddRange(seq);
            seq = emptySequence;
        }
        return seq;
    }

    /// <summary>
    /// 把已打出的炸弹数量转换为 One-hot 编码（最大考虑到15个炸弹）。
    /// </summary>
    public static float[] GetOneHotBomb(int bombNum)
    {
        float[] oneHot = new float[15];
        if (bombNum >= 0 && bombNum < 15) oneHot[bombNum] = 1;
        return oneHot;
    }

    // ========== 下面这些私有方法是为了在拼接张量（Tensor）时做数组复制（Batch处理）用的 ==========
    private static float[][] Repeat(float[] arr, int times)
    {
        float[][] result = new float[times][];
        for (int i = 0; i < times; i++) result[i] = (float[])arr.Clone();
        return result;
    }

    private static float[][][] Repeat(float[][] arr, int times)
    {
        float[][][] result = new float[times][][];
        for (int i = 0; i < times; i++)
        {
            result[i] = new float[arr.Length][];
            for (int j = 0; j < arr.Length; j++) result[i][j] = (float[])arr[j].Clone();
        }
        return result;
    }

    // 将多组 Batch 拼接到一起（相当于 Numpy.hstack 水平拼接）
    private static float[][] HStack(params float[][][] batches)
    {
        if (batches.Length == 0) return new float[0][];
        int batchSize = batches[0].Length;
        float[][] result = new float[batchSize][];

        for (int i = 0; i < batchSize; i++)
        {
            int totalLen = 0;
            foreach (var batch in batches) totalLen += batch[i].Length;

            result[i] = new float[totalLen];
            int offset = 0;
            foreach (var batch in batches)
            {
                Array.Copy(batch[i], 0, result[i], offset, batch[i].Length);
                offset += batch[i].Length;
            }
        }
        return result;
    }

    private static float[] HStackFlat(params float[][] arrays)
    {
        int totalLen = 0;
        foreach (var arr in arrays) totalLen += arr.Length;

        float[] result = new float[totalLen];
        int offset = 0;
        foreach (var arr in arrays)
        {
            Array.Copy(arr, 0, result, offset, arr.Length);
            offset += arr.Length;
        }
        return result;
    }

    // ==============================================================================
    // 以下三个方法是【地主】、【地主上家】、【地主下家】专属的特征提取逻辑。
    // 逻辑基本一致，但拼接特征张量（XBatch）的顺序和视角由于身份不同有所区别。
    // 比如：地主只需要看两个农民（不分敌友），但农民要严格区分队友和地主（记牌不同）。
    // ==============================================================================

    private static Observation GetObsLandlord(InfoSet infoset)
    {
        int numLegalActions = infoset.LegalActions.Count;

        float[] myHandcards = Cards2Array(infoset.PlayerHandCards);
        float[][] myHandcardsBatch = Repeat(myHandcards, numLegalActions);

        float[] otherHandcards = Cards2Array(infoset.OtherHandCards);
        float[][] otherHandcardsBatch = Repeat(otherHandcards, numLegalActions);

        float[] lastAction = Cards2Array(infoset.LastMove);
        float[][] lastActionBatch = Repeat(lastAction, numLegalActions);

        float[][] myActionBatch = new float[numLegalActions][];
        for (int j = 0; j < numLegalActions; j++)
            myActionBatch[j] = Cards2Array(infoset.LegalActions[j]);

        float[] landlordUpNumCardsLeft = GetOneHotArray(infoset.NumCardsLeftDict["landlord_up"], 17);
        float[][] landlordUpNumCardsLeftBatch = Repeat(landlordUpNumCardsLeft, numLegalActions);

        float[] landlordDownNumCardsLeft = GetOneHotArray(infoset.NumCardsLeftDict["landlord_down"], 17);
        float[][] landlordDownNumCardsLeftBatch = Repeat(landlordDownNumCardsLeft, numLegalActions);

        float[] landlordUpPlayedCards = Cards2Array(infoset.PlayedCards["landlord_up"]);
        float[][] landlordUpPlayedCardsBatch = Repeat(landlordUpPlayedCards, numLegalActions);

        float[] landlordDownPlayedCards = Cards2Array(infoset.PlayedCards["landlord_down"]);
        float[][] landlordDownPlayedCardsBatch = Repeat(landlordDownPlayedCards, numLegalActions);

        float[] bombNum = GetOneHotBomb(infoset.BombNum);
        float[][] bombNumBatch = Repeat(bombNum, numLegalActions);

        float[][] xBatch = HStack(myHandcardsBatch, otherHandcardsBatch, lastActionBatch,
                                  landlordUpPlayedCardsBatch, landlordDownPlayedCardsBatch,
                                  landlordUpNumCardsLeftBatch, landlordDownNumCardsLeftBatch,
                                  bombNumBatch, myActionBatch);

        float[] xNoAction = HStackFlat(myHandcards, otherHandcards, lastAction,
                                       landlordUpPlayedCards, landlordDownPlayedCards,
                                       landlordUpNumCardsLeft, landlordDownNumCardsLeft, bombNum);

        float[][] z = ActionSeqList2Array(ProcessActionSeq(infoset.CardPlayActionSeq));
        float[][][] zBatch = Repeat(z, numLegalActions);

        return new Observation
        {
            Position = "landlord",
            XBatch = xBatch,
            ZBatch = zBatch,
            LegalActions = infoset.LegalActions,
            XNoAction = xNoAction,
            Z = z
        };
    }

    private static Observation GetObsLandlordUp(InfoSet infoset)
    {
        int numLegalActions = infoset.LegalActions.Count;

        float[] myHandcards = Cards2Array(infoset.PlayerHandCards);
        float[][] myHandcardsBatch = Repeat(myHandcards, numLegalActions);

        float[] otherHandcards = Cards2Array(infoset.OtherHandCards);
        float[][] otherHandcardsBatch = Repeat(otherHandcards, numLegalActions);

        float[] lastAction = Cards2Array(infoset.LastMove);
        float[][] lastActionBatch = Repeat(lastAction, numLegalActions);

        float[][] myActionBatch = new float[numLegalActions][];
        for (int j = 0; j < numLegalActions; j++)
            myActionBatch[j] = Cards2Array(infoset.LegalActions[j]);

        float[] lastLandlordAction = Cards2Array(infoset.LastMoveDict["landlord"]);
        float[][] lastLandlordActionBatch = Repeat(lastLandlordAction, numLegalActions);

        float[] landlordNumCardsLeft = GetOneHotArray(infoset.NumCardsLeftDict["landlord"], 20);
        float[][] landlordNumCardsLeftBatch = Repeat(landlordNumCardsLeft, numLegalActions);

        float[] landlordPlayedCards = Cards2Array(infoset.PlayedCards["landlord"]);
        float[][] landlordPlayedCardsBatch = Repeat(landlordPlayedCards, numLegalActions);

        float[] lastTeammateAction = Cards2Array(infoset.LastMoveDict["landlord_down"]);
        float[][] lastTeammateActionBatch = Repeat(lastTeammateAction, numLegalActions);

        float[] teammateNumCardsLeft = GetOneHotArray(infoset.NumCardsLeftDict["landlord_down"], 17);
        float[][] teammateNumCardsLeftBatch = Repeat(teammateNumCardsLeft, numLegalActions);

        float[] teammatePlayedCards = Cards2Array(infoset.PlayedCards["landlord_down"]);
        float[][] teammatePlayedCardsBatch = Repeat(teammatePlayedCards, numLegalActions);

        float[] bombNum = GetOneHotBomb(infoset.BombNum);
        float[][] bombNumBatch = Repeat(bombNum, numLegalActions);

        float[][] xBatch = HStack(myHandcardsBatch, otherHandcardsBatch, landlordPlayedCardsBatch, teammatePlayedCardsBatch,
                                  lastActionBatch, lastLandlordActionBatch, lastTeammateActionBatch,
                                  landlordNumCardsLeftBatch, teammateNumCardsLeftBatch, bombNumBatch, myActionBatch);

        float[] xNoAction = HStackFlat(myHandcards, otherHandcards, landlordPlayedCards, teammatePlayedCards,
                                       lastAction, lastLandlordAction, lastTeammateAction,
                                       landlordNumCardsLeft, teammateNumCardsLeft, bombNum);

        float[][] z = ActionSeqList2Array(ProcessActionSeq(infoset.CardPlayActionSeq));
        float[][][] zBatch = Repeat(z, numLegalActions);

        return new Observation
        {
            Position = "landlord_up",
            XBatch = xBatch,
            ZBatch = zBatch,
            LegalActions = infoset.LegalActions,
            XNoAction = xNoAction,
            Z = z
        };
    }

    private static Observation GetObsLandlordDown(InfoSet infoset)
    {
        int numLegalActions = infoset.LegalActions.Count;

        float[] myHandcards = Cards2Array(infoset.PlayerHandCards);
        float[][] myHandcardsBatch = Repeat(myHandcards, numLegalActions);

        float[] otherHandcards = Cards2Array(infoset.OtherHandCards);
        float[][] otherHandcardsBatch = Repeat(otherHandcards, numLegalActions);

        float[] lastAction = Cards2Array(infoset.LastMove);
        float[][] lastActionBatch = Repeat(lastAction, numLegalActions);

        float[][] myActionBatch = new float[numLegalActions][];
        for (int j = 0; j < numLegalActions; j++)
            myActionBatch[j] = Cards2Array(infoset.LegalActions[j]);

        float[] lastLandlordAction = Cards2Array(infoset.LastMoveDict["landlord"]);
        float[][] lastLandlordActionBatch = Repeat(lastLandlordAction, numLegalActions);

        float[] landlordNumCardsLeft = GetOneHotArray(infoset.NumCardsLeftDict["landlord"], 20);
        float[][] landlordNumCardsLeftBatch = Repeat(landlordNumCardsLeft, numLegalActions);

        float[] landlordPlayedCards = Cards2Array(infoset.PlayedCards["landlord"]);
        float[][] landlordPlayedCardsBatch = Repeat(landlordPlayedCards, numLegalActions);

        float[] lastTeammateAction = Cards2Array(infoset.LastMoveDict["landlord_up"]);
        float[][] lastTeammateActionBatch = Repeat(lastTeammateAction, numLegalActions);

        float[] teammateNumCardsLeft = GetOneHotArray(infoset.NumCardsLeftDict["landlord_up"], 17);
        float[][] teammateNumCardsLeftBatch = Repeat(teammateNumCardsLeft, numLegalActions);

        float[] teammatePlayedCards = Cards2Array(infoset.PlayedCards["landlord_up"]);
        float[][] teammatePlayedCardsBatch = Repeat(teammatePlayedCards, numLegalActions);

        float[] bombNum = GetOneHotBomb(infoset.BombNum);
        float[][] bombNumBatch = Repeat(bombNum, numLegalActions);

        float[][] xBatch = HStack(myHandcardsBatch, otherHandcardsBatch, landlordPlayedCardsBatch, teammatePlayedCardsBatch,
                                  lastActionBatch, lastLandlordActionBatch, lastTeammateActionBatch,
                                  landlordNumCardsLeftBatch, teammateNumCardsLeftBatch, bombNumBatch, myActionBatch);

        float[] xNoAction = HStackFlat(myHandcards, otherHandcards, landlordPlayedCards, teammatePlayedCards,
                                       lastAction, lastLandlordAction, lastTeammateAction,
                                       landlordNumCardsLeft, teammateNumCardsLeft, bombNum);

        float[][] z = ActionSeqList2Array(ProcessActionSeq(infoset.CardPlayActionSeq));
        float[][][] zBatch = Repeat(z, numLegalActions);

        return new Observation
        {
            Position = "landlord_down",
            XBatch = xBatch,
            ZBatch = zBatch,
            LegalActions = infoset.LegalActions,
            XNoAction = xNoAction,
            Z = z
        };
    }
}