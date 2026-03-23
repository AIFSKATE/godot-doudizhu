using Godot;

/// <summary>
/// 独立的卡牌 UI 组件。
/// 支持设定在圆弧上的基准位置，并实现鼠标悬浮、选中时的法线外扩效果。
/// </summary>
public partial class CardButton : TextureButton
{
    public int CardValue { get; private set; }
    public bool IsSelected { get; private set; }
    public string CardSuit { get; private set; }

    // 记录卡牌在拱形上的基准位置和旋转角度
    public Vector2 BasePosition { get; private set; }
    public float BaseRotation { get; private set; }

    private bool _isHovered = false;

    public CardButton()
    {
    }

    public override void _Ready()
    {
        // 绑定鼠标悬浮与移出事件
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

            // 极其重要：将旋转中心点设置在卡牌的几何中心
            PivotOffset = CustomMinimumSize / 2f;
        }
        else
        {
            GD.PrintErr($"❌ 找不到卡牌图片: {fullPath}");
        }
    }

    private string GetImageFileName(int value, string suit)
    {
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

    /// <summary>
    /// 由主控程序调用，设定卡牌在圆弧上的固定落点和角度
    /// </summary>
    public void SetArchTransform(Vector2 pos, float rot)
    {
        BasePosition = pos;
        BaseRotation = rot;
        Position = pos;
        Rotation = rot;
    }

    // --- 交互与动画逻辑 ---

    private void OnMouseEntered()
    {
        _isHovered = true;
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
    /// 核心逻辑：沿法线方向移动
    /// </summary>
    private void UpdateTransform()
    {
        float offsetDistance = 0;

        // 悬浮时向外延展 15px，选中时再叠加 20px
        if (_isHovered) offsetDistance += 15f;
        if (IsSelected) offsetDistance += 20f;

        // 计算法线向量：向上的基本法线 (0, -1) 根据卡牌的旋转角度进行旋转
        Vector2 normal = new Vector2(0, -1).Rotated(BaseRotation);

        // 卡牌当前位置 = 基准位置 + 法线方向 * 偏移距离
        Position = BasePosition + normal * offsetDistance;
    }
}