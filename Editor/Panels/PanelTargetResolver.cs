using System;
using System.Collections.Generic;
using Object = UnityEngine.Object;

namespace DTech.Parley.Editor.Panels
{
	internal sealed class PanelTargetResolver
	{
		public bool Writable { get; set; } = true;

		public bool SpecChanged { get; private set; }

		private readonly PanelSpec _spec;
		private readonly Dictionary<string, Object> _resolved = new (StringComparer.Ordinal);
		private readonly Dictionary<string, PanelTargetStatus> _status = new (StringComparer.Ordinal);
		private readonly Dictionary<string, int> _captured = new (StringComparer.Ordinal);

		public PanelTargetResolver(PanelSpec spec)
		{
			_spec = spec;
		}

		public void Invalidate()
		{
			_resolved.Clear();
			_status.Clear();
		}

		public Object Resolve(string alias)
		{
			if (string.IsNullOrEmpty(alias) || !_spec.Targets.TryGetValue(alias, out string reference) || string.IsNullOrWhiteSpace(reference))
			{
				if (!string.IsNullOrEmpty(alias))
				{
					_status[alias] = PanelTargetStatus.Missing;
				}

				return null;
			}

			if (_resolved.TryGetValue(alias, out Object cached) && cached != null)
			{
				return cached;
			}

			Object byReference = UnityObjectPaths.Resolve(reference);
			if (byReference != null)
			{
				Capture(alias, byReference);
				return Remember(alias, byReference, PanelTargetStatus.Resolved);
			}

			if (!_spec.Ids.TryGetValue(alias, out string id) || string.IsNullOrEmpty(id))
			{
				return Remember(alias, null, PanelTargetStatus.Missing);
			}

			Object byId = GlobalObjectIds.Resolve(id);
			if (byId == null)
			{
				return Remember(alias, null, PanelTargetStatus.MissingWithId);
			}

			Heal(alias, reference, byId);
			return Remember(alias, byId, PanelTargetStatus.Healed);
		}

		public Object Resolve(string alias, string component)
		{
			return PanelTargets.WithComponent(Resolve(alias), component);
		}

		public PanelTargetStatus Status(string alias)
		{
			return !string.IsNullOrEmpty(alias) && _status.TryGetValue(alias, out PanelTargetStatus status)
				? status
				: PanelTargetStatus.Unknown;
		}

		public void AcceptChanges()
		{
			SpecChanged = false;
		}

		private Object Remember(string alias, Object target, PanelTargetStatus status)
		{
			_resolved[alias] = target;
			_status[alias] = status;
			return target;
		}

		private void Capture(string alias, Object target)
		{
			if (!Writable)
			{
				return;
			}

			int instanceId = target.GetInstanceID();
			if (_captured.TryGetValue(alias, out int known) && known == instanceId)
			{
				return;
			}

			_captured[alias] = instanceId;
			if (!GlobalObjectIds.TryCapture(target, out string id))
			{
				return;
			}

			if (_spec.Ids.TryGetValue(alias, out string stored) && string.Equals(stored, id, StringComparison.Ordinal))
			{
				return;
			}

			_spec.Ids[alias] = id;
			SpecChanged = true;
		}

		private void Heal(string alias, string reference, Object target)
		{
			if (!Writable || !UnityObjectPaths.TryReferenceOf(target, out string repaired))
			{
				return;
			}

			if (string.Equals(repaired, reference, StringComparison.Ordinal))
			{
				return;
			}

			_spec.Targets[alias] = repaired;
			SpecChanged = true;
		}
	}
}
