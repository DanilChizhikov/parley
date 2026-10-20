using System;

namespace DTech.Parley.Editor.Panels
{
	internal readonly struct PanelBuildRequest
	{
		public PanelSpec Spec { get; }
		public PanelTargetResolver Targets { get; }
		public PanelViewState View { get; }
		public Action Rebuild { get; }

		public PanelBuildRequest(PanelSpec spec, PanelTargetResolver targets, PanelViewState view, Action rebuild)
		{
			Spec = spec;
			Targets = targets;
			View = view;
			Rebuild = rebuild;
		}
	}
}
