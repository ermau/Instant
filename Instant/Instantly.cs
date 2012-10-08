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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Instant.Operations;
using Roslyn.Compilers;
using Roslyn.Compilers.CSharp;
using Roslyn.Scripting;
using Roslyn.Scripting.CSharp;

namespace Instant
{
	public static class Instantly
	{
		/// <summary>
		/// Instruments the supplied code to call the <see cref="Hook"/> methods.
		/// </summary>
		/// <param name="code">The code to instrument.</param>
		/// <param name="submission">The submission retrieved from <see cref="Hook.CreateSubmission"/>.</param>
		/// <returns>A task for a <see cref="SyntaxNode"/> representing the instrumented code.</returns>
		/// <seealso cref="Hook.CreateSubmission"/>
		public static Task<SyntaxNode> Instrument (SyntaxNode code, Submission submission)
		{
			if (code == null)
				throw new ArgumentNullException ("code");

			return Task<SyntaxNode>.Factory.StartNew (s =>
				InstrumentCore ((SyntaxNode)s, submission), code);
		}

		/// <summary>
		/// Instruments the supplied code to call the <see cref="Hook"/> methods.
		/// </summary>
		/// <param name="code">The code to instrument.</param>
		/// <param name="submission">The submission retrieved from <see cref="Hook.CreateSubmission"/>.</param>
		/// <returns>A task for a <see cref="SyntaxNode"/> representing the instrumented code.</returns>
		/// <seealso cref="Hook.CreateSubmission"/>
		public static Task<SyntaxNode> Instrument (string code, Submission submission)
		{
			if (code == null)
				throw new ArgumentNullException ("code");

			return Task<SyntaxNode>.Factory.StartNew (s =>
			{
				SyntaxTree tree = SyntaxTree.ParseText ((string)s, cancellationToken: submission.CancelToken);
				SyntaxNode node = tree.GetRoot (submission.CancelToken);

				return InstrumentCore (node, submission);
			}, code);
		}

		private static SyntaxNode InstrumentCore (SyntaxNode root, Submission submission)
		{
			if (root.GetDiagnostics().Any (d => d.Info.Severity == DiagnosticSeverity.Warning))
				return null;

			return new LoggingRewriter (submission).Visit (root);
		}

		/// <summary>
		/// Evaluates pre-instrumented code.
		/// </summary>
		/// <param name="instrumentedCode"></param>
		/// <param name="evalSource"></param>
		/// <returns>A task for a dictionary of managed thread IDs to <see cref="MethodCall"/>s.</returns>
		/// <seealso cref="Instrument(string,Instant.Submission)"/>
		public static Task Evaluate (SyntaxNode instrumentedCode, string evalSource)
		{
			if (instrumentedCode == null)
				throw new ArgumentNullException ("instrumentedCode");
			if (evalSource == null)
				throw new ArgumentNullException ("evalSource");

			return Task.Factory.StartNew (() =>
			{
				// Eventually when we support full projects, we can pull these
				// directly from the project. Until then, general basics.
				ScriptEngine engine = new ScriptEngine();
				engine.AddReference (typeof (string).Assembly); // mscorlib
				engine.AddReference (typeof (System.Diagnostics.Stopwatch).Assembly); // System.dll
				engine.AddReference (typeof (Enumerable).Assembly); // System.Core
				engine.AddReference (typeof (Hook).Assembly); // Instant.dll

				engine.ImportNamespace ("System");
				engine.ImportNamespace ("System.Linq");
				engine.ImportNamespace ("System.Collections.Generic");
				engine.ImportNamespace ("System.Diagnostics");

				try
				{
					Session session = engine.CreateSession();
					session.Execute (instrumentedCode.ToString());
					session.Execute (evalSource);
				}
				catch (CompilationErrorException)
				{
				}
				catch (OutOfMemoryException)
				{
				}
				catch (OperationCanceledException)
				{ // We can still potentially display results up to the cancellation
				}
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
			
			SyntaxNode instrumented = await Instrument (code, submission).ConfigureAwait (false);
			if (instrumented == null)
				return null;

			await Evaluate (instrumented, evalSource).ConfigureAwait (false);

			return sink.GetRootCalls();
		}
	}
}