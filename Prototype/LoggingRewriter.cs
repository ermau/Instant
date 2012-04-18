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

		protected override SyntaxNode VisitPrefixUnaryExpression (PrefixUnaryExpressionSyntax node)
		{
			IdentifierNameSyntax name = node.Operand as IdentifierNameSyntax;
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

		protected override SyntaxNode VisitVariableDeclarator (VariableDeclaratorSyntax node)
		{
			if (node.InitializerOpt == null)
				return base.VisitVariableDeclarator (node);

			EqualsValueClauseSyntax equals = node.InitializerOpt;
			equals = equals.Update (equals.EqualsToken, GetLogExpression (node.Identifier.ValueText, equals.Value));

			return node.Update (node.Identifier, null, equals);
		}

		protected override SyntaxNode VisitReturnStatement (ReturnStatementSyntax node)
		{
			if (node.ExpressionOpt == null)
				return base.VisitReturnStatement (node);

			return node.Update (node.ReturnKeyword, GetReturnExpression (this.currentMethod.Identifier.ValueText, node.ExpressionOpt.ToString()), node.SemicolonToken);
		}

		protected override SyntaxNode VisitBinaryExpression (BinaryExpressionSyntax node)
		{
			switch (node.Kind)
			{
				case SyntaxKind.AssignExpression:
				case SyntaxKind.AndAssignExpression:
				case SyntaxKind.DivideAssignExpression:
				case SyntaxKind.AddAssignExpression:
				case SyntaxKind.ModuloAssignExpression:
				case SyntaxKind.ExclusiveOrAssignExpression:
				case SyntaxKind.LeftShiftAssignExpression:
				case SyntaxKind.MultiplyAssignExpression:
				case SyntaxKind.OrAssignExpression:
				case SyntaxKind.RightShiftAssignExpression:
				case SyntaxKind.SubtractAssignExpression:

					string name = ((IdentifierNameSyntax) node.Left).PlainName;
					return node.Update (node.Left, node.OperatorToken, GetLogExpression (name, node.Right));

				default:
					return base.VisitBinaryExpression (node);
			}
		}

		private MethodDeclarationSyntax currentMethod;
		protected override SyntaxNode VisitMethodDeclaration (MethodDeclarationSyntax node)
		{
			this.currentMethod = node;
			return base.VisitMethodDeclaration (node);
		}

		private int loopLevel;
		protected override SyntaxNode VisitBlock (BlockSyntax node)
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
					else if (statement is ReturnStatementSyntax && ((ReturnStatementSyntax)statement).ExpressionOpt == null)
						statements.Add (ReturnStatement);
				}

				statements.Add (statement);

				var es = statement as ExpressionStatementSyntax;
				if (es != null)
				{
					var postfixUnary = es.ChildNodes().OfType<PostfixUnaryExpressionSyntax>().FirstOrDefault();
					if (postfixUnary != null)
					{
						var name = postfixUnary.Operand as IdentifierNameSyntax;
						if (name != null)
							statements.Add (Syntax.ExpressionStatement (GetLogExpression (name.PlainName, name.PlainName)));
					}
				}

				if (loop)
					statements.Add (EndLoopStatement);
			}

			return node.Update (node.OpenBraceToken, Syntax.List<StatementSyntax> (statements), node.CloseBraceToken);
		}

		private readonly SyntaxAnnotation isLoop = new SyntaxAnnotation();

		protected override SyntaxNode VisitWhileStatement (WhileStatementSyntax node)
		{
			node = node.Update (node.WhileKeyword, node.OpenParenToken, node.Condition, node.CloseParenToken,
			                    GetLoopBlock (node.Statement));

			return base.VisitWhileStatement ((WhileStatementSyntax)node.WithAdditionalAnnotations (this.isLoop));
		}

		protected override SyntaxNode VisitForStatement (ForStatementSyntax node)
		{
			node = node.Update (node.ForKeyword, node.OpenParenToken, node.DeclarationOpt,
			                    node.Initializers, node.FirstSemicolonToken, node.ConditionOpt,
			                    node.SecondSemicolonToken, node.Incrementors, node.CloseParenToken,
			                    GetLoopBlock (node.Statement));

			this.loopLevel++;
			var statement = base.VisitForStatement ((ForStatementSyntax)node.WithAdditionalAnnotations (this.isLoop));
			this.loopLevel--;
			return statement;
		}

		protected override SyntaxNode VisitForEachStatement (ForEachStatementSyntax node)
		{
			node = node.Update (node.ForEachKeyword, node.OpenParenToken, node.Type,
			                    node.Identifier, node.InKeyword, node.Expression, node.CloseParenToken,
			                    GetLoopBlock (node.Statement));

			this.loopLevel++;
			var statement = base.VisitForEachStatement ((ForEachStatementSyntax)node.WithAdditionalAnnotations (this.isLoop));
			this.loopLevel--;
			return statement;
		}

		protected override SyntaxNode VisitDoStatement (DoStatementSyntax node)
		{
			node = node.Update (node.DoKeyword, GetLoopBlock (node.Statement), node.WhileKeyword,
			                    node.OpenParenToken, node.Condition, node.CloseParenToken, node.SemicolonToken);

			this.loopLevel++;
			var statement = base.VisitDoStatement ((DoStatementSyntax) node.WithAdditionalAnnotations (this.isLoop));
			this.loopLevel--;
			return statement;
		}

		protected override SyntaxNode VisitIfStatement (IfStatementSyntax node)
		{
			if (!node.DescendentNodes().OfType<BlockSyntax>().Any())
			{
				node = node.Update (node.IfKeyword, node.OpenParenToken, node.Condition, node.CloseParenToken,
				                    Syntax.Block (statements: node.Statement), node.ElseOpt);
			}

			if (node.ElseOpt != null)
			{
				ElseClauseSyntax elseOpt = node.ElseOpt;
				IfStatementSyntax ifSyntax = elseOpt.Statement as IfStatementSyntax;
				if (ifSyntax != null)
				{
					if (!ifSyntax.DescendentNodes().OfType<BlockSyntax>().Any())
					{
						ifSyntax = ifSyntax.Update (ifSyntax.IfKeyword, ifSyntax.OpenParenToken, ifSyntax.Condition, ifSyntax.CloseParenToken,
						                            Syntax.Block (statements: ifSyntax.Statement), ifSyntax.ElseOpt);

						elseOpt = elseOpt.Update (elseOpt.ElseKeyword, ifSyntax);
					}
				}
				else if (!elseOpt.DescendentNodes().OfType<BlockSyntax>().Any())
					elseOpt = node.ElseOpt.Update (node.ElseOpt.ElseKeyword, Syntax.Block (statements: node.ElseOpt.Statement));
				
				if (elseOpt != node.ElseOpt)
					node = node.Update (node.IfKeyword, node.OpenParenToken, node.Condition, node.CloseParenToken, node.Statement, elseOpt);
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

			return Syntax.Block (statements: Syntax.List<StatementSyntax> (statements));
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