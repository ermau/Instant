using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Roslyn.Compilers.CSharp;

namespace LiveCSharp
{
	public static class Extensions
	{
		public static bool GetIsLoop (this SyntaxNode node)
		{
			return
				node is ForStatementSyntax
				|| node is ForEachStatementSyntax
				|| node is DoStatementSyntax
				|| node is WhileStatementSyntax;
		}

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