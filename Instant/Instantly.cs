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
using System.Linq;
using System.Threading.Tasks;
using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.TypeSystem;

namespace Instant
{
	public static class Instantly
	{
		static Instantly()
		{
			AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
			{
				if (args.Name.StartsWith ("Instant"))
					return typeof (Instantly).Assembly;

				return args.RequestingAssembly;
			};
		}

		/// <summary>
		/// Instruments the supplied code to call the <see cref="Hook"/> methods.
		/// </summary>
		/// <param name="code">The code to instrument.</param>
		/// <param name="submissionId">The submission ID.</param>
		/// <returns>A task for a <see cref="string"/> representing the instrumented code.</returns>
		/// <seealso cref="Hook.CreateSubmission"/>
		public static Task<string> Instrument (string code, int submissionId)
		{
			if (code == null)
				throw new ArgumentNullException ("code");

			return Task<string>.Factory.StartNew (s =>
			{
				SyntaxTree tree = SyntaxTree.Parse ((string)s);

				InstrumentingRewriter rewriter = new InstrumentingRewriter (submissionId);
				tree.AcceptVisitor (rewriter);

				if (tree.Errors.Any (e => e.ErrorType == ErrorType.Error))
					return null;

				return tree.GetText();
			}, code);
		}
	}
}