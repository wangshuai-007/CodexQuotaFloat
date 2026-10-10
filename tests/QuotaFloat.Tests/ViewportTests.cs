using QuotaFloat.Core;

static class ViewportTests
{
    /// <summary>验证鼠标命中、滚轮选中、十行视口和刷新收敛。</summary>
    public static int Run()
    {
        var count = 0;
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("失败：" + name);
            Console.WriteLine("通过：" + name);
            count++;
        }
        var view = new AccountViewport();
        Check(view.HitTest(50, 48, 144) == -1, "空账号不可命中");
        view.Reset(3);
        Check(view.Selected == 0 && view.VisibleCount == 3, "三账号初始游标");
        Check(view.HitTest(49, 48, 144) == 0, "浮窗上段命中第一账号");
        Check(view.HitTest(120, 48, 144) == 1, "浮窗中段命中第二账号");
        Check(view.HitTest(191, 48, 144) == 2, "浮窗下段命中第三账号");
        Check(view.HitTest(-100, 48, 144) == 0 && view.HitTest(1000, 48, 144) == 2, "鼠标越界夹紧");
        view.Step(1);
        Check(view.Selected == 1, "少量账号滚轮切换");
        view.Step(100);
        Check(view.Selected == 2 && view.Start == 0, "少量账号滚轮到末端");
        view.Step(-100);
        Check(view.Selected == 0, "滚轮到首端");
        view.Reset(12);
        Check(view.VisibleCount == 10, "十二账号只绘十行");
        view.Select(9);
        view.Step(1);
        Check(view.Selected == 10 && view.Start == 1, "滚轮第十一账号滚入视口");
        view.Step(1);
        Check(view.Selected == 11 && view.Start == 2, "最后账号可访问");
        Check(view.HitTest(49, 48, 144) == 2 && view.HitTest(191, 48, 144) == 11, "滚动后鼠标命中当前视口");
        view.Reset(12);
        Check(view.Start == 2 && view.Selected == 11, "刷新保持滚动位置");
        view.Reset(3);
        Check(view.Start == 0 && view.Selected == 2, "账号减少收敛索引");
        view.Reset(0);
        view.Step(1);
        Check(view.Start == 0 && view.Selected == -1, "全部账号消失恢复空态");
        view.Reset(100);
        view.Select(99);
        Check(view.Start == 90 && view.VisibleCount == 10, "百账号可滚到最后十个");
        Check(Math.Abs(view.Center(99, 48, 144) - 184.8) < 0.001, "三色线与详情锚点一致");
        return count;
    }
}
