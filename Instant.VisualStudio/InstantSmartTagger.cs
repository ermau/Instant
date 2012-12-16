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

		private void OnLayoutChanged (object sender, TextViewLayoutChangedEventArgs args)
		{
			ITextSnapshot snapshot = args.NewSnapshot;
			if (this.lastVersion == null)
				this.lastVersion = snapshot.Version;
			else if (this.lastVersion == snapshot.Version)
				return;

			// Clear old spans
			RemoveTagSpans (s => true);

			string code = snapshot.GetText();

			SyntaxTree tree = SyntaxTree.Parse (code);
			if (tree.Errors.Any (e => e.ErrorType == ErrorType.Error))
				return;

			foreach (MethodDeclaration method in tree.Descendants.OfType<MethodDeclaration>())
			{
				ITextSnapshotLine nameLine = snapshot.GetLineFromLineNumber (method.NameToken.StartLocation.Line - 1);
				ITextSnapshotLine startLine = snapshot.GetLineFromLineNumber (method.StartLocation.Line);
				ITextSnapshotLine endLine = snapshot.GetLineFromLineNumber (method.EndLocation.Line);

				int pos = nameLine.Start.Position + method.NameToken.StartLocation.Column - 1;

				ITrackingSpan nameSpan = snapshot.CreateTrackingSpan (pos, method.Name.Length, SpanTrackingMode.EdgeExclusive);
				ITrackingSpan methodSpan = snapshot.CreateTrackingSpan (Span.FromBounds (startLine.Start.Position, endLine.End.Position), SpanTrackingMode.EdgeExclusive);

				var actionSets = new ReadOnlyCollection<SmartTagActionSet> (new[]
				{
					new SmartTagActionSet (new ReadOnlyCollection<ISmartTagAction> (new[]
					{
						new InstantTagToggleAction (this.view, methodSpan, method.GetExampleInvocation()),
					})),
				});

				CreateTagSpan (nameSpan, new SmartTag (SmartTagType.Factoid, actionSets));
			}
		}
	}
}
