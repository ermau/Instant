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
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.NRefactory.CSharp;
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

				return tree.GetText();
			}, code);
		}

		/// <summary>
		/// Evaluates pre-instrumented code.
		/// </summary>
		/// <param name="instrumentedCode"></param>
		/// <param name="evalSource"></param>
		/// <returns>A task for a dictionary of managed thread IDs to <see cref="MethodCall"/>s.</returns>
		/// <seealso cref="Instrument(string,Instant.Submission)"/>
		public static Task Evaluate (string instrumentedCode, string evalSource)
		{
			if (instrumentedCode == null)
				throw new ArgumentNullException ("instrumentedCode");
			if (evalSource == null)
				throw new ArgumentNullException ("evalSource");

			return Task.Factory.StartNew (() =>
			{
				var cparams = new CompilerParameters();
				cparams.GenerateInMemory = true;
				cparams.IncludeDebugInformation = false;
				cparams.ReferencedAssemblies.Add (typeof (string).Assembly.Location); // mscorlib
				cparams.ReferencedAssemblies.Add (typeof (System.Diagnostics.Stopwatch).Assembly.Location); // System.dll
				cparams.ReferencedAssemblies.Add (typeof (Enumerable).Assembly.Location); // System.Core
				cparams.ReferencedAssemblies.Add (typeof (Hook).Assembly.Location);

				// HACK: Wrap test code into a proper method
				evalSource = "namespace Instant.User { static class Evaluation { static void Evaluate() {" + evalSource + " } } }";

				CSharpCodeProvider provider = new CSharpCodeProvider();
				CompilerResults results = provider.CompileAssemblyFromSource (cparams, instrumentedCode, evalSource);
				if (results.Errors.Count > 0)
					return;

				MethodInfo method = results.CompiledAssembly.GetType ("Instant.User.Evaluation").GetMethod ("Evaluate", BindingFlags.NonPublic | BindingFlags.Static);
				method.Invoke (null, null);
			});
		}

		/// <summary>
		/// Instruments and evaluates the supplied source.
		/// </summary>
		/// <param name="code"></param>
		/// <param name="evalSource"></param>
		/// <param name="cancelToken"></param>
		/// <returns>A task for a dictionary of managed thread IDs to <see cref="MethodCall"/>s.</returns>
		public static async Task<IDictionary<int, MethodCall>> InstrumentAndEvaluate (string code, string evalSource, CancellationToken cancelToken)
		{
			var sink = new MemoryInstrumentationSink();
			Submission submission = Hook.CreateSubmission (sink, cancelToken);
			
			string instrumented = await Instrument (code, submission).ConfigureAwait (false);
			if (instrumented == null)
				return null;

			await Evaluate (instrumented, evalSource).ConfigureAwait (false);

			return sink.GetRootCalls();
		}
	}
}