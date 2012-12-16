//
// InstantVisualStudioFactory.cs
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

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Instant.VisualStudio
{
	/// <summary>
	/// Establishes an <see cref="IAdornmentLayer"/> to place the adornment on and exports the <see cref="IWpfTextViewCreationListener"/>
	/// that instantiates the adornment on the event of a <see cref="IWpfTextView"/>'s creation
	/// </summary>
	[Export(typeof(IWpfTextViewConnectionListener))]
	[ContentType("CSharp")]
	[TextViewRole(PredefinedTextViewRoles.Document)]
	internal sealed class InstantVisualStudioFactory : IWpfTextViewConnectionListener
	{
		/// <summary>
		/// Defines the adornment layer for the adornment. This layer is ordered
		/// after the selection layer in the Z-order
		/// </summary>
		[Export(typeof(AdornmentLayerDefinition))]
		[Name("Instant.VisualStudio")]
		[Order(After = PredefinedAdornmentLayers.Text, Before = PredefinedAdornmentLayers.Caret)]
		public AdornmentLayerDefinition editorAdornmentLayer = null;

		public void SubjectBuffersConnected (IWpfTextView textView, ConnectionReason reason, Collection<ITextBuffer> subjectBuffers)
		{
			if (reason != ConnectionReason.TextViewLifetime)
				return;

			instants.Add (textView, new InstantVisualStudio (textView));
		}

		public void SubjectBuffersDisconnected (IWpfTextView textView, ConnectionReason reason, Collection<ITextBuffer> subjectBuffers)
		{
			if (reason != ConnectionReason.TextViewLifetime)
				return;

			InstantVisualStudio instant;
			if (this.instants.TryGetValue (textView, out instant))
				instant.Dispose();
		}

		private readonly Dictionary<IWpfTextView, InstantVisualStudio> instants = new Dictionary<IWpfTextView, InstantVisualStudio>();
	}
}
