namespace QuotaFloat.Core;

/// <summary>窄浮窗账号游标；可见最多10行，上下命中与滚轮选中共享同一状态。</summary>
public sealed class AccountViewport
{
    public int Count { get; private set; }
    public int Start { get; private set; }
    public int Selected { get; private set; } = -1;
    public int VisibleCount => Math.Min(10, Count);
    public int SelectedSlot => Selected - Start;

    /// <summary>刷新账号数并接收上层按ID定位的索引；账号减少时夹紧选择。</summary>
    public void Reset(int count, int? selected = null)
    {
        Count = Math.Max(0, count);
        if (Count == 0) { Start = 0; Selected = -1; return; }
        Select(selected ?? Math.Max(0, Selected));
    }

    /// <summary>选择索引，并把超过可见范围的账号滚入视口。</summary>
    public void Select(int index)
    {
        if (Count == 0) return;
        Selected = Math.Clamp(index, 0, Count - 1);
        Start = Math.Clamp(Start, 0, Math.Max(0, Count - 10));
        if (Selected < Start) Start = Selected;
        if (Selected >= Start + VisibleCount) Start = Selected - VisibleCount + 1;
    }

    /// <summary>滚轮/键盘逐账号切换；到两端保持，不跨端循环。</summary>
    public void Step(int delta) => Select(Selected + delta);

    /// <summary>将纵向鼠标位置映射到当前可见账号，边界夹紧；空数据返回-1。</summary>
    public int HitTest(double y, double top, double height)
    {
        if (VisibleCount == 0 || !double.IsFinite(y) || height <= 0) return -1;
        var slot = Math.Clamp((int)Math.Floor((y - top) / height * VisibleCount), 0, VisibleCount - 1);
        return Start + slot;
    }

    /// <summary>账号行中心坐标，用于三色线与详情锚点的一致定位。</summary>
    public double Center(int index, double top, double height) =>
        VisibleCount == 0 ? top : top + (index - Start + 0.5) * height / VisibleCount;
}
