//
// LineMap.cs
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
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.TypeSystem;
using Microsoft.VisualStudio.Text;

namespace Instant.VisualStudio
{
	internal class LineMap
	{
		private LineMap (Dictionary<int, ITrackingSpan> spans)
		{
			this.spans = spans;
		}

		/// <summary>
		/// Tries to get the current location of an operation.
		/// </summary>
		/// <param name="snapshot">The current text snapshot.</param>
		/// <param name="operationId">The operation ID.</param>
		/// <param name="span">The current location, if found.</param>
		/// <returns><c>true</c> if found, <c>false</c> otherwise.</returns>
		internal bool TryGetSpan (ITextSnapshot snapshot, int operationId, out SnapshotSpan span)
		{
			if (snapshot == null)
				throw new ArgumentNullException ("snapshot");

			span = default(SnapshotSpan);

			ITrackingSpan tracking;
			if (!this.spans.TryGetValue (operationId, out tracking))
				return false;

			span = tracking.GetSpan (snapshot);
			return true;
		}

		private readonly Dictionary<int, ITrackingSpan> spans;

		/// <summary>
		/// Asynchronously constructs a line map from a <paramref name="snapshot"/> and <paramref name="code"/>.
		/// </summary>
		/// <param name="snapshot">The current text snapshot.</param>
		/// <param name="code">The code to derive a line map from.</param>
		/// <param name="cancelToken">Cancellation token.</param>
		/// <returns>
		/// A <see cref="LineMap"/> if <paramref name="code"/> was parsed correctly,
		/// <c>null</c> if there was invalid code or it was canceled.
		/// </returns>
		internal static Task<LineMap> ConstructAsync (ITextSnapshot snapshot, string code, CancellationToken cancelToken)
		{
			if (snapshot == null)
				throw new ArgumentNullException ("snapshot");
			if (code == null)
				throw new ArgumentNullException ("code");

			return Task<LineMap>.Factory.StartNew (() =>
			{
				try
				{
					var tree = SyntaxTree.Parse (code, cancellationToken: cancelToken);
					if (tree.Errors.Any (p => p.ErrorType == ErrorType.Error))
						return null;

					var identifier = new IdentifyingVisitor();
					tree.AcceptVisitor (identifier);

					var spans = new Dictionary<int, ITrackingSpan> (identifier.LineMap.Count);
					foreach (var kvp in identifier.LineMap)
					{
						ITextSnapshotLine line = snapshot.GetLineFromLineNumber (kvp.Value - 1);
						ITrackingSpan span = snapshot.CreateTrackingSpan (line.Extent, SpanTrackingMode.EdgeExclusive);
						spans.Add (kvp.Key, span);
					}

					return (cancelToken.IsCancellationRequested) ? null : new LineMap (spans);
				}
				catch (OperationCanceledException)
				{
					return null;
				}
			}, cancelToken);
		}
	}
}