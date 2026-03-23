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
    [Export] private Label _lblBombCount;

    [Export] private Control _playedCardsSelf;
    [Export] private Control _playedCardsUp;
    [Export] private Control _playedCardsDown;

    [Export] private Control _handCardsContainer;
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

    // 🌟 性能优化：全局记录玩家当前的手牌实体
    private List<CardButton> _humanHandCards = new List<CardButton>();

    private static readonly string[] Suits = { "clubs", "diamonds", "hearts", "spades" };

    // 🌟 极简防作弊：全局发牌计数器。记录某个点数已经发出了几次，按顺序发花色
    private Dictionary<int, int> _globalSuitCounter = new Dictionary<int, int>();

    public override void _Ready()
    {
        CheckAndConnectUI();
        DouZeroAI.Initialize();
        StartGameLoop();
    }

    private void CheckAndConnectUI()
    {
        if (_btnPlay == null || _btnPass == null || _btnHint == null || _handCardsContainer == null)
        {
            GD.PrintErr("⚠️ UI 节点未完全赋值！请在 Godot 编辑器中拖入对应的 UI 节点。");
            return;
        }

        _btnPass.Pressed += OnBtnPassPressed;
        _btnHint.Pressed += OnBtnHintPressed;
        _btnPlay.Pressed += OnBtnPlayPressed;
    }

    private async void StartGameLoop()
    {
        _env = new Env("adp");
        _currentObs = _env.Reset();

        // 游戏开始，清空花色计数器
        _globalSuitCounter.Clear();

        InitializeHand();
        UpdateUI();

        while (!_env.GameOver)
        {
            string actingRole = _env.ActingPlayerPosition;
            List<int> chosenAction;

            if (actingRole == _humanRole)
            {
                _lblGameStatus.Text = "轮到你了，请出牌！";
                EnableActionButtons(true);

                _humanTurnTcs = new TaskCompletionSource<List<int>>();
                chosenAction = await _humanTurnTcs.Task;

                EnableActionButtons(false);
            }
            else
            {
                _lblGameStatus.Text = (actingRole == "landlord_up" ? "上家(AI)" : "下家(AI)") + " 思考中...";
                chosenAction = GetAIAction(_currentObs, actingRole);
            }

            var stepResult = _env.Step(chosenAction);
            _currentObs = stepResult.Obs;

            ShowPlayedCards(chosenAction, actingRole);
            UpdateUI();

            await ToSignal(GetTree().CreateTimer(1.0f), SceneTreeTimer.SignalName.Timeout);
        }

        string winner = _env.GameWinner;
        _lblGameStatus.Text = winner == "landlord" ? "游戏结束！地主获胜！" : "游戏结束！农民获胜！";
    }

    private void InitializeHand()
    {
        foreach (Node child in _handCardsContainer.GetChildren()) child.QueueFree();
        _selectedCards.Clear();
        _humanHandCards.Clear();

        var handCards = new List<int>(_env.GameInfoset.AllHandcards[_humanRole]);
        handCards.Sort((a, b) => b.CompareTo(a));

        foreach (int card in handCards)
        {
            // 大道至简：来一个点数，查一下它出现了几次，按顺序给花色
            int count = _globalSuitCounter.GetValueOrDefault(card, 0);
            string suit = Suits[count % 4];
            _globalSuitCounter[card] = count + 1; // 计数加一

            var btn = new CardButton();
            btn.Initialize(card, suit);
            btn.Pressed += () => OnCardClicked(btn);

            _handCardsContainer.AddChild(btn);
            _humanHandCards.Add(btn);
        }

        // 🌟 只在最初发牌时排版一次
        LayoutHandCardsArched(_humanHandCards);
    }

    private void UpdateUI()
    {
        var info = _env.GameInfoset;

        if (_lblUpFarmer != null) _lblUpFarmer.Text = $"上家 (AI)\n剩余手牌: {info.NumCardsLeftDict["landlord_up"]} 张";
        if (_lblDownFarmer != null) _lblDownFarmer.Text = $"下家 (AI)\n剩余手牌: {info.NumCardsLeftDict["landlord_down"]} 张";
        if (_lblBombCount != null) _lblBombCount.Text = $"当前炸弹数: {info.BombNum}";
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

        foreach (Node child in targetContainer.GetChildren()) child.QueueFree();

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
                    // 🌟 核心修复：优先寻找玩家真正选中的那张实体牌（防止多张同点数牌时找错花色）
                    // 兜底防错：如果异常情况没有选中的牌，再随便找一张同点数的
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
                    // AI 出牌：因为它是按需生成的，所以继续沿用全局计数器按顺序取花色
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

            // 🌟 只有当玩家自己打出了牌，手牌减少了，才需要重新排版手牌
            if (role == _humanRole)
            {
                LayoutHandCardsArched(_humanHandCards);
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

        float cardWidth = 70f;
        float cardHeight = 98f;
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

    private void LayoutPlayedCardsVertical(Control container, List<CardButton> cards)
    {
        int count = cards.Count;
        if (count == 0) return;

        float cardWidth = 70f;
        float cardHeight = 98f;
        float spacing = 30f;

        float totalHeight = cardHeight + (count - 1) * spacing;
        float centerX = (container.Size.X - cardWidth) / 2f;
        float startY = (container.Size.Y - totalHeight) / 2f;

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
            _lblGameStatus.Text = "非法出牌！请检查规则或是否能大过上家。";
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
            _lblGameStatus.Text = "当前回合你必须出牌！不能 Pass。";
        }
    }

    private void OnBtnHintPressed()
    {
        var hintAction = GetAIAction(_currentObs, _humanRole);

        if (hintAction != null && hintAction.Count > 0)
        {
            _lblGameStatus.Text = "AI 建议：出这手牌";
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
            _lblGameStatus.Text = "AI 建议：要不起，请点击【不出】！";
            foreach (var cardBtn in _selectedCards) cardBtn.ToggleSelection(false);
            _selectedCards.Clear();
        }
    }

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