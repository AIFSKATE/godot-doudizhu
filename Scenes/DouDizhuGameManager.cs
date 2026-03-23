using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// 斗地主核心游戏管理器（纯表现层与交互层）。
/// </summary>
public partial class DouDizhuGameManager : Control
{
    // === UI 节点引用 (请在 Inspector 拖拽赋值) ===
    [Export] private Label _lblUpFarmer;
    [Export] private Label _lblDownFarmer;
    [Export] private Label _lblGameStatus;
    [Export] private Label _lblBombCount;
    [Export] private HBoxContainer _playedCardsContainer;
    [Export] private Control _handCardsContainer;
    [Export] private Button _btnPlay;
    [Export] private Button _btnPass;
    [Export] private Button _btnHint;

    // === 游戏逻辑桥梁 ===
    private Env _env;
    private string _humanRole = "landlord";
    private TaskCompletionSource<List<int>> _humanTurnTcs;
    private List<CardButton> _selectedCards = new List<CardButton>();
    private Observation _currentObs;

    private static readonly string[] Suits = { "clubs", "diamonds", "hearts", "spades" };

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

        // 【修改点 1】: 游戏刚开始，洗牌发牌后，立刻刷新一次初始 UI
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

            // 【修改点 2】: 底层环境（_env）推演完毕后，趁着还没延迟，立刻刷新 UI！
            // 这样玩家打出的牌会瞬间从手里消失，同时上/下家剩余牌数的 UI 也会瞬间跳字
            UpdateUI();
            ShowPlayedCards(chosenAction, actingRole);

            // 停顿 1 秒留给玩家观察别人（或自己）打出的牌，然后再进入下一个人的回合
            await ToSignal(GetTree().CreateTimer(1.0f), SceneTreeTimer.SignalName.Timeout);
        }

        string winner = _env.GameWinner;
        _lblGameStatus.Text = winner == "landlord" ? "游戏结束！地主获胜！" : "游戏结束！农民获胜！";
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
            foreach (CardButton child in _handCardsContainer.GetChildren())
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

    private void UpdateUI()
    {
        var info = _env.GameInfoset;

        if (_lblUpFarmer != null) _lblUpFarmer.Text = $"上家 (AI)\n剩余手牌: {info.NumCardsLeftDict["landlord_up"]} 张";
        if (_lblDownFarmer != null) _lblDownFarmer.Text = $"下家 (AI)\n剩余手牌: {info.NumCardsLeftDict["landlord_down"]} 张";
        if (_lblBombCount != null) _lblBombCount.Text = $"当前炸弹数: {info.BombNum}";

        // 将旧卡牌标记为待删除
        foreach (Node child in _handCardsContainer.GetChildren()) child.QueueFree();
        _selectedCards.Clear();

        var handCards = new List<int>(info.AllHandcards[_humanRole]);
        handCards.Sort((a, b) => b.CompareTo(a));

        Dictionary<int, int> suitCounter = new Dictionary<int, int>();

        List<CardButton> newHandCards = new List<CardButton>();

        foreach (int card in handCards)
        {
            int count = suitCounter.GetValueOrDefault(card, 0);
            string suit = Suits[count % 4];
            suitCounter[card] = count + 1;

            var btn = new CardButton();
            btn.Initialize(card, suit);
            btn.Pressed += () => OnCardClicked(btn);

            _handCardsContainer.AddChild(btn);
            newHandCards.Add(btn);
        }

        LayoutHandCardsArched(newHandCards);
    }

    private void LayoutHandCardsArched(List<CardButton> activeCards)
    {
        int count = activeCards.Count;
        if (count == 0) return;

        float radius = 800f;
        float spacing = 30f;

        float angleStep = spacing / radius;
        float totalAngle = (count - 1) * angleStep;
        float startAngle = -totalAngle / 2f;

        Vector2 containerCenter = _handCardsContainer.Size / 2f;
        Vector2 circleCenter = containerCenter + new Vector2(0, radius);

        for (int i = 0; i < count; i++)
        {
            var card = activeCards[i];

            float angle = startAngle + i * angleStep;

            float x = circleCenter.X + Mathf.Sin(angle) * radius;
            float y = circleCenter.Y - Mathf.Cos(angle) * radius;

            Vector2 finalPos = new Vector2(x, y) - card.CustomMinimumSize / 2f;

            card.SetArchTransform(finalPos, angle);
        }
    }

    private void OnCardClicked(CardButton btn)
    {
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

    private void ShowPlayedCards(List<int> cards, string role)
    {
        if (_playedCardsContainer == null) return;
        foreach (Node child in _playedCardsContainer.GetChildren()) child.QueueFree();

        string roleName = role == "landlord" ? "你" : (role == "landlord_up" ? "上家" : "下家");
        Label lblInfo = new Label { Text = roleName + (cards.Count == 0 ? " : 不出" : " 打出:") };
        _playedCardsContainer.AddChild(lblInfo);

        if (cards.Count > 0)
        {
            var displayCards = new List<int>(cards);
            displayCards.Sort((a, b) => b.CompareTo(a));

            Dictionary<int, int> suitCounter = new Dictionary<int, int>();

            foreach (var c in displayCards)
            {
                int count = suitCounter.GetValueOrDefault(c, 0);
                string suit = Suits[count % 4];
                suitCounter[c] = count + 1;

                var displayBtn = new CardButton();
                displayBtn.Initialize(c, suit);
                displayBtn.Disabled = true;
                displayBtn.CustomMinimumSize = new Vector2(70, 98);
                _playedCardsContainer.AddChild(displayBtn);
            }
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