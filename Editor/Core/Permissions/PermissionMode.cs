using UnityEngine;

namespace DTech.Parley.Editor
{
	internal enum PermissionMode : byte
	{
		[InspectorName("Ask before edits")]
		Default = 0,
		[InspectorName("Accept edits")]
		AcceptEdits = 1,
		[InspectorName("Plan")]
		Plan = 2,
		[InspectorName("Auto")]
		Auto = 3,
		[InspectorName("Bypass permissions")]
		BypassPermissions = 4,
		[InspectorName("Don't ask")]
		DontAsk = 5,
	}
}