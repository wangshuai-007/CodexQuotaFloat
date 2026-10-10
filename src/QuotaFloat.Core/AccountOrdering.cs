namespace QuotaFloat.Core;

/// <summary>只改变本地显示顺序，不修改CPA账号或调度。</summary>
public static class AccountOrdering
{
    /// <summary>已有偏好优先，新账号按来源顺序追加；忽略重复和缺失ID。</summary>
    public static IReadOnlyList<AccountQuota> Apply(IReadOnlyList<AccountQuota> accounts, IEnumerable<string> order)
    {
        var ranks = order.Distinct(StringComparer.Ordinal).Select((id, index) => (id, index))
            .ToDictionary(pair => pair.id, pair => pair.index, StringComparer.Ordinal);
        return accounts.OrderBy(account => ranks.GetValueOrDefault(account.Id, int.MaxValue)).ToArray();
    }

    /// <summary>把账号移到夹紧后的目标位置，保留暂时不可见账号的偏好。</summary>
    public static List<string> Move(IReadOnlyList<AccountQuota> accounts, IEnumerable<string> order, string id, int destination)
    {
        var ids = Apply(accounts, order).Select(account => account.Id).Distinct(StringComparer.Ordinal).ToList();
        if (!ids.Remove(id)) return order.Distinct(StringComparer.Ordinal).ToList();
        ids.Insert(Math.Clamp(destination, 0, ids.Count), id);
        ids.AddRange(order.Where(previous => !ids.Contains(previous, StringComparer.Ordinal)).Distinct(StringComparer.Ordinal));
        return ids;
    }
}
