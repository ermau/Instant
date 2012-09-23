//
// Extensions.cs
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

using System.Collections.Generic;
using Roslyn.Compilers.CSharp;

namespace Instant
{
	public static class Extensions
	{
		public static IdentifierNameSyntax FindIdentifierName (this ExpressionSyntax expression)
		{
			IdentifierNameSyntax name = expression as IdentifierNameSyntax;
			if (name != null)
				return name;

			BinaryExpressionSyntax binaryExpression = expression as BinaryExpressionSyntax;
			if (binaryExpression != null)
				return FindIdentifierName(binaryExpression.Right);

			PostfixUnaryExpressionSyntax postfixUnaryExpression = expression as PostfixUnaryExpressionSyntax;
			if (postfixUnaryExpression != null)
				return FindIdentifierName (postfixUnaryExpression.Operand);

			PrefixUnaryExpressionSyntax prefixUnaryExpression = expression as PrefixUnaryExpressionSyntax;
			if (prefixUnaryExpression != null)
				return FindIdentifierName (prefixUnaryExpression.Operand);

			return null;
		}

		public static IEnumerable<SyntaxTrivia> InsertComment (this IEnumerable<SyntaxTrivia> self, SyntaxTrivia insert)
		{
			bool yielded = false;

			foreach (SyntaxTrivia trivia in self)
			{
				yield return trivia;

				if (trivia.Kind == SyntaxKind.EndOfLineTrivia)
				{
					yield return insert;
					yielded = true;
				}
			}

			if (!yielded)
				yield return insert;
		}

		public static IEnumerable<T> Concat<T> (this IEnumerable<T> self, T concated)
		{
			foreach (T element in self)
				yield return element;

			yield return concated;
		}

		public static IEnumerable<T> Prepend<T> (this IEnumerable<T> self, T prepended)
		{
			yield return prepended;

			foreach (T element in self)
				yield return element;
		}
	}
}