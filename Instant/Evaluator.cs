//
// Evaluator.cs
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
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CSharp;

namespace Instant
{
	public class EvaluationCompletedEventArgs
		: EventArgs
	{
		public EvaluationCompletedEventArgs (Submission submission)
		{
			if (submission == null)
				throw new ArgumentNullException ("submission");

			Submission = submission;
		}

		public EvaluationCompletedEventArgs (Exception exception)
		{
			if (exception == null)
				throw new ArgumentNullException ("exception");

			Exception = exception;
		}

		public Exception Exception
		{
			get;
			private set;
		}

		public Submission Submission
		{
			get;
			private set;
		}
	}

	public sealed class Evaluator
		: IDisposable
	{
		public event EventHandler<EvaluationCompletedEventArgs> EvaluationCompleted;

		public void Start()
		{
			this.running = true;
			new Thread (EvaluatorRunner)
			{
				Name = "Evaluator"
			}.Start();
		}

		public void PushSubmission (Submission submission)
		{
			Submission bumped = Interlocked.Exchange (ref this.nextSubmission, submission);
			if (bumped != null)
				bumped.Cancel();

			this.submissionWait.Set();
		}

		public void Dispose()
		{
			this.running = false;
			this.submissionWait.Dispose();
		}

		private bool running;

		private Submission nextSubmission;
		private readonly AutoResetEvent submissionWait = new AutoResetEvent (false);

		private readonly string RefPath = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.ProgramFiles), "Reference Assemblies");

		private void EvaluatorRunner()
		{
			while (this.running)
			{
				this.submissionWait.WaitOne();

				Submission next = Interlocked.Exchange (ref this.nextSubmission, null);
				if (next == null)
					continue;

				AppDomain evalDomain = null;
				try
				{
					AppDomainSetup setup = new AppDomainSetup
					{
						ApplicationBase = GetInstantDir(),
						LoaderOptimization = LoaderOptimization.MultiDomainHost
					};
					evalDomain = AppDomain.CreateDomain ("Instant Evaluation", null, setup);

					bool error = false;

					string[] references = next.Project.References.ToArray();
					for (int i = 0; i < references.Length; i++)
					{
						// HACK: We don't need to copy reference assemblies.
						if (references[i].StartsWith (RefPath))
							continue;

						string reference = Path.Combine (setup.ApplicationBase, Path.GetFileName (references[i]));

						if (!File.Exists (references[i]))
						{
							error = true;
							break;
						}

						File.Copy (references[i], reference);

						references[i] = reference;
					}

					if (error)
						continue;

					var cparams = new CompilerParameters();
					cparams.OutputAssembly = Path.Combine (setup.ApplicationBase, Path.GetRandomFileName());
					cparams.GenerateInMemory = false;
					cparams.IncludeDebugInformation = false;
					cparams.ReferencedAssemblies.AddRange (references);
					cparams.ReferencedAssemblies.Add (typeof (Instantly).Assembly.Location);

					// HACK: Wrap test code into a proper method
					string evalSource = "namespace Instant.User { static class Evaluation { static void Evaluate() {" + next.EvalCode + " } } }";
 
					List<string> sources = next.Project.Sources.AsParallel().Select (
							e => e.Fold (async f => await f.OpenText().ReadToEndAsync(), Task.FromResult)
						).ToListAsync().Result;

					sources.Add (evalSource);

					CSharpCodeProvider provider = new CSharpCodeProvider();
					CompilerResults results = provider.CompileAssemblyFromSource (cparams, sources.ToArray());
					if (results.Errors.HasErrors)
						continue;

					DomainEvaluator domainEvaluator = (DomainEvaluator)evalDomain.CreateInstanceAndUnwrap ("Instant", "Instant.Evaluator+DomainEvaluator");
					domainEvaluator.Evaluate (next, cparams, sources.ToArray());

					OnEvaluationCompleted (new EvaluationCompletedEventArgs (next));
				}
				catch (Exception ex)
				{
					OnEvaluationCompleted (new EvaluationCompletedEventArgs (ex));
				}
				finally
				{
					if (evalDomain != null)
					{
						string dir = evalDomain.BaseDirectory;
						AppDomain.Unload (evalDomain);
						Directory.Delete (dir, true);
					}
				}
			}
		}

		private class DomainEvaluator
			: MarshalByRefObject
		{
			public void Evaluate (Submission submission, CompilerParameters cparams, string[] sources)
			{
				CSharpCodeProvider provider = new CSharpCodeProvider();
				CompilerResults results = provider.CompileAssemblyFromSource (cparams, sources);
				if (results.Errors.HasErrors)
					return;

				MethodInfo method = results.CompiledAssembly.GetType ("Instant.User.Evaluation").GetMethod ("Evaluate", BindingFlags.NonPublic | BindingFlags.Static);
				if (method == null)
					return;

				Hook.LoadSubmission (submission);

				try
				{
					method.Invoke (null, null);
				}
				catch (OperationCanceledException)
				{
				}
			}
		}

		private static string GetInstantDir()
		{
			string temp = Path.GetTempPath();
			string path = Path.Combine (temp, Path.GetRandomFileName());

			bool created = false;
			while (!created)
			{
				try
				{
					Directory.CreateDirectory (path);
					created = true;
				}
				catch (IOException)
				{
					path = Path.Combine (temp, Path.GetRandomFileName());
				}
			}

			File.Copy (typeof (Instantly).Assembly.Location, Path.Combine (path, "Instant.dll"));

			return path;
		}

		private void OnEvaluationCompleted (EvaluationCompletedEventArgs e)
		{
			var handler = this.EvaluationCompleted;
			if (handler != null)
				handler (this, e);
		}
	}
}