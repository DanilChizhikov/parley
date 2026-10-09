namespace DTech.Parley.Editor.UI
{
    internal readonly struct DiffLine
    {
        public DiffOperation Operation { get; }
        public string Text { get; }
        public int OldNumber { get; }
        public int NewNumber { get; }

        public DiffLine(
            DiffOperation operation,
            string text,
            int oldNumber,
            int newNumber)
        {
            Operation = operation;
            Text = text;
            OldNumber = oldNumber;
            NewNumber = newNumber;
        }
    }
}