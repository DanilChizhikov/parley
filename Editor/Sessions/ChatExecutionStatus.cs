namespace DTech.Parley.Editor.Sessions
{
    internal enum ChatExecutionStatus
    {
        Idle,
        Streaming,
        WaitingApproval,
        Error,
        Interrupted,
    }
}
