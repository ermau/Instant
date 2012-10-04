//
// MainWindowViewModel.cs
//
// Copyright 2012 Eric Maupin
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Instant.Operations;
using Roslyn.Compilers.CSharp;

namespace Instant.Standalone
{
	public class MainWindowViewModel
		: INotifyPropertyChanged
	{

		public event PropertyChangedEventHandler PropertyChanged;

		public string Input
		{
			get { return this.input; }
			set
			{
				if (this.input == value)
					return;

				this.input = value;
				OnPropertyChanged ("Input");
				ProcessInput();
			}
		}

		public string Debug
		{
			get { return this.debug; }
			private set
			{
				if (this.debug == value)
					return;

				this.debug = value;
				OnPropertyChanged ("Debug");
			}
		}

		public string Output
		{
			get { return this.output; }
			private set
			{
				if (this.output == value)
					return;

				this.output = value;
				OnPropertyChanged ("Output");
			}
		}

		public string Status
		{
			get { return this.status; }
			private set
			{
				if (this.status == value)
					return;

				this.status = value;
				OnPropertyChanged ("Status");
			}
		}

		public bool DebugTree
		{
			get { return this.showDebugTree; }
			set
			{
				if (this.showDebugTree == value)
					return;

				this.showDebugTree = value;
				OnPropertyChanged ("DebugTree");

				if (value)
					ProcessInput();
			}
		}

		public bool ShowCompilerErrors
		{
			get { return this.showCompilerErrors; }
			set
			{
				if (this.showCompilerErrors == value)
					return;

				this.showCompilerErrors = value;
				OnPropertyChanged ("ShowCompilerErrors");

				if (value)
					ProcessInput();
			}
		}

		public int MaximumLoops
		{
			get { return this.maxLoops; }
			set
			{
				if (this.maxLoops == value)
					return;

				this.maxLoops = value;
				OnPropertyChanged ("MaximumLoops");
			}
		}

		public int ExecutionTimeout
		{
			get { return this.timeout; }
			set
			{
				if (this.timeout == value)
					return;

				this.timeout = value;
				OnPropertyChanged ("ExecutionTimeout");
			}
		}

		public MethodCall RootCall
		{
			get { return this.rootCall; }
			set
			{
				if (this.rootCall == value)
					return;

				this.rootCall = value;
				OnPropertyChanged ("RootCall");
			}
		}

		public double FontSize
		{
			get { return this.fontSize; }
			set
			{
				if (this.fontSize == value)
					return;

				this.fontSize = value;
				OnPropertyChanged ("FontSize");
			}
		}

		private int timeout = 5000, maxLoops = 100;
		private bool showDebugTree, showCompilerErrors;
		private string input, output, debug = "Initializing";

		private void OnPropertyChanged (string property)
		{
			var changed = this.PropertyChanged;
			if (changed != null)
				changed (this, new PropertyChangedEventArgs (property));
		}

		private CancellationTokenSource cancelSource;

		private string lastOutput = String.Empty;
		private MethodCall rootCall;
		private string status;
		private double fontSize = 16;

		private async void ProcessInput()
		{
			var newSource = new CancellationTokenSource();
			var source = Interlocked.Exchange (ref this.cancelSource, newSource);

			if (source != null)
			{
				source.Cancel();
				source.Dispose();
			}

			new Timer (o =>
			{
				var cancel = Interlocked.Exchange (ref this.cancelSource, null);
				if (cancel != null)
				{
					cancel.Cancel();
					cancel.Dispose();
				}
			}, null, this.ExecutionTimeout, Timeout.Infinite);

			var sink = new MemoryInstrumentationSink();
			Submission s = Hook.CreateSubmission (sink, this.cancelSource.Token);
			SyntaxNode instrumented = await Instantly.Instrument (input, s.SubmissionId);
			
			if (DebugTree)
				LogSyntaxTree (instrumented);

			try
			{
				await Instantly.Evaluate (instrumented, String.Empty);
				
				var methods = sink.GetRootCalls();
				if (methods == null || methods.Count == 0)
					return;

				RootCall = methods.Values.First();
				Status = null;
			}
			catch (Exception ex)
			{
				Status = ex.Message;
				Output = ex.ToString();
			}
		}

		private void LogSyntaxTree (SyntaxNode node)
		{
			var builder = new StringBuilder();
			LogSyntaxTree (node, builder, skipSelf: true);

			this.Debug = builder.ToString();
		}

		private void LogSyntaxTree (SyntaxNode node, StringBuilder builder, int ident = 0, bool skipSelf = false)
		{
			string sident = String.Empty;
			for (int i = 0; i < ident; ++i)
				sident += "\t";

			if (!skipSelf)
			{
				builder.AppendLine (sident + node.GetType() + ": " + node);
				ident++;
			}

			foreach (SyntaxNode childNode in node.ChildNodes())
				LogSyntaxTree (childNode, builder, ident);
		}
	}
}
