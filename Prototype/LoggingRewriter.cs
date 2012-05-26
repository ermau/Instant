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

		protected override SyntaxNode VisitPrefixUnaryExpression (PrefixUnaryExpressionSyntax node)
		{
			IdentifierNameSyntax name = FindIdentifierName (node.Operand);
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

		protected SeparatedSyntaxList<ExpressionSyntax> RewritePostfixUnarys (SeparatedSyntaxList<ExpressionSyntax> nodes)
		{
			if (nodes.Count == 0)
				return nodes;

			List<ExpressionSyntax> expressions = new List<ExpressionSyntax> (nodes.Count);
			foreach (var node in nodes)
			{
				var newNode = RewritePostfixUnarys (node);
				if (newNode != null)
					expressions.Add (newNode);
			}

			return Syntax.SeparatedList (expressions, Enumerable.Repeat (Syntax.Token (SyntaxKind.CommaToken), expressions.Count - 1));
		}

		protected SyntaxList<ExpressionSyntax> RewritePostfixUnarys (SyntaxList<ExpressionSyntax> nodes)
		{
			if (nodes.Count == 0)
				return nodes;

			return Syntax.List (nodes.Select (RewritePostfixUnarys));
		}

		protected ExpressionSyntax RewritePostfixUnarys (ExpressionSyntax node)
		{
			var expressions = node.DescendentNodesAndSelf().OfType<PostfixUnaryExpressionSyntax>().ToArray();
			if (expressions.Length == 0)
				return node;

			// We'll use a string builder construct our expression.
			StringBuilder builder = new StringBuilder ("LogPostfixValues ((");
			builder.Append (node.ToString());
			builder.Append ("), ");

			HashSet<string> names = new HashSet<string>();

			bool found = false;
			foreach (var expr in expressions)
			{
				if (expr.Kind != SyntaxKind.PostIncrementExpression && expr.Kind != SyntaxKind.PostDecrementExpression)
					continue; // These aren't the droids we're looking for.

				IdentifierNameSyntax name = FindIdentifierName (expr);
				if (name == null || names.Contains (name.PlainName))
					continue; // We didn't find a name, or it was already logged

				names.Add (name.PlainName);

				if (found)
					builder.Append (", ");

				// We need to specify our generic types so we match
				// the params argument type
				builder.Append ("new Tuple<string, object> (\"");
				builder.Append (name.PlainName);
				builder.Append ("\", ");
				builder.Append (name.PlainName);
				builder.Append (")");
				found = true;
			}

			if (!found)
				return node; // We didn't find any usable expressions

			builder.Append (")");

			// Parse and return our new expression.
			return Syntax.ParseExpression (builder.ToString());
		}

		protected override SyntaxNode VisitExpressionStatement (ExpressionStatementSyntax node)
		{
			ExpressionSyntax expression = RewritePostfixUnarys (node.Expression);
			if (expression != node.Expression)
				node = node.Update (expression, node.SemicolonToken);

			return base.VisitExpressionStatement (node);
		}

		protected override SyntaxNode VisitVariableDeclarator (VariableDeclaratorSyntax node)
		{
			var newNode = base.VisitVariableDeclarator (node);
			if ((node = newNode as VariableDeclaratorSyntax) == null)
				return newNode;

			if (node.InitializerOpt == null)
				return base.VisitVariableDeclarator (node);

			EqualsValueClauseSyntax equals = node.InitializerOpt;

			ExpressionSyntax value = RewritePostfixUnarys (equals.Value);
			equals = equals.Update (equals.EqualsToken, GetLogExpression (node.Identifier.ValueText, value));

			return node.Update (node.Identifier, null, equals);
		}

		protected override SyntaxNode VisitReturnStatement (ReturnStatementSyntax node)
		{
			if (node.ExpressionOpt == null || this.currentMethod == null)
				return base.VisitReturnStatement (node);

			ExpressionSyntax expression = RewritePostfixUnarys (node.ExpressionOpt);
			return node.Update (node.ReturnKeyword, GetReturnExpression (this.currentMethod.Identifier.ValueText, expression.ToString()), node.SemicolonToken);
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

		protected override SyntaxNode VisitBinaryExpression (BinaryExpressionSyntax node)
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
					return node.Update (node.Left, node.OperatorToken, GetLogExpression (nameSyntax.PlainName, node.Right));

				default:
					return node;
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

				if (loop)
					statements.Add (EndLoopStatement);
			}

			return node.Update (node.OpenBraceToken, Syntax.List<StatementSyntax> (statements), node.CloseBraceToken);
		}

		private readonly SyntaxAnnotation isLoop = new SyntaxAnnotation();

		protected override SyntaxNode VisitWhileStatement (WhileStatementSyntax node)
		{
			ExpressionSyntax conditional = RewritePostfixUnarys (node.Condition);

			node = node.Update (node.WhileKeyword, node.OpenParenToken, conditional, node.CloseParenToken,
			                    GetLoopBlock (node.Statement));

			return base.VisitWhileStatement ((WhileStatementSyntax)node.WithAdditionalAnnotations (this.isLoop));
		}

		protected override SyntaxNode VisitForStatement (ForStatementSyntax node)
		{
			var inits = RewritePostfixUnarys (node.Initializers);
			var condition = RewritePostfixUnarys (node.ConditionOpt);
			var incrs = RewritePostfixUnarys (node.Incrementors);

			node = node.Update (node.ForKeyword, node.OpenParenToken, node.DeclarationOpt,
			                    inits, node.FirstSemicolonToken, condition,
			                    node.SecondSemicolonToken, incrs, node.CloseParenToken,
			                    GetLoopBlock (node.Statement));

			this.loopLevel++;
			var statement = base.VisitForStatement ((ForStatementSyntax)node.WithAdditionalAnnotations (this.isLoop));
			this.loopLevel--;
			return statement;
		}

		protected override SyntaxNode VisitForEachStatement (ForEachStatementSyntax node)
		{
			var expr = RewritePostfixUnarys (node.Expression);

			node = node.Update (node.ForEachKeyword, node.OpenParenToken, node.Type,
			                    node.Identifier, node.InKeyword, expr, node.CloseParenToken,
			                    GetLoopBlock (node.Statement));

			this.loopLevel++;
			var statement = base.VisitForEachStatement ((ForEachStatementSyntax)node.WithAdditionalAnnotations (this.isLoop));
			this.loopLevel--;
			return statement;
		}

		protected override SyntaxNode VisitDoStatement (DoStatementSyntax node)
		{
			ExpressionSyntax condition = RewritePostfixUnarys (node.Condition);

			node = node.Update (node.DoKeyword, GetLoopBlock (node.Statement), node.WhileKeyword,
			                    node.OpenParenToken, condition, node.CloseParenToken, node.SemicolonToken);

			this.loopLevel++;
			var statement = base.VisitDoStatement ((DoStatementSyntax) node.WithAdditionalAnnotations (this.isLoop));
			this.loopLevel--;
			return statement;
		}

		protected override SyntaxNode VisitYieldStatement(YieldStatementSyntax node)
		{
			var expr = RewritePostfixUnarys (node.ExpressionOpt);
			if (expr != node.ExpressionOpt)
				node = node.Update (node.YieldKeyword, node.ReturnOrBreakKeyword, expr, node.SemicolonToken);
			
			return base.VisitYieldStatement (node);
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

			var conditional = RewritePostfixUnarys (node.Condition);
			if (conditional != node.Condition)
				node = node.Update (node.IfKeyword, node.OpenParenToken, conditional, node.CloseParenToken, node.Statement, node.ElseOpt);

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