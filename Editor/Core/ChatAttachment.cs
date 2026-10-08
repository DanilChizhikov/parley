namespace DTech.Parley.Editor
{
    internal sealed class ChatAttachment
    {
        public AttachmentKind Kind { get; set; }
        public string Label { get; set; }
        public string Text { get; set; }
        public string MediaType { get; set; }
        public string Base64 { get; set; }
    }
}