#if PARLEY_DEBUGG
namespace DTech.Parley.Editor.Agents.Preview
{
	internal enum PreviewScenario : byte
	{
		Showcase = 0,
		Markdown = 1,
		Thinking = 2,
		Tools = 3,
		Edits = 4,
		Subagent = 5,
		Permission = 6,
		Question = 7,
		Plan = 8,
		EnterPlan = 9,
		TodosAndTasks = 10,
		Notices = 11,
		SignIn = 12,
		Crash = 13,
		LongTurn = 14,
		ContextFull = 15,
		RestartHint = 16,
	}
}
#endif
