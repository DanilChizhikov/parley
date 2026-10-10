using System;
using System.Threading;
using System.Threading.Tasks;

namespace DTech.Parley.Editor
{
	internal interface IAgentBackend : IDisposable
	{
		bool IsRunning { get; }
		bool IsBusy { get; }
		string SessionId { get; }
		PermissionMode Mode { get; }
		bool HasPendingRestart { get; }

		Task StartAsync(CancellationToken token);
		Task SendAsync(UserTurn turn, CancellationToken token);
		void Interrupt();
		void SetPermissionMode(PermissionMode mode);
		void SetModel(string model);
		void SetEffort(string effort);
		void Respond(PendingRequest request, Decision decision);
		void StopTask(string taskId);
		void SetMcpConfiguration(McpConfiguration configuration);
		void RefreshMcpStatus();
		void SetSkillConfiguration(SkillConfiguration configuration);
		void RefreshSkills();
	}
}