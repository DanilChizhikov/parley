namespace DTech.Parley.Editor.Tools
{
    internal readonly struct EnumSchemaRequest
    {
        public string Name { get; }
        public string Description { get; }
        public string[] Values { get; }
        public bool Required { get; }

        public EnumSchemaRequest(
            string name,
            string description,
            string[] values,
            bool required = false)
        {
            Name = name;
            Description = description;
            Values = values;
            Required = required;
        }
    }
}