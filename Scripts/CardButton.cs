using Godot;

public partial class CardButton : TextureButton
{
    public int CardValue { get; private set; }
    public bool IsSelected { get; private set; }
    public string CardSuit { get; private set; }

    public Vector2 BasePosition { get; private set; }
    public float BaseRotation { get; private set; }

    // 【新增】：悬浮时弹出的基本方向向量。默认是 (0, -1) 即向上
    public Vector2 HoverDirection { get; set; } = new Vector2(0, -1);

    private bool _isHovered = false;

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

    public void SetArchTransform(Vector2 pos, float rot)
    {
        BasePosition = pos;
        BaseRotation = rot;
        Position = pos;
        Rotation = rot;
    }

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

    private void UpdateTransform()
    {
        float offsetDistance = 0;

        if (_isHovered) offsetDistance += 15f;
        if (IsSelected) offsetDistance += 20f;

        // 【核心修改】：现在不再定死为向上，而是根据外部指定的 HoverDirection 进行旋转和偏移
        Vector2 normal = HoverDirection.Rotated(BaseRotation);

        Position = BasePosition + normal * offsetDistance;
    }
}