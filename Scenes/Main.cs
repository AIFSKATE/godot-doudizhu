using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Main : Control
{
    private GameEnv _game = new GameEnv();
    private List<int> _fullDeck = new List<int>(54);

    public override void _Ready()
    {
        GD.Print("游戏启动...");
        DouZeroAI.Initialize(); // 初始化 AI 推理引擎

        InitDeck();
        StartNewGame();
    }

    private void InitDeck()
    {
        _fullDeck.Clear();
        for (int i = 3; i <= 14; i++) for (int j = 0; j < 4; j++) _fullDeck.Add(i);
        for (int j = 0; j < 4; j++) _fullDeck.Add(17);
        _fullDeck.Add(20); _fullDeck.Add(30);
    }

    public void StartNewGame()
    {
        var shuffled = _fullDeck.OrderBy(x => Guid.NewGuid()).ToList();
        var hands = new Dictionary<string, List<int>>
        {
            {"landlord", shuffled.GetRange(0, 20)},
            {"landlord_down", shuffled.GetRange(20, 17)},
            {"landlord_up", shuffled.GetRange(37, 17)}
        };

        _game.CardPlayInit(hands);
        GD.Print("新对局开始。");
    }

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete)
        {
            GD.Print("清理 AI 资源...");
            DouZeroAI.Dispose();
        }
    }
}