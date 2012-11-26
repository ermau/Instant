//
// Instantly.cs
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
using Cadenza;
using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.TypeSystem;
using Instant.Operations;
using Microsoft.CSharp;

namespace Instant
{
	public static class Instantly
	{
		/// <summary>
		/// Instruments the supplied code to call the <see cref="Hook"/> methods.
		/// </summary>
		/// <param name="code">The code to instrument.</param>
		/// <param name="submission">The submission retrieved from <see cref="Hook.CreateSubmission"/>.</param>
		/// <returns>A task for a <see cref="string"/> representing the instrumented code.</returns>
		/// <seealso cref="Hook.CreateSubmission"/>
		public static Task<string> Instrument (string code, Submission submission)
		{
			if (code == null)
				throw new ArgumentNullException ("code");

			return Task<string>.Factory.StartNew (s =>
			{
				SyntaxTree tree = SyntaxTree.Parse ((string)s, cancellationToken: submission.CancelToken);

				InstrumentingRewriter rewriter = new InstrumentingRewriter (submission);
				tree.AcceptVisitor (rewriter);

				if (tree.Errors.Any (e => e.ErrorType == ErrorType.Error))
					return null;

				return tree.GetText();
			}, code);
		}

		/// <summary>
		/// Evaluates pre-instrumented code.
		/// </summary>
		/// <param name="project">The representation of the current project.</param>
		/// <param name="evalSource">The source code to execute as the test.</param>
		/// <returns>A task for a dictionary of managed thread IDs to <see cref="MethodCall"/>s.</returns>
		/// <seealso cref="Instrument(string,Instant.Submission)"/>
		public static Task Evaluate (IProject project, string evalSource)
		{
			if (project == null)
				throw new ArgumentNullException ("project");
			if (evalSource == null)
				throw new ArgumentNullException ("evalSource");

			return Task.Factory.StartNew (() =>
			{
				string path = GetInstantDir();
				string[] references = project.References.ToArray();
				foreach (string reference in references)
					File.Copy (reference, Path.Combine (path, Path.GetFileName (reference)));

				var cparams = new CompilerParameters();
				cparams.OutputAssembly = Path.Combine (path, Path.GetRandomFileName());
				cparams.GenerateInMemory = false;
				cparams.IncludeDebugInformation = false;
				cparams.ReferencedAssemblies.AddRange (references);
				cparams.ReferencedAssemblies.Add (typeof (Instantly).Assembly.Location);

				// HACK: Wrap test code into a proper method
				evalSource = "namespace Instant.User { static class Evaluation { static void Evaluate() {" + evalSource + " } } }";
 
				List<string> sources = project.Sources.AsParallel().Select (
						e => e.Fold (async f => await f.OpenText().ReadToEndAsync(), Task.FromResult)
					).ToListAsync().Result;

				sources.Add (evalSource);

				// TODO: Needs to be in separate AppDomain so we can unload
				CSharpCodeProvider provider = new CSharpCodeProvider();
				CompilerResults results = provider.CompileAssemblyFromSource (cparams, sources.ToArray());
				if (results.Errors.HasErrors)
					return;

				MethodInfo method = results.CompiledAssembly.GetType ("Instant.User.Evaluation").GetMethod ("Evaluate", BindingFlags.NonPublic | BindingFlags.Static);
				method.Invoke (null, null);
			}, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default);
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

			File.Copy (typeof (Instantly).Assembly.Location, Path.Combine (path, "Instantly.dll"));

			return path;
		}
	}
}