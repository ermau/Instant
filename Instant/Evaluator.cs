using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
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
			new Thread (EvaluatorRunner).Start();
		}

		public void PushSubmission (Submission submission)
		{
			Interlocked.Exchange (ref this.nextSubmission, submission);
			this.submissionWait.Set();
		}

		public void Dispose()
		{
			this.running = false;
			this.submissionWait.Dispose();
		}

		private AppDomain domain;
		private int domainCount;
		private bool running;

		private Submission nextSubmission;
		private readonly AutoResetEvent submissionWait = new AutoResetEvent (false);

		private void EvaluatorRunner()
		{
			while (this.running)
			{
				this.submissionWait.WaitOne();

				Submission next = Interlocked.Exchange (ref this.nextSubmission, null);

				string path;
				AppDomain evalDomain = GetDomain (out path);

				string[] references = next.Project.References.ToArray();
				for (int i = 0; i < references.Length; i++)
				{
					string reference = Path.Combine (path, Path.GetFileName (references[i]));
					if (!File.Exists (reference))
						File.Copy (references[i], reference);

					references[i] = reference;
				}

				var cparams = new CompilerParameters();
				cparams.OutputAssembly = Path.Combine (path, Path.GetRandomFileName());
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
					return;

				//MethodInfo method = results.CompiledAssembly.GetType ("Instant.User.Evaluation").GetMethod ("Evaluate", BindingFlags.NonPublic | BindingFlags.Static);
				//method.Invoke (null, null);

				try
				{
					DomainEvaluator domainEvaluator = (DomainEvaluator)evalDomain.CreateInstanceAndUnwrap ("Instant", "Instant.Evaluator+DomainEvaluator");
					domainEvaluator.Evaluate (next, cparams, sources.ToArray());
				}
				catch (OperationCanceledException)
				{
				}

				OnEvaluationCompleted (new EvaluationCompletedEventArgs (next));
			}
		}

		private AppDomain GetDomain (out string path)
		{
			if (domain == null || domainCount++ == 25)
			{
				if (domain != null)
				{
					string dir = domain.BaseDirectory;
					AppDomain.Unload (domain);
					Directory.Delete (dir, true);
				}

				domainCount = 0;

				AppDomainSetup setup = new AppDomainSetup { ApplicationBase = GetInstantDir() };
				domain = AppDomain.CreateDomain ("Instant Evaluation", null, setup);
			}

			path = domain.BaseDirectory;
			return domain;
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