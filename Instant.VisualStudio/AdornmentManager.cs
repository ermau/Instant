//
// AdornmentManager.cs
//
// Copyright 2013 Eric Maupin
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
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Instant.VisualStudio
{
	class AdornmentManager
	{
		private readonly IWpfTextView view;
		private readonly IAdornmentLayer layer;
		private readonly Dictionary<ITrackingSpan, Tuple<FrameworkElement, bool>> views = new Dictionary<ITrackingSpan, Tuple<FrameworkElement, bool>>();

		public AdornmentManager (IWpfTextView view)
		{
			if (view == null)
				throw new ArgumentNullException ("view");

			this.view = view;
			this.layer = view.GetAdornmentLayer ("Instant.VisualStudio");
		}

		public void AddAdorner (ITrackingSpan span, FrameworkElement element)
		{
			if (span == null)
				throw new ArgumentNullException ("span");
			if (element == null)
				throw new ArgumentNullException ("element");

			ITextSnapshot snapshot = this.view.TextSnapshot;
			SnapshotSpan snapSpan = span.GetSpan (snapshot);

			Geometry g = this.view.TextViewLines.GetLineMarkerGeometry (snapSpan);
			this.views.Add (span, new Tuple<FrameworkElement, bool> (element, g != null));
			if (g == null)
				return;

			SetLocation (g, element);
			this.layer.AddAdornment (AdornmentPositioningBehavior.TextRelative, snapSpan, span, element, OnAdornerRemoved);
		}

		private void OnAdornerRemoved (object tag, UIElement element)
		{
			this.views[(ITrackingSpan)tag] = new Tuple<FrameworkElement, bool> ((FrameworkElement)element, false);
		}

		public void Clear()
		{
			foreach (Tuple<FrameworkElement, bool> adorner in this.views.Values)
				this.layer.RemoveAdornment (adorner.Item1);

			this.views.Clear();
		}

		public void Update (IEnumerable<SnapshotSpan> newOrUpdatedSpans)
		{
			if (newOrUpdatedSpans == null)
				throw new ArgumentNullException ("newOrUpdatedSpans");

			SnapshotSpan[] updated = newOrUpdatedSpans.ToArray();

			ITextSnapshot snapshot = this.view.TextSnapshot;

			foreach (var kvp in this.views) {
				SnapshotSpan span = kvp.Key.GetSpan (snapshot);
				if (!updated.Any (s => s.OverlapsWith (span)))
					continue;

				Geometry g = this.view.TextViewLines.GetMarkerGeometry (span);
				if (g == null)
					continue;

				Tuple<FrameworkElement, bool> adorner = kvp.Value;
				if (!adorner.Item2)
					this.layer.AddAdornment (AdornmentPositioningBehavior.TextRelative, span, kvp.Key, adorner.Item1, OnAdornerRemoved);

				SetLocation (g, adorner.Item1);
			}
		}

		void SetLocation (Geometry geometry, FrameworkElement element)
		{
			Canvas.SetLeft (element, geometry.Bounds.Right + 10);
			Canvas.SetTop (element, geometry.Bounds.Top + 1);
		}
	}
}