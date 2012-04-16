//
// MainWindowViewModel.cs
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
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Roslyn.Compilers;
using Roslyn.Compilers.CSharp;
using Roslyn.Scripting;
using Roslyn.Scripting.CSharp;

namespace LiveCSharp
{
	public class MainWindowViewModel
		: INotifyPropertyChanged
	{
		public MainWindowViewModel()
		{
			this.init = Task.Factory.StartNew (Initialize)
				.ContinueWith (t => Debug = String.Empty);
		}

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

		private int timeout = 5000;
		private bool showDebugTree, showCompilerErrors;
		private string input, output, debug = "Initializing";

		private void OnPropertyChanged (string property)
		{
			var changed = PropertyChanged;
			if (changed != null)
				changed (this, new PropertyChangedEventArgs (property));
		}

		private CommonScriptEngine scripting;
		private Task init;

		private void Initialize()
		{
			this.scripting = new ScriptEngine (
				new[]
				{
					typeof (string).Assembly, // mscorlib
					typeof (System.Diagnostics.Stopwatch).Assembly, // System.dll
					typeof (Enumerable).Assembly, // System.Core.dll
					typeof (LoggingRewriter).Assembly, // this
				},

				new[]
				{
					"System",
					"System.Collections.Generic",
					"System.Diagnostics",
					"System.Linq"
				});

			SyntaxTree.ParseCompilationUnit (String.Empty);
		}

		private CancellationTokenSource cancelSource;

		private string lastOutput = String.Empty;
		private void ProcessInput()
		{
			if (this.init != null)
			{
				this.init.Wait();
				this.init = null;
			}

			var newSource = new CancellationTokenSource();
			var source = Interlocked.Exchange (ref this.cancelSource, newSource);

			if (source != null)
			{
				source.Cancel();
				source.Dispose();
			}

			StringObjectLogger logger = new StringObjectLogger (this.cancelSource.Token);

			new Timer (o =>
			{
				var cancel = Interlocked.Exchange (ref this.cancelSource, null);
				if (cancel != null)
				{
					cancel.Cancel();
					cancel.Dispose();
				}
			}, null, ExecutionTimeout, Timeout.Infinite);

			Task.Factory.StartNew (s =>
			{
				string code = (string) s;
				if (code == null)
					return;

				SyntaxNode root = Syntax.ParseCompilationUnit (code);

				var logRewriter = new LoggingRewriter();
				root = logRewriter.Visit (root);
				
				if (DebugTree)
					LogSyntaxTree (root);

				try
				{
					Session session = Session.Create (logger);
			
					this.scripting.Execute (root.ToString(), session);
				}
				catch (CompilationErrorException cex)
				{
					if (ShowCompilerErrors)
						Output = this.lastOutput + Environment.NewLine + cex.ToString();

					return;
				}
				catch (OutOfMemoryException)
				{
					return;
				}
				catch (Exception ex)
				{
					this.lastOutput = logger.Output;
					Output = logger.Output + Environment.NewLine + ex.ToString();
					return;
				}

				string o = logger.Output;
				if (!String.IsNullOrWhiteSpace (o))
				{
					Output = o;
					this.lastOutput = o;
				}

			}, Input, this.cancelSource.Token)
			.ContinueWith(t =>
			{
				if (t.IsFaulted)
					t.Exception.ToString();
			});
		}

		private void LogSyntaxTree (SyntaxNode node)
		{
			var builder = new StringBuilder();
			LogSyntaxTree (node, builder, skipSelf: true);

			Debug = builder.ToString();
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
