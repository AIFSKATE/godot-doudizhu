using System;
using System.Collections.Generic;
using System.Linq;

// 对应 env.py
public class Env
{
    public static readonly Dictionary<int, int> Card2Column = new Dictionary<int, int>
    {
        { 3, 0 }, { 4, 1 }, { 5, 2 }, { 6, 3 }, { 7, 4 }, { 8, 5 }, { 9, 6 }, { 10, 7 },
        { 11, 8 }, { 12, 9 }, { 13, 10 }, { 14, 11 }, { 17, 12 }
    };

    public static readonly Dictionary<int, float[]> NumOnes2Array = new Dictionary<int, float[]>
    {
        { 0, new float[] { 0, 0, 0, 0 } },
        { 1, new float[] { 1, 0, 0, 0 } },
        { 2, new float[] { 1, 1, 0, 0 } },
        { 3, new float[] { 1, 1, 1, 0 } },
        { 4, new float[] { 1, 1, 1, 1 } }
    };

    public static readonly List<int> Deck = new List<int>(54);

    static Env()
    {
        Deck = new List<int>();
        for (int i = 3; i < 15; i++)
        {
            for (int j = 0; j < 4; j++) Deck.Add(i);
        }
        for (int j = 0; j < 4; j++) Deck.Add(17);
        Deck.Add(20);
        Deck.Add(30);
    }

    public string Objective { get; set; }
    public Dictionary<string, DummyAgent> Players { get; set; }
    private Game _env;
    public InfoSet Infoset { get; set; }

    public Env(string objective)
    {
        Objective = objective;
        Players = new Dictionary<string, DummyAgent>
        {
            { "landlord", new DummyAgent("landlord") },
            { "landlord_up", new DummyAgent("landlord_up") },
            { "landlord_down", new DummyAgent("landlord_down") }
        };

        _env = new Game(Players);
        Infoset = null;
    }

    public Observation Reset()
    {
        _env.Reset();

        var deckCopy = new List<int>(Deck);
        Shuffle(deckCopy);

        var cardPlayData = new Dictionary<string, List<int>>
        {
            { "landlord", deckCopy.GetRange(0, 20) },
            { "landlord_up", deckCopy.GetRange(20, 17) },
            { "landlord_down", deckCopy.GetRange(37, 17) },
            { "three_landlord_cards", deckCopy.GetRange(17, 3) }
        };

        foreach (var key in cardPlayData.Keys.ToList())
        {
            cardPlayData[key].Sort();
        }

        _env.CardPlayInit(cardPlayData);
        Infoset = GameInfoset;

        return EnvHelper.GetObs(Infoset);
    }

    public (Observation Obs, float Reward, bool Done, Dictionary<string, object> Info) Step(List<int> action)
    {
        Players[ActingPlayerPosition].SetAction(action);
        _env.Step();
        Infoset = GameInfoset;

        bool done = false;
        float reward = 0.0f;
        Observation obs = null;

        if (GameOver)
        {
            done = true;
            reward = GetReward();
        }
        else
        {
            obs = EnvHelper.GetObs(Infoset);
        }

        return (obs, reward, done, new Dictionary<string, object>());
    }

    private float GetReward()
    {
        string winner = GameWinner;
        int bombNum = GameBombNum;

        if (winner == "landlord")
        {
            if (Objective == "adp") return (float)Math.Pow(2.0, bombNum);
            else if (Objective == "logadp") return bombNum + 1.0f;
            else return 1.0f;
        }
        else
        {
            if (Objective == "adp") return -(float)Math.Pow(2.0, bombNum);
            else if (Objective == "logadp") return -bombNum - 1.0f;
            else return -1.0f;
        }
    }

    public InfoSet GameInfoset => _env.GameInfoset;
    public int GameBombNum => _env.GetBombNum();
    public string GameWinner => _env.GetWinner();
    public string ActingPlayerPosition => _env.ActingPlayerPosition;
    public bool GameOver => _env.GameOver;

    private static Random _rng = new Random();
    private static void Shuffle<T>(List<T> list)
    {
        int n = list.Count;
        for (int i = 0; i < list.Count; i++)
        {
            int k = _rng.Next(n + 1);
            T value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }
}

public class DummyAgent
{
    public string Position { get; set; }
    public List<int> Action { get; set; }

    public DummyAgent(string position)
    {
        Position = position;
    }

    public List<int> Act(InfoSet infoset)
    {
        return Action;
    }

    public void SetAction(List<int> action)
    {
        Action = action;
    }
}

public class Observation
{
    public string Position { get; set; }
    public float[][] XBatch { get; set; }
    public float[][][] ZBatch { get; set; }
    public List<List<int>> LegalActions { get; set; }
    public float[] XNoAction { get; set; }
    public float[][] Z { get; set; }
}

public static class EnvHelper
{
    public static Observation GetObs(InfoSet infoset)
    {
        if (infoset.PlayerPosition == "landlord") return GetObsLandlord(infoset);
        if (infoset.PlayerPosition == "landlord_up") return GetObsLandlordUp(infoset);
        if (infoset.PlayerPosition == "landlord_down") return GetObsLandlordDown(infoset);
        throw new ArgumentException("Invalid player position");
    }

    public static float[] GetOneHotArray(int numLeftCards, int maxNumCards)
    {
        float[] oneHot = new float[maxNumCards];
        if (numLeftCards - 1 >= 0 && numLeftCards - 1 < maxNumCards)
        {
            oneHot[numLeftCards - 1] = 1;
        }
        return oneHot;
    }

    public static float[] Cards2Array(List<int> listCards)
    {
        float[] res = new float[54];
        if (listCards == null || listCards.Count == 0) return res;

        float[,] matrix = new float[4, 13];
        float[] jokers = new float[2];

        var counter = listCards.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        foreach (var kvp in counter)
        {
            int card = kvp.Key;
            int numTimes = kvp.Value;

            if (card < 20)
            {
                int col = Env.Card2Column[card];
                for (int i = 0; i < numTimes; i++) matrix[i, col] = 1;
            }
            else if (card == 20) jokers[0] = 1;
            else if (card == 30) jokers[1] = 1;
        }

        int idx = 0;
        for (int c = 0; c < 13; c++)
        {
            for (int r = 0; r < 4; r++)
            {
                res[idx++] = matrix[r, c];
            }
        }
        res[52] = jokers[0];
        res[53] = jokers[1];
        return res;
    }

    public static float[][] ActionSeqList2Array(List<List<int>> actionSeqList)
    {
        float[][] actionSeqArray = new float[actionSeqList.Count][];
        for (int row = 0; row < actionSeqList.Count; row++)
        {
            actionSeqArray[row] = Cards2Array(actionSeqList[row]);
        }

        float[][] reshaped = new float[5][];
        for (int i = 0; i < 5; i++)
        {
            reshaped[i] = new float[162];
            Array.Copy(actionSeqArray[i * 3], 0, reshaped[i], 0, 54);
            Array.Copy(actionSeqArray[i * 3 + 1], 0, reshaped[i], 54, 54);
            Array.Copy(actionSeqArray[i * 3 + 2], 0, reshaped[i], 108, 54);
        }
        return reshaped;
    }

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

    public static float[] GetOneHotBomb(int bombNum)
    {
        float[] oneHot = new float[15];
        if (bombNum >= 0 && bombNum < 15) oneHot[bombNum] = 1;
        return oneHot;
    }

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