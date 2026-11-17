using System;
using System.Collections.Generic;
using DTech.Parley.Editor.Sessions;

namespace DTech.Parley.Editor.Orchestration
{
    internal enum RunStatus { Running, Completed, Cancelled, Failed, Interrupted }
    internal enum StepStatus { Running, WaitingChildren, Completed, Cancelled, Failed, Interrupted }
    internal enum DelegationStatus
    {
        Queued, WaitingApproval, Preparing, Running, WaitingResultApproval, Ready, Delivered,
        Cancelled, Failed, Interrupted
    }

    internal sealed class OrchestrationRun
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string RootSessionId { get; set; }
        public RunStatus Status { get; set; }
        public int MaxHops { get; set; } = 4;
        public List<OrchestrationStep> Steps { get; set; } = new();
        public List<DelegationRecord> Delegations { get; set; } = new();
    }

    internal sealed class OrchestrationStep
    {
        public string TurnId { get; set; } = Guid.NewGuid().ToString("N");
        public string SessionId { get; set; }
        public string ParentDelegationId { get; set; }
        public int Depth { get; set; }
        public StepStatus Status { get; set; }
        public bool TurnFinished { get; set; }
        public string Output { get; set; }
        public bool IsError { get; set; }
        public string Error { get; set; }
    }

    internal sealed class DelegationRecord
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string RequestId { get; set; }
        public string ParentTurnId { get; set; }
        public string ChildSessionId { get; set; }
        public string TargetSkill { get; set; }
        public string TaskSummary { get; set; }
        public string ContextMode { get; set; }
        public ContextSnapshot Context { get; set; }
        public DelegationStatus Status { get; set; }
        public bool RequireApproval { get; set; }
        public PermissionMode PermissionCeiling { get; set; }
        public string Result { get; set; }
        public bool IsError { get; set; }
        public string Error { get; set; }
        public string PartialOutput { get; set; }
    }
}
