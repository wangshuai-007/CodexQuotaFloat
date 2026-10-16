using System.Runtime.InteropServices;
using System.Windows;

namespace QuotaFloat;

public partial class MainWindow
{
    private void CheckHiddenAttachmentAnchor(Window client, IntPtr handle, List<string> checks)
    {
        Collapse();
        client.Activate();
        var helper = CreateAnchorHelper(0, "STATIC", "", 0x80000000,
            0, 0, 0, 0, handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        Check(helper != IntPtr.Zero, "创建隐藏Owned辅助窗口复现输入法层级现场", checks);
        try
        {
            Check(FollowTestSetWindowPos(helper, IntPtr.Zero, 0, 0, 0, 0, 0x0013),
                "隐藏辅助窗口排在客户端之前", checks);
            Check(LayerPreviousWindow(handle, 3) == helper,
                "客户端的前一个窗口确实是隐藏Owned辅助窗口", checks);
            Check(ClientAttachment.AttachmentAnchor(handle, OwnHandles.ToHashSet()) != helper,
                "附着锚点明确排除隐藏Owned辅助窗口", checks);
            Check(ClientAttachment.Place(this, handle, OwnHandles, FollowOffset) && AppearsAbove(this, client),
                "隐藏Owned辅助窗口存在时浮窗仍排在客户端上方", checks);
            Check(!IsNativeTopmost(this), "修复隐藏锚点不依赖全局置顶", checks);
            CheckPassiveRecovery(client, handle, checks);
            CheckTopmostAttachmentAnchor(client, handle, checks);
        }
        finally { DestroyAnchorHelper(helper); }
    }

    private void CheckPassiveRecovery(Window client, IntPtr handle, List<string> checks)
    {
        var window = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        Check(FollowTestSetWindowPos(window, handle, 0, 0, 0, 0, 0x0213) &&
            !ClientAttachment.IsAbove(window, handle), "构造原生移动成功但实际仍在客户端后方", checks);
        var foreground = LayerForegroundWindow();
        Check(ClientAttachment.RecoverLayer(window, handle, IntPtr.Zero) && AppearsAbove(this, client) &&
            !IsNativeTopmost(this) && LayerForegroundWindow() == foreground,
            "被动恢复后实际高于客户端、撤销置顶且不抢焦点", checks);
    }

    private void CheckTopmostAttachmentAnchor(Window client, IntPtr handle, List<string> checks)
    {
        var top = new Window { Title = "置顶锚点回归", Width = 80, Height = 60,
            Topmost = true, ShowActivated = false, ShowInTaskbar = false };
        try
        {
            top.Show(); client.Activate();
            Check(FollowTestSetWindowPos(new System.Windows.Interop.WindowInteropHelper(this).Handle,
                handle, 0, 0, 0, 0, 0x0013), "构造非置顶浮窗落在客户端后面", checks);
            Check(ClientAttachment.Place(this, handle, OwnHandles, FollowOffset) &&
                AppearsAbove(this, client) && AppearsAbove(top, this) && !IsNativeTopmost(this),
                "前方只有置顶窗口时仍恢复普通层级，且不盖住置顶窗口", checks);
        }
        finally { top.Close(); }
    }

    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateAnchorHelper(uint exStyle, string className, string name,
        uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);

    [DllImport("user32.dll", EntryPoint = "DestroyWindow")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyAnchorHelper(IntPtr handle);
}
