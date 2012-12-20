//
// InstantTagToggle.cs
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
using System.Windows;
using System.Windows.Media;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Instant.VisualStudio
{
	public class InstantToggleEventArgs
		: EventArgs
	{
		public InstantToggleEventArgs (ITextView view, ITrackingSpan methodSpan)
		{
			if (view == null)
				throw new ArgumentNullException ("view");
			if (methodSpan == null)
				throw new ArgumentNullException ("methodSpan");

			View = view;
			MethodSpan = methodSpan;
			IsRunning = false;
		}

		public InstantToggleEventArgs (ITextView view, ITrackingSpan methodSpan, string testCode)
		{
			if (view == null)
				throw new ArgumentNullException ("view");
			if (methodSpan == null)
				throw new ArgumentNullException ("methodSpan");
			if (testCode == null)
				throw new ArgumentNullException ("testCode");

			View = view;
			MethodSpan = methodSpan;
			TestCode = testCode;
			IsRunning = true;
		}

		public bool IsRunning
		{
			get;
			private set;
		}

		public ITextView View
		{
			get;
			private set;
		}

		public ITrackingSpan MethodSpan
		{
			get;
			private set;
		}

		public string TestCode
		{
			get;
			private set;
		}
	}

	internal sealed class InstantTagToggleAction
		: ISmartTagAction, IDisposable
	{
		public static event EventHandler<InstantToggleEventArgs> Toggled;

		public InstantTagToggleAction (ITextView view, ITrackingSpan methodSpan, string exampleCode)
		{
			if (view == null)
				throw new ArgumentNullException ("view");
			if (exampleCode == null)
				throw new ArgumentNullException ("exampleCode");
			if (methodSpan == null)
				throw new ArgumentNullException ("methodSpan");

			View = view;
			ExampleCode = exampleCode;
			MethodSpan = methodSpan;
			IsEnabled = !isRunning;

			Toggled += OnGlobalToggled;
		}

		public ITextView View
		{
			get;
			private set;
		}

		public string ExampleCode
		{
			get;
			private set;
		}

		public ITrackingSpan MethodSpan
		{
			get;
			private set;
		}

		public void Invoke()
		{
			if (!IsRunning)
			{
				var window = new TestCodeWindow();
				window.Owner = Application.Current.MainWindow;
				string testCode = window.ShowForTestCode (ExampleCode);
				if (testCode == null)
					return;

				IsRunning = true;
				OnToggled (new InstantToggleEventArgs (View, MethodSpan, testCode));
			}
			else
			{
				IsRunning = false;
				OnToggled (new InstantToggleEventArgs (View, MethodSpan));
			}
		}

		public ReadOnlyCollection<SmartTagActionSet> ActionSets
		{
			get;
			private set;
		}

		public ImageSource Icon
		{
			get;
			private set;
		}

		public string DisplayText
		{
			get { return (IsRunning) ? "Stop Evaluating" : "Evaluate"; }
		}

		public bool IsEnabled
		{
			get;
			private set;
		}

		public bool IsRunning
		{
			get;
			private set;
		}

		private static bool isRunning = false;
		private void OnToggled (InstantToggleEventArgs args)
		{
			isRunning = args.IsRunning;

			var handler = Toggled;
			if (handler != null)
				handler (this, args);
		}

		public void Dispose()
		{
			Toggled -= OnGlobalToggled;
		}

		private void OnGlobalToggled (object sender, InstantToggleEventArgs args)
		{
			if (args.View != View || sender == this)
				return;

			IsEnabled = !args.IsRunning;
		}
	}
}