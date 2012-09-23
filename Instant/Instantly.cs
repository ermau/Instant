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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Instant.Operations;
using Roslyn.Compilers;
using Roslyn.Compilers.CSharp;
using Roslyn.Scripting;
using Roslyn.Scripting.CSharp;
using Roslyn.Services;

namespace Instant
{
	public static class Instantly
	{
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

		public static Task<IDictionary<int, MethodCall>> Evaluate (string code, string evalSource, CancellationToken cancelToken)
		{
			return Task<IDictionary<int, MethodCall>>.Factory.StartNew (() =>
			{
			    ScriptEngine engine = new ScriptEngine();
                engine.AddReference (typeof (string).Assembly);// mscorlib
                engine.AddReference (typeof (System.Diagnostics.Stopwatch).Assembly);// System.dll
				engine.AddReference (typeof (Enumerable).Assembly); // System.Core.dll
                engine.AddReference (typeof (Hook).Assembly); // this

			    engine.ImportNamespace ("System");
			    engine.ImportNamespace ("System.Collections.Generic");
			    engine.ImportNamespace ("System.Diagnostics");

				int id = Hook.CreateSubmission (cancelToken);

				SyntaxNode instrumented = Instrument (code, id).Result;
				
				try
				{
				    Session session = engine.CreateSession();
					session.Execute (instrumented.ToString());
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
					return Hook.RootCalls;
				}
				catch (Exception)
				{
					throw;
				}
			});
		}
	}
}