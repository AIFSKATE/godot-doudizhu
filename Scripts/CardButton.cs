using Godot;

public partial class CardButton : TextureButton
{
    public int CardValue { get; private set; }
    public bool IsSelected { get; private set; }
    public string CardSuit { get; private set; }

    public Vector2 BasePosition { get; private set; }
    public float BaseRotation { get; private set; }

    // 悬浮时弹出的基本方向向量。默认是 (0, -1) 即向上
    public Vector2 HoverDirection { get; set; } = new Vector2(0, -1);

    private bool _isHovered = false;

    // 🌟 新增：用于管理当前卡牌动画的 Tween 对象
    private Tween _tween;

    public CardButton()
    {
    }

    public override void _Ready()
    {
        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;
    }

    public void Initialize(int value, string suit)
    {
        CardValue = value;
        CardSuit = suit;

        string fileName = GetImageFileName(value, suit);
        string fullPath = "res://Playing Cards/PNG-cards-1.3/" + fileName;

        Texture2D tex = ResourceLoader.Load<Texture2D>(fullPath);
        if (tex != null)
        {
            TextureNormal = tex;
            IgnoreTextureSize = true;
            StretchMode = StretchModeEnum.Scale;
            CustomMinimumSize = new Vector2(100, 140);
            PivotOffset = CustomMinimumSize / 2f;
        }
        else
        {
            GD.PrintErr($"❌ 找不到卡牌图片: {fullPath}");
        }
    }

    private string GetImageFileName(int value, string suit)
    {
        if (value == 0 && suit == "back") return "CardBack.webp";

        if (value == 20) return "black_joker.png";
        if (value == 30) return "red_joker.png";

        switch (value)
        {
            case 11: return $"jack_of_{suit}2.png";
            case 12: return $"queen_of_{suit}2.png";
            case 13: return $"king_of_{suit}2.png";
            case 14: return $"ace_of_{suit}.png";
            case 17: return $"2_of_{suit}.png";
            default: return $"{value}_of_{suit}.png";
        }
    }

    // 加入 instant 参数，默认 false，这样外部 Manager 调用时不传参就会自动带有平滑发牌/重排的动画
    public void SetArchTransform(Vector2 pos, float rot, bool instant = false)
    {
        BasePosition = pos;
        BaseRotation = rot;

        if (instant)
        {
            Position = pos;
            Rotation = rot;
        }
        else
        {
            // 改变基准位置后，统一交给 UpdateTransform 重新计算并执行 Tween 动画
            UpdateTransform();
        }
    }

    private void OnMouseEntered()
    {
        _isHovered = true;

        // 🌟 核心滑动多选逻辑：当鼠标滑入且左键按住时，发射 ButtonDown 信号
        if (Input.IsMouseButtonPressed(MouseButton.Left))
        {
            EmitSignal(SignalName.ButtonDown);
        }

        UpdateTransform();
    }

    private void OnMouseExited()
    {
        _isHovered = false;
        UpdateTransform();
    }

    public void ToggleSelection(bool select)
    {
        if (IsSelected == select) return;
        IsSelected = select;
        UpdateTransform();
    }

    /// <summary>
    /// 核心动画逻辑：结合当前状态计算目标位置，并使用 Tween 平滑过渡
    /// </summary>
    private void UpdateTransform()
    {
        float offsetDistance = 0;

        if (_isHovered) offsetDistance += 15f;
        if (IsSelected) offsetDistance += 20f;

        // 计算当前卡牌应该在的最终目标位置
        Vector2 normal = HoverDirection.Rotated(BaseRotation);
        Vector2 targetPosition = BasePosition + normal * offsetDistance;

        // 🌟 动画核心：
        // 1. 如果当前卡牌正在做其他动画（比如刚滑出去你又滑进来了），先中止旧动画，防止鬼畜
        _tween?.Kill();

        // 2. 创建一个新的 Tween 动画
        _tween = CreateTween();

        // 3. SetParallel(true) 意味着接下来的多个属性变化（位置和旋转）会同时进行，而不是排队执行
        _tween.SetParallel(true);

        // 4. 设置动画曲线：Quad（二次方曲线）和 Out（缓出，即开始快，结尾慢），这是最适合 UI 的手感
        _tween.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);

        // 5. 让 Position 和 Rotation 在 0.15 秒内平滑过渡到目标值
        _tween.TweenProperty(this, "position", targetPosition, 0.1f);
        _tween.TweenProperty(this, "rotation", BaseRotation, 0.1f);
    }
}