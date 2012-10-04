//
// IdentifyingVisitor.cs
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
using Roslyn.Compilers;
using Roslyn.Compilers.CSharp;

namespace Instant
{
	public class IdentifyingVisitor
		: SyntaxRewriter
	{
		public override SyntaxNode VisitVariableDeclarator (VariableDeclaratorSyntax node)
		{
			var based = base.VisitVariableDeclarator (node);
			if (node.Initializer == null)
				return based;

			return based.WithTrailingTrivia (based.GetTrailingTrivia().Prepend (GetIdComment()));
		}

		private bool visitedMethod;
		public override SyntaxNode VisitMethodDeclaration (MethodDeclarationSyntax node)
		{
			if (this.visitedMethod)
				return node;

			this.visitedMethod = true;

			var based = base.VisitMethodDeclaration (node);

			SyntaxTrivia methodComment = GetIdComment();

			this.id += node.ParameterList.Parameters.Count;

			return based.WithLeadingTrivia (based.GetLeadingTrivia().Prepend (methodComment));
		}

		public override SyntaxNode VisitPostfixUnaryExpression (PostfixUnaryExpressionSyntax node)
		{
			var based = base.VisitPostfixUnaryExpression (node);

			switch (node.Kind)
			{
				case SyntaxKind.PostIncrementExpression:
				case SyntaxKind.PostDecrementExpression:
					return based.WithTrailingTrivia (based.GetTrailingTrivia().Prepend (GetIdComment()));

				default:
					return based;
			}
		}

		public override SyntaxNode VisitPrefixUnaryExpression (PrefixUnaryExpressionSyntax node)
		{
			var based = base.VisitPrefixUnaryExpression (node);
			switch (node.Kind)
			{
				case SyntaxKind.PreIncrementExpression:
				case SyntaxKind.PreDecrementExpression:
					return based.WithTrailingTrivia (based.GetTrailingTrivia().Prepend (GetIdComment()));

				default:
					return based;
			}
		}

		public override SyntaxNode VisitReturnStatement (ReturnStatementSyntax node)
		{
			var based = base.VisitReturnStatement (node);
			based = based.WithTrailingTrivia (based.GetTrailingTrivia().Prepend (GetIdComment()));
			return based;
		}

		public override SyntaxNode VisitBinaryExpression (BinaryExpressionSyntax node)
		{
			var based = base.VisitBinaryExpression (node);

			if (node.Left.FindIdentifierName() == null)
				return node;

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
				case SyntaxKind.AssignExpression:
					return based.WithTrailingTrivia (based.GetTrailingTrivia().Prepend (GetIdComment()));

				default:
					return node;
			}
		}

		public override SyntaxNode VisitBlock (BlockSyntax node)
		{
			var results = base.VisitBlock (node);
			if ((node = results as BlockSyntax) == null)
				return results;

			var list = new List<StatementSyntax>();
			foreach (StatementSyntax statement in node.Statements)
			{
				var s = statement;
				
				bool loop = s.HasAnnotation (this.isLoop);
				if (loop)
					s = s.WithLeadingTrivia (s.GetLeadingTrivia().InsertComment (GetIdComment (this.blockIds.Dequeue())));

				if (this.loopLevel > 0)
				{
					if (s is ContinueStatementSyntax || s is BreakStatementSyntax)
						s = s.WithTrailingTrivia (s.GetTrailingTrivia().Prepend (GetIdComment()));
				}

				list.Add (s);

				if (loop)
					this.id++;
				//if (loop)
				//	s = s.WithTrailingTrivia (s.GetTrailingTrivia().Prepend (GetIdComment()));
			}

			return node.WithStatements (Syntax.List<StatementSyntax> (list));
		}

		public override SyntaxNode VisitWhileStatement (WhileStatementSyntax node)
		{
			this.loopLevel++;

			var statement = base.VisitWhileStatement (node
								.WithStatement (GetLoopBlock (node.Statement)))
							.WithAdditionalAnnotations (this.isLoop);

			this.loopLevel--;

			return statement;
		}

		public override SyntaxNode VisitForEachStatement (ForEachStatementSyntax node)
		{
			this.loopLevel++;

			var statement = base.VisitForEachStatement (node
								.WithStatement (GetLoopBlock (node.Statement)))
							.WithAdditionalAnnotations (this.isLoop);

			this.loopLevel--;

			return statement;
		}

		public override SyntaxNode VisitForStatement (ForStatementSyntax node)
		{
			this.loopLevel++;

			var statement = base.VisitForStatement (node
								.WithStatement (GetLoopBlock (node.Statement)))
							.WithAdditionalAnnotations (this.isLoop);

			this.loopLevel--;

			return statement;
		}

		public override SyntaxNode VisitDoStatement (DoStatementSyntax node)
		{
			this.loopLevel++;

			var statement = base.VisitDoStatement (node
								.WithStatement (GetLoopBlock (node.Statement)))
							.WithAdditionalAnnotations (this.isLoop);

			this.loopLevel--;

			return statement;
		}

		private int id, loopLevel;
		private readonly Queue<int> blockIds = new Queue<int>();
		private readonly SyntaxAnnotation isLoop = new SyntaxAnnotation();

		private string GetIdCommentString (int cid)
		{
			return "/*_" + cid + "_*/";
		}

		private SyntaxTrivia GetIdComment()
		{
			return GetIdComment (this.id++);
		}	

		private SyntaxTrivia GetIdComment (int cid)
		{
			return Syntax.Comment (GetIdCommentString (cid));
		}

		private BlockSyntax GetLoopBlock (StatementSyntax statement)
		{
			this.blockIds.Enqueue (this.id++);
			this.id += 2;

			if (statement is BlockSyntax)
				return (BlockSyntax)statement;
			
			return Syntax.Block (Syntax.List (statement));
		}
	}
}