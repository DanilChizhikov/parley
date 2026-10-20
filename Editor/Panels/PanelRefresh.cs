namespace DTech.Parley.Editor.Panels
{
	internal sealed class PanelRefresh
	{
		public PanelCoverage Coverage { get; private set; } = PanelCoverage.Empty;

		private bool _assetsDirty;
		private bool _hierarchyDirty;
		private PanelFingerprint _fingerprint;

		public void MarkAssets()
		{
			_assetsDirty = true;
		}

		public void MarkHierarchy()
		{
			_hierarchyDirty = true;
		}

		public PanelRefreshResult Poll(PanelSpec spec, PanelTargetResolver targets, bool busy)
		{
			if (!_assetsDirty && !_hierarchyDirty || busy)
			{
				return PanelRefreshResult.Idle;
			}

			if (spec == null || targets == null)
			{
				_assetsDirty = false;
				_hierarchyDirty = false;
				return PanelRefreshResult.Idle;
			}

			bool assetsChanged = _assetsDirty;
			_assetsDirty = false;
			_hierarchyDirty = false;
			targets.Invalidate();
			PanelCoverage coverage = assetsChanged ? PanelCoverage.Of(spec, targets) : Coverage;
			PanelFingerprint next = new PanelFingerprint(
				PanelFingerprint.HashTargets(spec, targets),
				assetsChanged ? PanelFingerprint.HashQueries(spec.Queries) : _fingerprint.Queries,
				PanelFingerprint.HashCoverage(coverage));
			Coverage = coverage;
			if (!next.Matches(_fingerprint))
			{
				_fingerprint = next;
				return PanelRefreshResult.Rebuild;
			}

			return targets.SpecChanged ? PanelRefreshResult.Persist : PanelRefreshResult.Idle;
		}

		public void Capture(PanelSpec spec, PanelTargetResolver targets)
		{
			Coverage = PanelCoverage.Of(spec, targets);
			_fingerprint = new PanelFingerprint(
				PanelFingerprint.HashTargets(spec, targets),
				PanelFingerprint.HashQueries(spec?.Queries),
				PanelFingerprint.HashCoverage(Coverage));
		}
	}
}
