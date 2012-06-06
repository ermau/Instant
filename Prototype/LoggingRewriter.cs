//
// LoggingRewriter.cs
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
using System.Text;
using Roslyn.Compilers;
using Roslyn.Compilers.CSharp;

namespace LiveCSharp
{
	internal class LoggingRewriter
		: SyntaxRewriter
	{
		private static readonly StatementSyntax ReturnStatement = Syntax.ParseStatement ("LogReturn();");
		private static readonly StatementSyntax BeginLoopStatement = Syntax.ParseStatement ("BeginLoop();");
		private static readonly StatementSyntax EndLoopStatement = Syntax.ParseStatement ("EndLoop();");
		private static readonly StatementSyntax BeginInsideLoopStatement = Syntax.ParseStatement ("BeginInsideLoop();");
		private static readonly StatementSyntax EndInsideLoopStatement = Syntax.ParseStatement ("EndInsideLoop();");
		private static readonly SyntaxToken AssignToken = Syntax.Token (SyntaxKind.EqualsToken);

		public override SyntaxNode VisitPrefixUnaryExpression (PrefixUnaryExpressionSyntax node)
		{
			IdentifierNameSyntax name = FindIdentifierName (node);
			if (name != null)
			{
				switch (node.Kind)
				{
					case SyntaxKind.PreIncrementExpression:
					case SyntaxKind.PreDecrementExpression:
						return GetLogExpression (name.PlainName, node);
				}
			}

			return base.VisitPrefixUnaryExpression (node);
		}

		public override SyntaxNode VisitPostfixUnaryExpression(PostfixUnaryExpressionSyntax node)
		{
			IdentifierNameSyntax name = FindIdentifierName (node);
			if (name != null)
			{
				switch (node.Kind)
				{
					case SyntaxKind.PostIncrementExpression:
					case SyntaxKind.PostDecrementExpression:
						return Syntax.ParseExpression ("LogPostfix (" + node + ", \"" + name.PlainName + "\", " + name.PlainName + ")");
				}
			}

			return base.VisitPostfixUnaryExpression(node);
		}

		public override SyntaxNode VisitVariableDeclarator (VariableDeclaratorSyntax node)
		{
			var newNode = base.VisitVariableDeclarator (node);
			if ((node = newNode as VariableDeclaratorSyntax) == null)
				return newNode;

			if (node.Initializer == null)
				return base.VisitVariableDeclarator (node);

			return node.WithInitializer (node.Initializer.WithValue (GetLogExpression (node.Identifier.ValueText, node.Initializer.Value)));
		}

		public override SyntaxNode VisitReturnStatement (ReturnStatementSyntax node)
		{
			if (node.Expression == null || this.currentMethod == null)
				return base.VisitReturnStatement (node);

			return node.WithExpression (GetReturnExpression (this.currentMethod.Identifier.ValueText, node.Expression.ToString()));
		}

		private IdentifierNameSyntax FindIdentifierName (ExpressionSyntax expression)
		{
			IdentifierNameSyntax name = expression as IdentifierNameSyntax;
			if (name != null)
				return name;

			BinaryExpressionSyntax binaryExpression = expression as BinaryExpressionSyntax;
			if (binaryExpression != null)
				return FindIdentifierName (binaryExpression.Right);

			PostfixUnaryExpressionSyntax postfixUnaryExpression = expression as PostfixUnaryExpressionSyntax;
			if (postfixUnaryExpression != null)
				return FindIdentifierName (postfixUnaryExpression.Operand);

			PrefixUnaryExpressionSyntax prefixUnaryExpression = expression as PrefixUnaryExpressionSyntax;
			if (prefixUnaryExpression != null)
				return FindIdentifierName (prefixUnaryExpression.Operand);

			return null;
		}

		private SyntaxKind GetComplexAssignOperator (SyntaxKind kind)
		{
			switch (kind)
			{
				case SyntaxKind.AddAssignExpression:
					return SyntaxKind.PlusToken;
				case SyntaxKind.SubtractAssignExpression:
					return SyntaxKind.MinusToken;
				case SyntaxKind.MultiplyAssignExpression:
					return SyntaxKind.AsteriskToken;
				case SyntaxKind.DivideAssignExpression:
					return SyntaxKind.SlashToken;
				case SyntaxKind.AndAssignExpression:
					return SyntaxKind.AmpersandToken;
				case SyntaxKind.OrAssignExpression:
					return SyntaxKind.BarToken;
				case SyntaxKind.ExclusiveOrAssignExpression:
					return SyntaxKind.CaretToken;
				case SyntaxKind.RightShiftAssignExpression:
					return SyntaxKind.GreaterThanGreaterThanToken;
				case SyntaxKind.LeftShiftAssignExpression:
					return SyntaxKind.LessThanLessThanToken;
				case SyntaxKind.ModuloAssignExpression:
					return SyntaxKind.PercentToken;

				default:
					throw new ArgumentException();
			}
		}

		public override SyntaxNode VisitBinaryExpression (BinaryExpressionSyntax node)
		{
			var newNode = base.VisitBinaryExpression (node);
			node = newNode as BinaryExpressionSyntax;
			if (node == null)
				return newNode;

			var nameSyntax = FindIdentifierName (node.Left);
			if (nameSyntax == null)
				return newNode;

			switch (node.Kind)
			{
				case SyntaxKind.AddAssignExpression:
				case SyntaxKind.OrAssignExpression:
				case SyntaxKind.SubtractAssignExpression:
				case SyntaxKind.MultiplyAssignExpression:
				case SyntaxKind.DivideAssignExpression:
				case SyntaxKind.ModuloAssignExpression:
				case SyntaxKind.RightShiftAssignExpression:
				case SyntaxKind.LeftShiftAssignExpression:
				case SyntaxKind.AndAssignExpression:
				case SyntaxKind.ExclusiveOrAssignExpression:
					var token = Syntax.Token (GetComplexAssignOperator (node.Kind));

					ExpressionSyntax expr = Syntax.ParseExpression (nameSyntax.PlainName + token + node.Right);

					return node.Update (node.Left, AssignToken, GetLogExpression (nameSyntax.PlainName, expr));

				case SyntaxKind.AssignExpression:
					return node.WithRight (GetLogExpression (nameSyntax.PlainName, node.Right));

				default:
					return node;
			}
		}

		private MethodDeclarationSyntax currentMethod;
		public override SyntaxNode VisitMethodDeclaration (MethodDeclarationSyntax node)
		{
			this.currentMethod = node;
			return base.VisitMethodDeclaration (node);
		}

		private int loopLevel;
		public override SyntaxNode VisitBlock (BlockSyntax node)
		{
			var results = base.VisitBlock (node);
			node = results as BlockSyntax;
			if (node == null)
				return results;

			List<StatementSyntax> statements = new List<StatementSyntax> (node.Statements.Count + 4);

			foreach (StatementSyntax statement in node.Statements)
			{
				bool loop = statement.HasAnnotation (this.isLoop);
				if (loop)
					statements.Add (BeginLoopStatement);

				if (this.loopLevel > 0)
				{
					if (statement is ContinueStatementSyntax || statement is BreakStatementSyntax)
						statements.Add (EndInsideLoopStatement);
					else if (statement is ReturnStatementSyntax && ((ReturnStatementSyntax)statement).Expression == null)
						statements.Add (ReturnStatement);
				}

				statements.Add (statement);

				if (loop)
					statements.Add (EndLoopStatement);
			}

			return node.WithStatements (Syntax.List<StatementSyntax> (statements));
		}

		private readonly SyntaxAnnotation isLoop = new SyntaxAnnotation();

		public override SyntaxNode VisitWhileStatement (WhileStatementSyntax node)
		{
			this.loopLevel++;

			var statement = base.VisitWhileStatement (node
				.WithStatement (GetLoopBlock (node.Statement))
				.WithAdditionalAnnotations (this.isLoop));

			this.loopLevel--;

			return statement;
		}

		public override SyntaxNode VisitForStatement (ForStatementSyntax node)
		{
			this.loopLevel++;

			var statement = base.VisitForStatement (node
				.WithStatement (GetLoopBlock (node.Statement))
				.WithAdditionalAnnotations (this.isLoop));

			this.loopLevel--;

			return statement;
		}

		public override SyntaxNode VisitForEachStatement (ForEachStatementSyntax node)
		{
			this.loopLevel++;

			var statement = base.VisitForEachStatement (node
				.WithStatement (GetLoopBlock (node.Statement))
				.WithAdditionalAnnotations (this.isLoop));

			this.loopLevel--;

			return statement;
		}

		public override SyntaxNode VisitDoStatement (DoStatementSyntax node)
		{
			this.loopLevel++;

			var statement = base.VisitDoStatement (node
				.WithStatement (GetLoopBlock (node.Statement))
				.WithAdditionalAnnotations (this.isLoop));

			this.loopLevel--;

			return statement;
		}

		public override SyntaxNode VisitIfStatement (IfStatementSyntax node)
		{
			if (!node.DescendantNodes().OfType<BlockSyntax>().Any())
				node = node.WithStatement (Syntax.Block (node.Statement));

			if (node.Else != null)
			{
				ElseClauseSyntax elseOpt = node.Else;
				IfStatementSyntax ifSyntax = elseOpt.Statement as IfStatementSyntax;
				if (ifSyntax != null)
				{
					if (!ifSyntax.DescendantNodes().OfType<BlockSyntax>().Any())
					{
						ifSyntax = ifSyntax.WithStatement (Syntax.Block (ifSyntax.Statement));
						elseOpt = elseOpt.WithStatement (ifSyntax);
					}
				}
				else if (!elseOpt.DescendantNodes().OfType<BlockSyntax>().Any())
					elseOpt = node.Else.WithStatement (Syntax.Block (node.Else.Statement));
				
				if (elseOpt != node.Else)
					node = node.WithElse (elseOpt);
			}

			return base.VisitIfStatement (node);
		}

		private BlockSyntax GetLoopBlock (StatementSyntax statement)
		{
			List<StatementSyntax> statements;
			BlockSyntax block = statement as BlockSyntax;
			if (block != null)
				statements = new List<StatementSyntax> (block.Statements);
			else
				statements = new List<StatementSyntax>();

			statements.Insert (0, BeginInsideLoopStatement);
			statements.Add (EndInsideLoopStatement);

			return Syntax.Block (Syntax.List<StatementSyntax> (statements));
		}

		private ExpressionSyntax GetLogExpression (string name, SyntaxNode value)
		{
			return GetLogExpression (name, value.ToString());
		}

		private ExpressionSyntax GetLogExpression (string name, string value)
		{
			return Syntax.ParseExpression ("LogObject (\"" + (name ?? "null") + "\", " + value + ")");
		}

		private ExpressionSyntax GetReturnExpression (string name, string value)
		{
			return Syntax.ParseExpression ("LogReturn (" + value + ");");
		}
	}
}