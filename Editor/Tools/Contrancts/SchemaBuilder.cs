using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Tools
{
	internal sealed class SchemaBuilder
	{
		private readonly JObject _properties = new ();
		private readonly JArray _required = new ();

		public SchemaBuilder String(string name, string description, bool required = false)
		{
			return Add(name, new JObject { ["type"] = "string", ["description"] = description }, required);
		}

		public SchemaBuilder Integer(string name, string description, bool required = false)
		{
			return Add(name, new JObject { ["type"] = "integer", ["description"] = description }, required);
		}

		public SchemaBuilder Boolean(string name, string description, bool required = false)
		{
			return Add(name, new JObject { ["type"] = "boolean", ["description"] = description }, required);
		}

		public SchemaBuilder Enum(string name, string description, string[] values, bool required = false)
		{
			return Add(name, new JObject { ["type"] = "string", ["description"] = description, ["enum"] = new JArray(values) }, required);
		}

		public SchemaBuilder Add(string name, JObject schema, bool required = false)
		{
			_properties[name] = schema;
			if (required)
			{
				_required.Add(name);
			}

			return this;
		}

		public JObject Build()
		{
			JObject schema = new JObject
			{
				["type"] = "object",
				["properties"] = _properties,
			};

			if (_required.Count > 0)
			{
				schema["required"] = _required;
			}

			return schema;
		}
	}
}