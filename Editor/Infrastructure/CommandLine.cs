using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DTech.Parley.Editor
{
	internal static class CommandLine
	{
		public static bool IsWindows => Application.platform == RuntimePlatform.WindowsEditor;

		public static string Join(IEnumerable<string> arguments) => Join(arguments, IsWindows);

		public static string Join(IEnumerable<string> arguments, bool windows)
		{
			StringBuilder builder = new StringBuilder();
			foreach (string argument in arguments)
			{
				if (builder.Length > 0)
				{
					builder.Append(' ');
				}

				builder.Append(windows ? QuoteWindows(argument) : QuoteUnix(argument));
			}

			return builder.ToString();
		}

		public static IEnumerable<string> Split(string text)
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				yield break;
			}

			StringBuilder current = new StringBuilder();
			bool quoted = false;
			foreach (char character in text)
			{
				if (character == '"')
				{
					quoted = !quoted;
					continue;
				}

				if (char.IsWhiteSpace(character) && !quoted)
				{
					if (current.Length > 0)
					{
						yield return current.ToString();
						current.Clear();
					}

					continue;
				}

				current.Append(character);
			}

			if (current.Length > 0)
			{
				yield return current.ToString();
			}
		}

		public static string QuoteWindows(string argument)
		{
			if (string.IsNullOrEmpty(argument))
			{
				return "\"\"";
			}

			if (argument.IndexOfAny(new[] { ' ', '\t', '\n', '\v', '"' }) < 0)
			{
				return argument;
			}

			StringBuilder builder = new StringBuilder(argument.Length + 2);
			builder.Append('"');
			int backslashes = 0;
			foreach (char character in argument)
			{
				if (character == '\\')
				{
					backslashes++;
					continue;
				}

				if (character == '"')
				{
					builder.Append('\\', backslashes * 2 + 1);
					builder.Append('"');
				}
				else
				{
					builder.Append('\\', backslashes);
					builder.Append(character);
				}

				backslashes = 0;
			}

			builder.Append('\\', backslashes * 2);
			builder.Append('"');
			return builder.ToString();
		}

		public static string QuoteUnix(string argument)
		{
			if (string.IsNullOrEmpty(argument))
			{
				return "\"\"";
			}

			bool plain = true;
			foreach (char character in argument)
			{
				if (!IsPlainUnix(character))
				{
					plain = false;
					break;
				}
			}

			if (plain)
			{
				return argument;
			}

			StringBuilder builder = new StringBuilder(argument.Length + 2);
			builder.Append('"');
			foreach (char character in argument)
			{
				if (character == '\\' || character == '"' || character == '$' || character == '`')
				{
					builder.Append('\\');
				}

				builder.Append(character);
			}

			builder.Append('"');
			return builder.ToString();
		}

		private static bool IsPlainUnix(char character)
		{
			return char.IsLetterOrDigit(character)
				|| character == '-' || character == '_' || character == '.' || character == '/'
				|| character == '=' || character == ':' || character == ',' || character == '@'
				|| character == '%' || character == '+';
		}
	}
}