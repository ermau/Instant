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
		/// <param name="submissionId">The submission ID retrieved from <see cref="Hook.CreateSubmission"/>.</param>
		/// <returns>A task for a <see cref="SyntaxNode"/> representing the instrumented code.</returns>
		/// <seealso cref="Hook.CreateSubmission"/>
		public static Task<SyntaxNode> Instrument (SyntaxNode code, int submissionId)
		{
			if (code == null)
				throw new ArgumentNullException ("code");

			return Task<SyntaxNode>.Factory.StartNew (s =>
			{
				SyntaxNode root = (SyntaxNode)s;
				//root = new FixingRewriter().Visit (root);

				return new LoggingRewriter (submissionId).Visit (root);
			}, code);
		}

		/// <summary>
		/// Instruments the supplied code to call the <see cref="Hook"/> methods.
		/// </summary>
		/// <param name="code">The code to instrument.</param>
		/// <param name="submissionId">The submission ID retrieved from <see cref="Hook.CreateSubmission"/>.</param>
		/// <returns>A task for a <see cref="SyntaxNode"/> representing the instrumented code.</returns>
		/// <seealso cref="Hook.CreateSubmission"/>
		public static Task<SyntaxNode> Instrument (string code, int submissionId)
		{
			if (code == null)
				throw new ArgumentNullException ("code");

			return Task<SyntaxNode>.Factory.StartNew (s =>
			{
				string c = (string)s;

				return Instrument (Syntax.ParseCompilationUnit (c), submissionId).Result;
			}, code);
		}

		/// <summary>
		/// Evaluates pre-instrumented code.
		/// </summary>
		/// <param name="instrumentedCode"></param>
		/// <param name="evalSource"></param>
		/// <returns>A task for a dictionary of managed thread IDs to <see cref="MethodCall"/>s.</returns>
		/// <seealso cref="Instrument(string,int)"/>
		public static Task<IDictionary<int, MethodCall>> Evaluate (SyntaxNode instrumentedCode, string evalSource)
		{
			if (instrumentedCode == null)
				throw new ArgumentNullException ("instrumentedCode");
			if (evalSource == null)
				throw new ArgumentNullException ("evalSource");

			return Task<IDictionary<int, MethodCall>>.Factory.StartNew (() =>
			{
				// Eventually when we support full projects, we can pull these
				// directly from the project. Until then, general basics.
				ScriptEngine engine = new ScriptEngine();
				engine.AddReference (typeof (string).Assembly);// mscorlib
				engine.AddReference (typeof (System.Diagnostics.Stopwatch).Assembly);// System.dll
				engine.AddReference (typeof (Enumerable).Assembly); // System.Core.dll
				engine.AddReference (typeof (Hook).Assembly); // this

				engine.ImportNamespace ("System");
				engine.ImportNamespace ("System.Collections.Generic");
				engine.ImportNamespace ("System.Diagnostics");

				try
				{
					Session session = engine.CreateSession();
					session.Execute (instrumentedCode.ToString());
					session.Execute (evalSource);
					
					return Hook.RootCalls;
				}
				catch (CompilationErrorException)
				{
					return null;
				}
				catch (OutOfMemoryException)
				{
					return null;
				}
				catch (OperationCanceledException)
				{
					// We can still potentially display results up to the cancellation
					return Hook.RootCalls;
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
		public static Task<IDictionary<int, MethodCall>> InstrumentAndEvaluate (string code, string evalSource, CancellationToken cancelToken)
		{
			int submissionId = Hook.CreateSubmission (cancelToken);
			return Instrument (code, submissionId).ContinueWith (t => Evaluate (t.Result, evalSource)).Unwrap();
		}
	}
}