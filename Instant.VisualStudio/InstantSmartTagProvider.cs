//
// InstantSmartTagProvider.cs
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

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace Instant.VisualStudio
{
	[Export	(typeof (IViewTaggerProvider))]
	[ContentType ("CSharp")]
	[Order (Before = "default")]
	[TagType (typeof (SmartTag))]
	internal class InstantSmartTagProvider
		: IViewTaggerProvider
	{
		[Import (typeof(ITextStructureNavigatorSelectorService))]
		internal ITextStructureNavigatorSelectorService NavigatorService
		{
			get;
			set;
		}

		public ITagger<T> CreateTagger<T> (ITextView textView, ITextBuffer buffer)
			where T : ITag
		{
			if (textView == null || buffer == null)
				return null;

			if (buffer == textView.TextBuffer)
				return new InstantSmartTagger (buffer, textView, this) as ITagger<T>;
			else
				return null;
		}
	}
}
