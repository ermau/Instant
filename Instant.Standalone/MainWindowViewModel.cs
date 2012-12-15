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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cadenza;
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

		public bool IdentTree
		{
			get { return this.showIdentTree; }
			set
			{
				if (this.showIdentTree == value)
					return;

				this.showIdentTree = value;
				OnPropertyChanged ("IdentTree");

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

		public string TestCode
		{
			get { return this.testCode; }
			set
			{
				if (this.testCode == value)
					return;

				this.testCode = value;
				OnPropertyChanged ("TestCode");
			}
		}

		private bool showDebugTree, showCompilerErrors, showIdentTree;
		private string input, output, debug = "Initializing";

		private void OnPropertyChanged (string property)
		{
			var changed = this.PropertyChanged;
			if (changed != null)
				changed (this, new PropertyChangedEventArgs (property));
		}

		private string lastOutput = String.Empty;
		private MethodCall rootCall;
		private string status;
		private double fontSize = 16;
		private string testCode;

		private Submission submission;

		private async void ProcessInput()
		{
			var source = Interlocked.Exchange (ref this.submission, null);

			if (source != null)
				source.Cancel();

			if (String.IsNullOrEmpty (input) || String.IsNullOrEmpty (TestCode))
				return;

			Submission s = null;
			var sink = new MemoryInstrumentationSink (() => s.IsCanceled);
			s = Hook.CreateSubmission (sink);
			string instrumented = await Instantly.Instrument (input, s);

			if (DebugTree)
				Debug = instrumented;
			//	LogSyntaxTree (instrumented);

			Project project = new Project();
			project.Sources.Add (Either<FileInfo, string>.B (instrumented));

			try
			{
				await Instantly.Evaluate (s, project, TestCode);
				
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
