//
// FixingRewriter.cs
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

using Roslyn.Compilers.CSharp;

namespace Instant
{
	public class FixingRewriter
		: SyntaxRewriter
	{
		/*public override SyntaxNode VisitMethodDeclaration (MethodDeclarationSyntax node)
		{
			var newNode = base.VisitMethodDeclaration (node);
			if ((node = newNode as MethodDeclarationSyntax) == null)
				return newNode;

			if (node.Body != null && node.Body.CloseBraceToken.Value == null)
				node = node.WithBody (node.Body.WithCloseBraceToken (Syntax.ParseToken ("}")));

			if (node.Body == null)
				node = node.WithBody (Syntax.Block());

			if (node.ReturnType != null && node.ReturnType.PlainName != "void")
				node = node.WithBody (node.Body.AddStatements (Syntax.ParseStatement ("return default(" + node.ReturnType.PlainName + ");")));

			return node;
		}*/

		public override SyntaxNode VisitIfStatement (IfStatementSyntax node)
		{
			var based = base.VisitIfStatement (node);
			if ((node = based as IfStatementSyntax) == null)
				return based;

			if (node.Else != null)
				node = node.WithElse (node.Else.WithStatement (GetBlock (node.Else.Statement)));

			return node.WithStatement (GetBlock (node.Statement));
		}

		public override SyntaxNode VisitForStatement (ForStatementSyntax node)
		{
			var based = base.VisitForStatement (node);
			if ((node = based as ForStatementSyntax) == null)
				return based;

			return node.WithStatement (GetBlock (node.Statement));
		}

		public override SyntaxNode VisitForEachStatement (ForEachStatementSyntax node)
		{
			var based = base.VisitForEachStatement (node);
			if ((node = based as ForEachStatementSyntax) == null)
				return based;

			return node.WithStatement (GetBlock (node.Statement));
		}

		public override SyntaxNode VisitWhileStatement (WhileStatementSyntax node)
		{
			var based = base.VisitWhileStatement (node);
			if ((node = based as WhileStatementSyntax) == null)
				return based;

			return node.WithStatement (GetBlock (node.Statement));
		}

		public override SyntaxNode VisitDoStatement (DoStatementSyntax node)
		{
			var based = base.VisitDoStatement (node);
			if ((node = based as DoStatementSyntax) == null)
				return based;

			return node.WithStatement (GetBlock (node.Statement));
		}

		private StatementSyntax GetBlock (StatementSyntax syntax)
		{
			var block = syntax as BlockSyntax;
			if (block != null)
				return block;

			return Syntax.Block (syntax);
		}
	}
}
