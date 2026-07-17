namespace MojiCollaTool
{
    /// <summary>
    /// 保存処理を持たないworkspaceがdirty sessionを閉じるときの方針です。
    /// </summary>
    public enum CloseSessionPolicy
    {
        RejectIfDirty,
        DiscardChanges,
        AllowDirty,
    }
}
