namespace DTech.Parley.Editor
{
	internal enum PermissionMode : byte
	{
		Default = 0,
		AcceptEdits = 1,
		Plan = 2,
		Auto = 3,
		BypassPermissions = 4,
		DontAsk = 5,
	}
}