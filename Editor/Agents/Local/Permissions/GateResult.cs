namespace DTech.Parley.Editor.Agents.Local
{
    internal readonly struct GateResult
    {
        public GateVerdict Verdict { get; }
        public string Reason { get; }

        public GateResult(GateVerdict verdict, string reason)
        {
            Verdict = verdict;
            Reason = reason;
        }
    }
}