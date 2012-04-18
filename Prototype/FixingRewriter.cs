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

using System.Linq;
using Roslyn.Compilers.CSharp;

namespace LiveCSharp
{
	public class FixingRewriter
		: SyntaxRewriter
	{
		protected override SyntaxNode VisitWhileStatement (WhileStatementSyntax node)
		{
			if (!node.DescendentNodes().OfType<BlockSyntax>().Any())
				node = node.Update (node.WhileKeyword, node.OpenParenToken, node.Condition, node.CloseParenToken, Syntax.Block (statements: node.Statement));

			return base.VisitWhileStatement (node);
		}

		protected override SyntaxNode VisitForStatement (ForStatementSyntax node)
		{
			if (!node.DescendentNodes().OfType<BlockSyntax>().Any())
			{
				node = node.Update (node.ForKeyword, node.OpenParenToken, node.DeclarationOpt,
									node.Initializers, node.FirstSemicolonToken, node.ConditionOpt,
									node.SecondSemicolonToken, node.Incrementors, node.CloseParenToken,
									Syntax.Block (statements: node.Statement));
			}

			return base.VisitForStatement (node);
		}

		protected override SyntaxNode VisitForEachStatement (ForEachStatementSyntax node)
		{
			if (!node.DescendentNodes().OfType<BlockSyntax>().Any())
			{
				node = node.Update (node.ForEachKeyword, node.OpenParenToken, node.Type,
									node.Identifier, node.InKeyword, node.Expression, node.CloseParenToken,
									Syntax.Block (statements: node.Statement));
			}

			return base.VisitForEachStatement (node);
		}

		protected override SyntaxNode VisitDoStatement (DoStatementSyntax node)
		{
			if (!node.DescendentNodes().OfType<BlockSyntax>().Any())
			{
				node = node.Update (node.DoKeyword, Syntax.Block (statements: node.Statement), node.WhileKeyword,
									node.OpenParenToken, node.Condition, node.CloseParenToken, node.SemicolonToken);
			}

			return base.VisitDoStatement (node);
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
	}
}