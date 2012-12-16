//
// ViewCache.cs
//
// Copyright 2012 Eric Maupin
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at

//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Instant.VisualStudio
{
	internal class OperationVisuals
	{
		public Type ViewType;
		public Func<InstantView> CreateView;
		public Type ViewModelType;
		public Func<OperationViewModel> CreateViewModel;

		public static OperationVisuals Create<TView,TViewModel> (Func<TView> createView, Func<TViewModel> createModel)
			where TView : InstantView
			where TViewModel : OperationViewModel
		{
			OperationVisuals visuals = new OperationVisuals();
			visuals.CreateView = createView;
			visuals.ViewType = typeof (TView);
			visuals.CreateViewModel = createModel;
			visuals.ViewModelType = typeof (TViewModel);

			return visuals;
		}
	}

	internal class ViewCache
	{
		public ViewCache (OperationVisuals visuals)
		{
			if (visuals == null)
				throw new ArgumentNullException ("visuals");

			this.visuals = visuals;
		}

		public bool TryGetView (out InstantView view)
		{
			if (this.views.Count > this.index)
			{
				view = this.views[this.index++];
				return true;
			}

			view = this.visuals.CreateView();
			this.views.Add (view);
			this.index++;

			return false;
		}

		public InstantView GetView (int opId)
		{
			return this.views.FirstOrDefault (v => v.Tag is int && (int)v.Tag == opId);
		}

		public InstantView[] ClearViews()
		{
			InstantView[] cleared;

			if (this.index >= this.views.Count)
				cleared = new InstantView[0];
			else
			{
				cleared = new InstantView[this.views.Count - this.index];
				for (int i = 0; i < cleared.Length; i++)
				{
					int x = this.views.Count - 1;
					cleared[i] = this.views[x];
					this.views.RemoveAt (x);
				}
			}

			this.index = 0;
			return cleared;
		}

		public void Remove (InstantView view)
		{
			this.views.Remove (view);
		}

		private readonly OperationVisuals visuals;
		private int index;
		private readonly List<InstantView> views = new List<InstantView>();
	}
}
