//
// InstantSmartTagger.cs
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
using System.Collections.ObjectModel;
using System.Linq;
using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.TypeSystem;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;

namespace Instant.VisualStudio
{
	internal class InstantSmartTagger
		: SimpleTagger<SmartTag>
	{
		public InstantSmartTagger (ITextBuffer buffer, ITextView view, InstantSmartTagProvider provider)
			: base (buffer)
		{
			if (buffer == null)
				throw new ArgumentNullException ("buffer");
			if (view == null)
				throw new ArgumentNullException ("view");
			if (provider == null)
				throw new ArgumentNullException ("provider");

			this.buffer = buffer;
			this.view = view;
			this.view.LayoutChanged += OnLayoutChanged;
			this.provider = provider;
		}

		private readonly ITextBuffer buffer;
		private readonly ITextView view;
		private readonly InstantSmartTagProvider provider;

		private ITextVersion lastVersion;

		private readonly Dictionary<ITrackingSpan, TrackingTagSpan<SmartTag>> spans = new Dictionary<ITrackingSpan, TrackingTagSpan<SmartTag>>();

		private void OnLayoutChanged (object sender, TextViewLayoutChangedEventArgs args)
		{
			ITextSnapshot snapshot = args.NewSnapshot;
			if (this.lastVersion == snapshot.Version)
				return;

			this.lastVersion = snapshot.Version;
			string code = snapshot.GetText();

			SyntaxTree tree = SyntaxTree.Parse (code);
			if (tree.Errors.Any (e => e.ErrorType == ErrorType.Error))
				return;

			List<Tuple<ITrackingSpan, SnapshotSpan>> currentSpans = new List<Tuple<ITrackingSpan, SnapshotSpan>> (
				this.spans.Keys.Select (t => new Tuple<ITrackingSpan, SnapshotSpan> (t, t.GetSpan (args.NewSnapshot))));

			foreach (MethodDeclaration method in tree.Descendants.OfType<MethodDeclaration>())
			{
				if (method.HasModifier (Modifiers.Abstract) || method.HasModifier (Modifiers.Extern))
					continue;

				ITextSnapshotLine nameLine = snapshot.GetLineFromLineNumber (method.NameToken.StartLocation.Line - 1);
				int pos = nameLine.Start.Position + method.NameToken.StartLocation.Column - 1;

				SnapshotSpan nameSpan = new SnapshotSpan (snapshot, pos, method.Name.Length);
				var overlapped = currentSpans.FirstOrDefault (ss => ss.Item2.OverlapsWith (nameSpan));

				// If the method is already being tracked, VS will handle it.
				if (overlapped != null)
				{
					// Update example invocation, signature might have changed
					InstantTagToggleAction action = this.spans[overlapped.Item1].Tag.ActionSets.SelectMany (s => s.Actions).OfType<InstantTagToggleAction>().Single();
					action.ExampleCode = method.GetExampleInvocation();

					currentSpans.Remove (overlapped);
					continue;
				}

				ITrackingSpan trackingNameSpan = snapshot.CreateTrackingSpan (nameSpan, SpanTrackingMode.EdgeExclusive);
				
				ITextSnapshotLine startLine = snapshot.GetLineFromLineNumber (method.StartLocation.Line);
				ITextSnapshotLine endLine = snapshot.GetLineFromLineNumber (method.EndLocation.Line);
				ITrackingSpan methodSpan = snapshot.CreateTrackingSpan (Span.FromBounds (startLine.Start.Position, endLine.End.Position), SpanTrackingMode.EdgeExclusive);

				var actionSets = new ReadOnlyCollection<SmartTagActionSet> (new[]
				{
					new SmartTagActionSet (new ReadOnlyCollection<ISmartTagAction> (new[]
					{
						new InstantTagToggleAction (this.view, methodSpan, method.GetExampleInvocation()),
					})),
				});

				var tag = new SmartTag (SmartTagType.Factoid, actionSets);
				TrackingTagSpan<SmartTag> trackingTagSpan = CreateTagSpan (trackingNameSpan, tag);
				this.spans[trackingNameSpan] = trackingTagSpan;
			}

			// Remove any empty spans, they've been deleted
			foreach (var kvp in this.spans.Where (kvp => kvp.Key.GetSpan (snapshot).IsEmpty).ToArray())
			{
				foreach (IDisposable disposable in kvp.Value.Tag.ActionSets.SelectMany (s => s.Actions).OfType<IDisposable>())
					disposable.Dispose();

				RemoveTagSpan (kvp.Value);
				this.spans.Remove (kvp.Key);
			}
		}
	}
}