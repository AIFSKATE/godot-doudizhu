using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class DouDizhuGameManager : Control
{
    // === UI 节点引用 ===
    [Export] private Label _lblUpFarmer;
    [Export] private Label _lblDownFarmer;
    [Export] private Label _lblGameStatus;
    [Export] private Label _lblUpFarmerScore;
    [Export] private Label _lblDownFarmerScore;
    [Export] private Label _lblGameStatusScore;
    [Export] private Label _lblBombCount;

    [Export] private Control _playedCardsSelf;
    [Export] private Control _playedCardsUp;
    [Export] private Control _playedCardsDown;

    [Export] private Control _upCardsContainer;
    [Export] private Control _downCardsContainer;

    [Export] private Control _handCardsContainer;

    [Export] private Button _btnStart;
    [Export] private Button _btnPlay;
    [Export] private Button _btnPass;
    [Export] private Button _btnHint;

    // === 卡牌排版视觉参数 ===
    [Export(PropertyHint.Range, "100, 2000, 10")]
    private float _archRadius = 2000f;

    [Export(PropertyHint.Range, "10, 100, 1")]
    private float _archSpacing = 40f;

    // === 游戏逻辑桥梁 ===
    private Env _env;
    private string _humanRole = "landlord";
    private TaskCompletionSource<List<int>> _humanTurnTcs;
    private List<CardButton> _selectedCards = new List<CardButton>();
    private Observation _currentObs;

    // 全局记录玩家当前的手牌实体
    private List<CardButton> _humanHandCards = new List<CardButton>();

    private static readonly string[] Suits = { "clubs", "diamonds", "hearts", "spades" };

    // 全局发牌计数器
    private Dictionary<int, int> _globalSuitCounter = new Dictionary<int, int>();

    private const int ScoreBase = 100;
    private string _statusMessage = "";

    private class PlayerBattleStats
    {
        public int Wins;
        public int Losses;
        public int Score;
    }

    private readonly Dictionary<string, PlayerBattleStats> _playerStats = new Dictionary<string, PlayerBattleStats>
    {
        { "landlord", new PlayerBattleStats() },
        { "landlord_up", new PlayerBattleStats() },
        { "landlord_down", new PlayerBattleStats() }
    };

    public override void _Ready()
    {
        CheckAndConnectUI();
        DouZeroAI.Initialize();

        // 🌟 游戏加载完毕，不直接开始，而是进入待机状态
        EnterStandbyState();
    }

    private void CheckAndConnectUI()
    {
        // 加入了对 _btnStart 的非空校验
        if (_btnPlay == null || _btnPass == null || _btnHint == null || _handCardsContainer == null || _btnStart == null)
        {
            GD.PrintErr("⚠️ UI 节点未完全赋值！请在 Godot 编辑器中拖入对应的 UI 节点。");
            return;
        }

        _btnPass.Pressed += OnBtnPassPressed;
        _btnHint.Pressed += OnBtnHintPressed;
        _btnPlay.Pressed += OnBtnPlayPressed;

        // 🌟 绑定开始按钮事件
        _btnStart.Pressed += OnBtnStartPressed;
    }

    // 🌟 辅助方法：立即从树中移除子节点并排队释放，解决 QueueFree 的帧延迟读取问题
    private void ClearContainer(Control container)
    {
        if (container == null) return;
        foreach (Node child in container.GetChildren())
        {
            container.RemoveChild(child); // 立即脱离父节点，确保 GetChildCount() 瞬间归零
            child.QueueFree();            // 安全释放内存
        }
    }

    // 🌟 待机状态：显示开始按钮，隐藏所有出牌相关 UI
    private void EnterStandbyState(bool keepStatusMessage = false)
    {
        _btnStart.Visible = true;
        _btnStart.Disabled = false;

        SetActionButtonsVisible(false);

        if (!keepStatusMessage)
        {
            SetStatusMessage("点击【开始游戏】进入对局");
        }
        else
        {
            RefreshGameStatusLabel();
        }

        RefreshFarmerLabels();

        if (_lblBombCount != null) _lblBombCount.Text = "";
    }

    // 🌟 点击开始按钮：清理残局，切换 UI 状态，启动主循环
    private void OnBtnStartPressed()
    {
        _btnStart.Visible = false;      // 隐藏开始按钮
        SetActionButtonsVisible(true);  // 显示行动按钮（此时先禁用，等轮到玩家再激活）
        EnableActionButtons(false);

        ClearBoard();                   // 大扫除
        StartGameLoop();                // 发车！
    }

    // 🌟 极其重要的残局清理逻辑
    private void ClearBoard()
    {
        // 1. 使用新增的 ClearContainer 方法，立刻清空所有物理节点
        ClearContainer(_handCardsContainer);
        ClearContainer(_playedCardsSelf);
        ClearContainer(_playedCardsUp);
        ClearContainer(_playedCardsDown);
        ClearContainer(_upCardsContainer);
        ClearContainer(_downCardsContainer);

        // 2. 清空所有的逻辑集合与发牌器状态
        _humanHandCards.Clear();
        _selectedCards.Clear();
        _globalSuitCounter.Clear();
    }

    private async void StartGameLoop()
    {
        _env = new Env("adp");
        _currentObs = _env.Reset();

        InitializeHand();
        UpdateUI();

        while (!_env.GameOver)
        {
            string actingRole = _env.ActingPlayerPosition;
            List<int> chosenAction;

            if (actingRole == _humanRole)
            {
                SetStatusMessage("轮到你了，请出牌！");
                EnableActionButtons(true);

                _humanTurnTcs = new TaskCompletionSource<List<int>>();
                chosenAction = await _humanTurnTcs.Task;

                EnableActionButtons(false);
            }
            else
            {
                SetStatusMessage((actingRole == "landlord_up" ? "上家(AI)" : "下家(AI)") + " 思考中...");
                chosenAction = GetAIAction(_currentObs, actingRole);
            }

            var stepResult = _env.Step(chosenAction);
            _currentObs = stepResult.Obs;

            ShowPlayedCards(chosenAction, actingRole);
            UpdateUI();

            await ToSignal(GetTree().CreateTimer(0.5f), SceneTreeTimer.SignalName.Timeout);
        }

        string winner = _env.GameWinner;
        int bombNum = _env.GameBombNum;
        ApplyRoundResult(winner, bombNum);

        EnterStandbyState(keepStatusMessage: true);
    }

    private void InitializeHand()
    {
        ClearContainer(_handCardsContainer); // 同样使用新的立刻清理方法

        var handCards = new List<int>(_env.GameInfoset.AllHandcards[_humanRole]);
        handCards.Sort((a, b) => b.CompareTo(a));

        foreach (int card in handCards)
        {
            int count = _globalSuitCounter.GetValueOrDefault(card, 0);
            string suit = Suits[count % 4];
            _globalSuitCounter[card] = count + 1;

            var btn = new CardButton();
            btn.Initialize(card, suit);
            btn.ButtonDown += () => OnCardClicked(btn);
            _handCardsContainer.AddChild(btn);
            _humanHandCards.Add(btn);
        }

        LayoutHandCardsArched(_humanHandCards);
    }

    private void UpdateUI()
    {
        var info = _env.GameInfoset;

        int upCardsLeft = info.NumCardsLeftDict["landlord_up"];
        int downCardsLeft = info.NumCardsLeftDict["landlord_down"];

        RefreshFarmerLabels(upCardsLeft, downCardsLeft);
        RefreshGameStatusLabel();
        if (_lblBombCount != null) _lblBombCount.Text = $"当前炸弹数: {info.BombNum}";

        UpdateHiddenCards(_upCardsContainer, upCardsLeft, "up");
        UpdateHiddenCards(_downCardsContainer, downCardsLeft, "down");
    }

    private void SetStatusMessage(string message)
    {
        _statusMessage = message ?? "";
        RefreshGameStatusLabel();
    }

    private void RefreshGameStatusLabel()
    {
        if (_lblGameStatus == null) return;

        var landlordStats = _playerStats["landlord"];
        _lblGameStatus.Text =
            $"{_statusMessage}\n地主(你) 战绩: {landlordStats.Wins}胜{landlordStats.Losses}负  积分: {FormatSignedScore(landlordStats.Score)}";
    }

    private void RefreshFarmerLabels(int? upCardsLeft = null, int? downCardsLeft = null)
    {
        string upCardsText = upCardsLeft.HasValue ? upCardsLeft.Value.ToString() : "--";
        string downCardsText = downCardsLeft.HasValue ? downCardsLeft.Value.ToString() : "--";

        var upStats = _playerStats["landlord_up"];
        var downStats = _playerStats["landlord_down"];

        if (_lblUpFarmer != null)
        {
            _lblUpFarmer.Text =
                $"上家 (AI)\n剩余手牌: {upCardsText} 张\n战绩: {upStats.Wins}胜{upStats.Losses}负\n积分: {FormatSignedScore(upStats.Score)}";
        }

        if (_lblDownFarmer != null)
        {
            _lblDownFarmer.Text =
                $"下家 (AI)\n剩余手牌: {downCardsText} 张\n战绩: {downStats.Wins}胜{downStats.Losses}负\n积分: {FormatSignedScore(downStats.Score)}";
        }
    }

    private void ApplyRoundResult(string winner, int bombNum)
    {
        int multiplier = Math.Max(1, (int)Math.Pow(2, bombNum));
        int farmerDelta = ScoreBase * multiplier;
        int landlordDelta = farmerDelta * 2;
        bool landlordWin = winner == "landlord";

        UpdatePlayerStats("landlord", landlordWin, landlordDelta);
        UpdatePlayerStats("landlord_up", !landlordWin, farmerDelta);
        UpdatePlayerStats("landlord_down", !landlordWin, farmerDelta);

        string winnerText = landlordWin ? "游戏结束！地主(你)获胜！" : "游戏结束！农民获胜！";
        int landlordRoundDelta = landlordWin ? landlordDelta : -landlordDelta;
        int farmerRoundDelta = landlordWin ? -farmerDelta : farmerDelta;

        SetStatusMessage(
            $"{winnerText} 炸弹: {bombNum} 倍率: x{multiplier}\n本局分数 地主: {FormatSignedScore(landlordRoundDelta)}  农民: {FormatSignedScore(farmerRoundDelta)}");
        RefreshFarmerLabels();
    }

    private void UpdatePlayerStats(string role, bool isWin, int delta)
    {
        if (!_playerStats.TryGetValue(role, out var stats)) return;

        if (isWin)
        {
            stats.Wins += 1;
            stats.Score += delta;
        }
        else
        {
            stats.Losses += 1;
            stats.Score -= delta;
        }
    }

    private string FormatSignedScore(int score)
    {
        return score > 0 ? $"+{score}" : score.ToString();
    }

    private void UpdateHiddenCards(Control container, int targetCount, string role)
    {
        if (container == null) return;

        int currentCount = container.GetChildCount();
        Vector2 hoverDir = role == "up" ? new Vector2(1, 0) : new Vector2(-1, 0);

        if (currentCount > targetCount)
        {
            int cardsToRemove = currentCount - targetCount;
            for (int i = 0; i < cardsToRemove; i++)
            {
                Node child = container.GetChild(container.GetChildCount() - 1);
                container.RemoveChild(child);
                child.QueueFree();
            }
            return;
        }
        else if (currentCount < targetCount)
        {
            int cardsToAdd = targetCount - currentCount;
            for (int i = 0; i < cardsToAdd; i++)
            {
                var backBtn = new CardButton();
                backBtn.Initialize(0, "back");
                backBtn.HoverDirection = hoverDir;
                backBtn.CustomMinimumSize = new Vector2(100, 140);
                container.AddChild(backBtn);
            }

            List<CardButton> hiddenCardsList = new List<CardButton>();
            foreach (Node child in container.GetChildren())
            {
                if (child is CardButton cb)
                {
                    hiddenCardsList.Add(cb);
                }
            }

            if (hiddenCardsList.Count > 0)
            {
                LayoutPlayedCardsVertical(container, hiddenCardsList, spacing: 20f);
            }
        }
    }

    private void ShowPlayedCards(List<int> cards, string role)
    {
        Control targetContainer = null;
        Vector2 hoverDir = new Vector2(0, -1);
        bool isVerticalLayout = false;

        if (role == _humanRole)
        {
            targetContainer = _playedCardsSelf;
            hoverDir = new Vector2(0, -1);
            isVerticalLayout = false;
        }
        else if (role == "landlord_up")
        {
            targetContainer = _playedCardsUp;
            hoverDir = new Vector2(1, 0);
            isVerticalLayout = true;
        }
        else if (role == "landlord_down")
        {
            targetContainer = _playedCardsDown;
            hoverDir = new Vector2(-1, 0);
            isVerticalLayout = true;
        }

        if (targetContainer == null) return;

        ClearContainer(targetContainer); // 同样使用立刻清理方法，防止重叠和闪烁

        if (cards.Count == 0)
        {
            Label lblPass = new Label { Text = "不出" };
            lblPass.AddThemeFontSizeOverride("font_size", 24);
            targetContainer.AddChild(lblPass);
            lblPass.Position = targetContainer.Size / 2f - new Vector2(24, 12);
        }
        else
        {
            var displayCards = new List<int>(cards);
            displayCards.Sort((a, b) => b.CompareTo(a));

            List<CardButton> newPlayedCards = new List<CardButton>();

            foreach (var c in displayCards)
            {
                if (role == _humanRole)
                {
                    CardButton matchedBtn = _humanHandCards.FirstOrDefault(cb => cb.CardValue == c && cb.IsSelected)
                                         ?? _humanHandCards.FirstOrDefault(cb => cb.CardValue == c);

                    if (matchedBtn != null)
                    {
                        _humanHandCards.Remove(matchedBtn);
                        _handCardsContainer.RemoveChild(matchedBtn);

                        if (matchedBtn.IsSelected)
                        {
                            matchedBtn.ToggleSelection(false);
                            _selectedCards.Remove(matchedBtn);
                        }

                        targetContainer.AddChild(matchedBtn);
                        matchedBtn.HoverDirection = hoverDir;
                        matchedBtn.CustomMinimumSize = new Vector2(70, 98);

                        newPlayedCards.Add(matchedBtn);
                    }
                }
                else
                {
                    int count = _globalSuitCounter.GetValueOrDefault(c, 0);
                    string suit = Suits[count % 4];
                    _globalSuitCounter[c] = count + 1;

                    var displayBtn = new CardButton();
                    displayBtn.Initialize(c, suit);
                    displayBtn.HoverDirection = hoverDir;
                    displayBtn.CustomMinimumSize = new Vector2(70, 98);
                    targetContainer.AddChild(displayBtn);
                    newPlayedCards.Add(displayBtn);
                }
            }

            if (isVerticalLayout)
                LayoutPlayedCardsVertical(targetContainer, newPlayedCards);
            else
                LayoutPlayedCardsLinear(targetContainer, newPlayedCards);

            if (role == _humanRole)
            {
                LayoutHandCardsArched(_humanHandCards);
            }
        }
    }

    public override void _Input(InputEvent @event)
    {
        // 🌟 新增：全局鼠标右键检测
        if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Right && mb.Pressed)
        {
            if (_selectedCards.Count > 0)
            {
                // 如果当前有选中的牌，则一键取消选中
                foreach (var cardBtn in _selectedCards)
                {
                    cardBtn.ToggleSelection(false);
                }
                _selectedCards.Clear();
            }
            else
            {
                // 如果当前没有选中的牌，则一键选中所有手牌
                foreach (var cardBtn in _humanHandCards)
                {
                    cardBtn.ToggleSelection(true);
                    _selectedCards.Add(cardBtn);
                }
            }
        }
    }

    private void OnCardClicked(CardButton btn)
    {
        if (!_humanHandCards.Contains(btn)) return;

        if (btn.IsSelected)
        {
            btn.ToggleSelection(false);
            _selectedCards.Remove(btn);
        }
        else
        {
            btn.ToggleSelection(true);
            _selectedCards.Add(btn);
        }
    }

    private void LayoutHandCardsArched(List<CardButton> activeCards)
    {
        int count = activeCards.Count;
        if (count == 0) return;

        float angleStep = _archSpacing / _archRadius;
        float totalAngle = (count - 1) * angleStep;
        float startAngle = -totalAngle / 2f;

        Vector2 containerCenter = _handCardsContainer.Size / 2f;
        Vector2 circleCenter = containerCenter + new Vector2(0, _archRadius);

        for (int i = 0; i < count; i++)
        {
            var card = activeCards[i];
            float angle = startAngle + i * angleStep;
            float x = circleCenter.X + Mathf.Sin(angle) * _archRadius;
            float y = circleCenter.Y - Mathf.Cos(angle) * _archRadius;
            Vector2 finalPos = new Vector2(x, y) - card.CustomMinimumSize / 2f;
            card.SetArchTransform(finalPos, angle);
        }
    }

    private void LayoutPlayedCardsLinear(Control container, List<CardButton> cards)
    {
        int count = cards.Count;
        if (count == 0) return;

        float cardWidth = cards[0].CustomMinimumSize.X;
        float cardHeight = cards[0].CustomMinimumSize.Y;
        float spacing = 30f;

        float totalWidth = cardWidth + (count - 1) * spacing;
        float startX = (container.Size.X - totalWidth) / 2f;
        float centerY = (container.Size.Y - cardHeight) / 2f;

        for (int i = 0; i < count; i++)
        {
            var card = cards[i];
            Vector2 finalPos = new Vector2(startX + i * spacing, centerY);
            card.SetArchTransform(finalPos, 0f);
        }
    }

    private void LayoutPlayedCardsVertical(Control container, List<CardButton> cards, float spacing = 30f)
    {
        int count = cards.Count;
        if (count == 0) return;

        float cardWidth = cards[0].CustomMinimumSize.X;
        float centerX = (container.Size.X - cardWidth) / 2f;
        float startY = 0f;

        for (int i = 0; i < count; i++)
        {
            var card = cards[i];
            Vector2 finalPos = new Vector2(centerX, startY + i * spacing);
            card.SetArchTransform(finalPos, 0f);
        }
    }

    private List<int> GetAIAction(Observation obs, string role)
    {
        var legalActions = obs.LegalActions;
        if (legalActions.Count == 0) return new List<int>();
        if (legalActions.Count == 1) return legalActions[0];

        int actionIdx = 0;
        try
        {
            float[] xFlat = Flatten2D(obs.XBatch);
            float[] zFlat = Flatten3D(obs.ZBatch);

            if (role == "landlord") actionIdx = DouZeroAI.GetLandlordAction(zFlat, xFlat);
            else if (role == "landlord_up") actionIdx = DouZeroAI.GetUpFarmerAction(zFlat, xFlat);
            else if (role == "landlord_down") actionIdx = DouZeroAI.GetDownFarmerAction(zFlat, xFlat);

            if (actionIdx < 0 || actionIdx >= legalActions.Count) actionIdx = 0;
        }
        catch (Exception e)
        {
            GD.PrintErr("AI 推理失败: " + e.Message);
            var validActions = legalActions.Where(a => a.Count > 0).ToList();
            if (validActions.Count > 0) return validActions[new Random().Next(validActions.Count)];
        }

        return legalActions[actionIdx];
    }

    private void OnBtnPlayPressed()
    {
        List<int> action = _selectedCards.Select(c => c.CardValue).ToList();
        action.Sort();

        bool isLegal = _env.GameInfoset.LegalActions.Any(legal => legal.SequenceEqual(action));

        if (isLegal)
        {
            _humanTurnTcs?.TrySetResult(action);
        }
        else
        {
            SetStatusMessage("非法出牌！请检查规则或是否能大过上家。");
        }
    }

    private void OnBtnPassPressed()
    {
        var action = new List<int>();
        bool canPass = _env.GameInfoset.LegalActions.Any(la => la.Count == 0);

        if (canPass)
        {
            _humanTurnTcs?.TrySetResult(action);
        }
        else
        {
            SetStatusMessage("当前回合你必须出牌！不能 Pass。");
        }
    }

    private void OnBtnHintPressed()
    {
        var hintAction = GetAIAction(_currentObs, _humanRole);

        if (hintAction != null && hintAction.Count > 0)
        {
            SetStatusMessage("AI 建议：出这手牌。");
            foreach (var cardBtn in _selectedCards) cardBtn.ToggleSelection(false);
            _selectedCards.Clear();

            var tempHint = new List<int>(hintAction);

            foreach (CardButton child in _humanHandCards)
            {
                if (tempHint.Contains(child.CardValue))
                {
                    child.ToggleSelection(true);
                    _selectedCards.Add(child);
                    tempHint.Remove(child.CardValue);
                }
            }
        }
        else
        {
            SetStatusMessage("AI 建议：要不起，请点击【不出】！");
            foreach (var cardBtn in _selectedCards) cardBtn.ToggleSelection(false);
            _selectedCards.Clear();
        }
    }

    // 🌟 控制行动按钮的显示与隐藏
    private void SetActionButtonsVisible(bool visible)
    {
        if (_btnPlay != null) _btnPlay.Visible = visible;
        if (_btnPass != null) _btnPass.Visible = visible;
        if (_btnHint != null) _btnHint.Visible = visible;
    }

    // 控制行动按钮的交互状态
    private void EnableActionButtons(bool enable)
    {
        if (_btnPlay != null) _btnPlay.Disabled = !enable;
        if (_btnPass != null) _btnPass.Disabled = !enable;
        if (_btnHint != null) _btnHint.Disabled = !enable;
    }

    private float[] Flatten2D(float[][] arr)
    {
        if (arr == null) return new float[0];
        return arr.SelectMany(x => x).ToArray();
    }

    private float[] Flatten3D(float[][][] arr)
    {
        if (arr == null) return new float[0];
        return arr.SelectMany(x => x).SelectMany(y => y).ToArray();
    }
}