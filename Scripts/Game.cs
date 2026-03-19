using System;
using System.Collections.Generic;
using System.Linq;

// 对应 game.py
public class Game
{
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

    public static readonly List<List<int>> Bombs = new List<List<int>>
    {
        new List<int> { 3, 3, 3, 3 }, new List<int> { 4, 4, 4, 4 }, new List<int> { 5, 5, 5, 5 }, new List<int> { 6, 6, 6, 6 },
        new List<int> { 7, 7, 7, 7 }, new List<int> { 8, 8, 8, 8 }, new List<int> { 9, 9, 9, 9 }, new List<int> { 10, 10, 10, 10 },
        new List<int> { 11, 11, 11, 11 }, new List<int> { 12, 12, 12, 12 }, new List<int> { 13, 13, 13, 13 }, new List<int> { 14, 14, 14, 14 },
        new List<int> { 17, 17, 17, 17 }, new List<int> { 20, 30 }
    };

    public List<List<int>> CardPlayActionSeq { get; set; }
    public List<int> ThreeLandlordCards { get; set; }
    public bool GameOver { get; set; }

    public string ActingPlayerPosition { get; set; }
    public Dictionary<string, int> PlayerUtilityDict { get; set; }
    public Dictionary<string, DummyAgent> Players { get; set; }

    public Dictionary<string, List<int>> LastMoveDict { get; set; }
    public Dictionary<string, List<int>> PlayedCards { get; set; }

    public List<int> LastMove { get; set; }
    public List<List<int>> LastTwoMoves { get; set; }

    public Dictionary<string, int> NumWins { get; set; }
    public Dictionary<string, int> NumScores { get; set; }

    public Dictionary<string, InfoSet> InfoSets { get; set; }

    public int BombNum { get; set; }
    public string LastPid { get; set; }
    public InfoSet GameInfoset { get; set; }
    private string Winner { get; set; }

    public Game(Dictionary<string, DummyAgent> players)
    {
        Players = players;

        NumWins = new Dictionary<string, int>
        {
            { "landlord", 0 },
            { "farmer", 0 }
        };

        NumScores = new Dictionary<string, int>
        {
            { "landlord", 0 },
            { "farmer", 0 }
        };

        Reset();
    }

    public void CardPlayInit(Dictionary<string, List<int>> cardPlayData)
    {
        InfoSets["landlord"].PlayerHandCards = new List<int>(cardPlayData["landlord"]);
        InfoSets["landlord_up"].PlayerHandCards = new List<int>(cardPlayData["landlord_up"]);
        InfoSets["landlord_down"].PlayerHandCards = new List<int>(cardPlayData["landlord_down"]);
        ThreeLandlordCards = new List<int>(cardPlayData["three_landlord_cards"]);

        GetActingPlayerPosition();
        GameInfoset = GetInfoset();
    }

    public void GameDone()
    {
        if (InfoSets["landlord"].PlayerHandCards.Count == 0 ||
            InfoSets["landlord_up"].PlayerHandCards.Count == 0 ||
            InfoSets["landlord_down"].PlayerHandCards.Count == 0)
        {
            ComputePlayerUtility();
            UpdateNumWinsScores();
            GameOver = true;
        }
    }

    public void ComputePlayerUtility()
    {
        if (InfoSets["landlord"].PlayerHandCards.Count == 0)
        {
            PlayerUtilityDict = new Dictionary<string, int>
            {
                { "landlord", 2 },
                { "farmer", -1 }
            };
        }
        else
        {
            PlayerUtilityDict = new Dictionary<string, int>
            {
                { "landlord", -2 },
                { "farmer", 1 }
            };
        }
    }

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
                Winner = pos;
                NumScores[pos] += baseScore * (int)Math.Pow(2, BombNum);
            }
            else
            {
                NumScores[pos] -= baseScore * (int)Math.Pow(2, BombNum);
            }
        }
    }

    public string GetWinner()
    {
        return Winner;
    }

    public int GetBombNum()
    {
        return BombNum;
    }

    public void Step()
    {
        var action = Players[ActingPlayerPosition].Act(GameInfoset);

        if (action.Count > 0)
        {
            LastPid = ActingPlayerPosition;
        }

        if (Bombs.Any(b => b.SequenceEqual(action)))
        {
            BombNum++;
        }

        LastMoveDict[ActingPlayerPosition] = new List<int>(action);
        CardPlayActionSeq.Add(new List<int>(action));
        UpdateActingPlayerHandCards(action);

        PlayedCards[ActingPlayerPosition].AddRange(action);

        if (ActingPlayerPosition == "landlord" && action.Count > 0 && ThreeLandlordCards.Count > 0)
        {
            foreach (var card in action)
            {
                if (ThreeLandlordCards.Count > 0)
                {
                    if (ThreeLandlordCards.Contains(card))
                    {
                        ThreeLandlordCards.Remove(card);
                    }
                }
                else
                {
                    break;
                }
            }
        }

        GameDone();
        if (!GameOver)
        {
            GetActingPlayerPosition();
            GameInfoset = GetInfoset();
        }
    }

    public List<int> GetLastMove()
    {
        List<int> lastMove = new List<int>();
        if (CardPlayActionSeq.Count != 0)
        {
            if (CardPlayActionSeq.Last().Count == 0)
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

    public string GetActingPlayerPosition()
    {
        if (string.IsNullOrEmpty(ActingPlayerPosition))
        {
            ActingPlayerPosition = "landlord";
        }
        else
        {
            if (ActingPlayerPosition == "landlord")
                ActingPlayerPosition = "landlord_down";
            else if (ActingPlayerPosition == "landlord_down")
                ActingPlayerPosition = "landlord_up";
            else
                ActingPlayerPosition = "landlord";
        }
        return ActingPlayerPosition;
    }

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

    public List<List<int>> GetLegalCardPlayActions()
    {
        var mg = new MoveGenerator(InfoSets[ActingPlayerPosition].PlayerHandCards);
        var actionSequence = CardPlayActionSeq;

        List<int> rivalMove = new List<int>();
        if (actionSequence.Count != 0)
        {
            if (actionSequence.Last().Count == 0)
            {
                if (actionSequence.Count >= 2)
                    rivalMove = actionSequence[actionSequence.Count - 2];
            }
            else
            {
                rivalMove = actionSequence.Last();
            }
        }

        var rivalTypeInfo = MoveDetector.GetMoveType(rivalMove);
        int rivalMoveType = rivalTypeInfo.Type;
        int rivalMoveLen = rivalTypeInfo.Len == 0 ? 1 : rivalTypeInfo.Len;

        List<List<int>> moves = new List<List<int>>();

        if (rivalMoveType == DouUtils.Type0Pass)
            moves = mg.GenAllMoves();
        else if (rivalMoveType == DouUtils.Type1Single)
            moves = MoveSelector.FilterType1Single(mg.GenType1Single(), rivalMove);
        else if (rivalMoveType == DouUtils.Type2Pair)
            moves = MoveSelector.FilterType2Pair(mg.GenType2Pair(), rivalMove);
        else if (rivalMoveType == DouUtils.Type3Triple)
            moves = MoveSelector.FilterType3Triple(mg.GenType3Triple(), rivalMove);
        else if (rivalMoveType == DouUtils.Type4Bomb)
            moves = MoveSelector.FilterType4Bomb(mg.GenType4Bomb().Concat(mg.GenType5KingBomb()).ToList(), rivalMove);
        else if (rivalMoveType == DouUtils.Type5KingBomb)
            moves = new List<List<int>>();
        else if (rivalMoveType == DouUtils.Type631)
            moves = MoveSelector.FilterType631(mg.GenType6ThreeOne(), rivalMove);
        else if (rivalMoveType == DouUtils.Type732)
            moves = MoveSelector.FilterType732(mg.GenType7ThreeTwo(), rivalMove);
        else if (rivalMoveType == DouUtils.Type8SerialSingle)
            moves = MoveSelector.FilterType8SerialSingle(mg.GenType8SerialSingle(rivalMoveLen), rivalMove);
        else if (rivalMoveType == DouUtils.Type9SerialPair)
            moves = MoveSelector.FilterType9SerialPair(mg.GenType9SerialPair(rivalMoveLen), rivalMove);
        else if (rivalMoveType == DouUtils.Type10SerialTriple)
            moves = MoveSelector.FilterType10SerialTriple(mg.GenType10SerialTriple(rivalMoveLen), rivalMove);
        else if (rivalMoveType == DouUtils.Type11Serial31)
            moves = MoveSelector.FilterTypeSerialTripleWing(mg.GenType11Serial31(rivalMoveLen), rivalMove);
        else if (rivalMoveType == DouUtils.Type12Serial32)
            moves = MoveSelector.FilterTypeSerialTripleWing(mg.GenType12Serial32(rivalMoveLen), rivalMove);
        else if (rivalMoveType == DouUtils.Type1342)
            moves = MoveSelector.FilterType1342(mg.GenType1342(), rivalMove);
        else if (rivalMoveType == DouUtils.Type14422)
            moves = MoveSelector.FilterType14422(mg.GenType14422(), rivalMove);

        if (rivalMoveType != DouUtils.Type0Pass &&
            rivalMoveType != DouUtils.Type4Bomb &&
            rivalMoveType != DouUtils.Type5KingBomb)
        {
            moves.AddRange(mg.GenType4Bomb());
            moves.AddRange(mg.GenType5KingBomb());
        }

        if (rivalMove.Count != 0)
        {
            moves.Add(new List<int>());
        }

        foreach (var m in moves)
        {
            m.Sort();
        }

        return moves;
    }

    public void Reset()
    {
        CardPlayActionSeq = new List<List<int>>();
        ThreeLandlordCards = null;
        GameOver = false;

        ActingPlayerPosition = null;
        PlayerUtilityDict = null;

        LastMoveDict = new Dictionary<string, List<int>>
        {
            { "landlord", new List<int>() },
            { "landlord_up", new List<int>() },
            { "landlord_down", new List<int>() }
        };

        PlayedCards = new Dictionary<string, List<int>>
        {
            { "landlord", new List<int>() },
            { "landlord_up", new List<int>() },
            { "landlord_down", new List<int>() }
        };

        LastMove = new List<int>();
        LastTwoMoves = new List<List<int>>();

        InfoSets = new Dictionary<string, InfoSet>
        {
            { "landlord", new InfoSet("landlord") },
            { "landlord_up", new InfoSet("landlord_up") },
            { "landlord_down", new InfoSet("landlord_down") }
        };

        BombNum = 0;
        LastPid = "landlord";
    }

    public InfoSet GetInfoset()
    {
        InfoSets[ActingPlayerPosition].LastPid = LastPid;
        InfoSets[ActingPlayerPosition].LegalActions = GetLegalCardPlayActions();
        InfoSets[ActingPlayerPosition].BombNum = BombNum;
        InfoSets[ActingPlayerPosition].LastMove = GetLastMove();
        InfoSets[ActingPlayerPosition].LastTwoMoves = GetLastTwoMoves();
        InfoSets[ActingPlayerPosition].LastMoveDict = LastMoveDict;

        InfoSets[ActingPlayerPosition].NumCardsLeftDict = new Dictionary<string, int>
        {
            { "landlord", InfoSets["landlord"].PlayerHandCards.Count },
            { "landlord_up", InfoSets["landlord_up"].PlayerHandCards.Count },
            { "landlord_down", InfoSets["landlord_down"].PlayerHandCards.Count }
        };

        InfoSets[ActingPlayerPosition].OtherHandCards = new List<int>();
        string[] positions = { "landlord", "landlord_up", "landlord_down" };
        foreach (var pos in positions)
        {
            if (pos != ActingPlayerPosition)
            {
                InfoSets[ActingPlayerPosition].OtherHandCards.AddRange(InfoSets[pos].PlayerHandCards);
            }
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

        return InfoSets[ActingPlayerPosition].Clone();
    }
}

public class InfoSet
{
    public string PlayerPosition { get; set; }
    public List<int> PlayerHandCards { get; set; }
    public Dictionary<string, int> NumCardsLeftDict { get; set; }
    public List<int> ThreeLandlordCards { get; set; }
    public List<List<int>> CardPlayActionSeq { get; set; }
    public List<int> OtherHandCards { get; set; }
    public List<List<int>> LegalActions { get; set; }
    public List<int> LastMove { get; set; }
    public List<List<int>> LastTwoMoves { get; set; }
    public Dictionary<string, List<int>> LastMoveDict { get; set; }
    public Dictionary<string, List<int>> PlayedCards { get; set; }
    public Dictionary<string, List<int>> AllHandcards { get; set; }
    public string LastPid { get; set; }
    public int BombNum { get; set; }

    public InfoSet(string playerPosition)
    {
        PlayerPosition = playerPosition;
    }

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
        {
            copy[kvp.Key] = kvp.Value == null ? new List<int>() : new List<int>(kvp.Value);
        }
        return copy;
    }
}