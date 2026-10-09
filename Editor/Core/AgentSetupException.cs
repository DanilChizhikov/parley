using System;

namespace DTech.Parley.Editor
{
	internal sealed class AgentSetupException : InvalidOperationException
	{
		public AgentSetupException(string message) : base(message)
		{
		}
	}
}
