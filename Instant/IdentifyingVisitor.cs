//
// IdentifyingVisitor.cs
//
// Copyright 2012 Eric Maupin
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using ICSharpCode.NRefactory.CSharp;

namespace Instant
{
	public class IdentifyingVisitor
		: DepthFirstAstVisitor
	{
		public IDictionary<int, int> LineMap
		{
			get { return this.lineMap; }
		}

		public override void VisitMethodDeclaration (MethodDeclaration methodDeclaration)
		{
			base.VisitMethodDeclaration (methodDeclaration);

			var body = methodDeclaration.Body;
			if (!body.HasChildren)
				return;

			this.lineMap[this.id++] = methodDeclaration.StartLocation.Line;
		}

		public override void VisitVariableInitializer (VariableInitializer initializer)
		{
			base.VisitVariableInitializer (initializer);

			Identifier identifier = initializer.FindIdentifier();
			if (identifier == null)
				return;

			this.lineMap[this.id++] = initializer.StartLocation.Line;
		}

		public override void VisitAssignmentExpression (AssignmentExpression expression)
		{
			base.VisitAssignmentExpression (expression);

			Identifier identifier = expression.FindIdentifier();
			if (identifier == null)
				return;

			switch (expression.Operator)
			{
				case AssignmentOperatorType.BitwiseOr:
				case AssignmentOperatorType.BitwiseAnd:
				case AssignmentOperatorType.ExclusiveOr:
				case AssignmentOperatorType.Add:
				case AssignmentOperatorType.Subtract:
				case AssignmentOperatorType.Divide:
				case AssignmentOperatorType.Modulus:
				case AssignmentOperatorType.Multiply:
				case AssignmentOperatorType.ShiftLeft:
				case AssignmentOperatorType.ShiftRight:
				case AssignmentOperatorType.Assign:
					this.lineMap[this.id++] = expression.StartLocation.Line;
					break;
			}
		}

		public override void VisitUnaryOperatorExpression (UnaryOperatorExpression unary)
		{
			base.VisitUnaryOperatorExpression (unary);

			Identifier identifier = unary.FindIdentifier();
			if (identifier == null)
				return;

			switch (unary.Operator)
			{
				case UnaryOperatorType.Increment:
				case UnaryOperatorType.Decrement:
				case UnaryOperatorType.PostDecrement:
				case UnaryOperatorType.PostIncrement:
					this.lineMap[this.id++] = unary.StartLocation.Line;
					break;
			}
		}

		public override void VisitReturnStatement (ReturnStatement returnStatement)
		{
			base.VisitReturnStatement (returnStatement);

			if (returnStatement.Expression == Expression.Null)
				return;

			int nodeId = this.id++;
			this.lineMap[nodeId] = returnStatement.StartLocation.Line;
		}

		public override void VisitForStatement (ForStatement forStatement)
		{
			this.loopLevel++;

			forStatement.EmbeddedStatement = GetLoopBlock (forStatement.EmbeddedStatement);
			base.VisitForStatement (forStatement);

			this.loopLevel--;
		}

		public override void VisitForeachStatement (ForeachStatement foreachStatement)
		{
			this.loopLevel++;

			foreachStatement.EmbeddedStatement = GetLoopBlock (foreachStatement.EmbeddedStatement);
			base.VisitForeachStatement (foreachStatement);

			this.loopLevel--;
		}

		public override void VisitWhileStatement (WhileStatement whileStatement)
		{
			this.loopLevel++;

			whileStatement.EmbeddedStatement = GetLoopBlock (whileStatement.EmbeddedStatement);
			base.VisitWhileStatement (whileStatement);

			this.loopLevel--;
		}

		public override void VisitDoWhileStatement (DoWhileStatement doWhileStatement)
		{
			this.loopLevel++;

			doWhileStatement.EmbeddedStatement = GetLoopBlock (doWhileStatement.EmbeddedStatement);
			base.VisitDoWhileStatement (doWhileStatement);

			this.loopLevel--;
		}

		public override void VisitIfElseStatement (IfElseStatement ifElseStatement)
		{
			if (!(ifElseStatement.TrueStatement is BlockStatement))
				ifElseStatement.TrueStatement = new BlockStatement { ifElseStatement.TrueStatement.Clone() };
			if (!(ifElseStatement.FalseStatement is BlockStatement) && !(ifElseStatement.FalseStatement is IfElseStatement))
				ifElseStatement.FalseStatement = new BlockStatement { ifElseStatement.FalseStatement.Clone() };

			base.VisitIfElseStatement (ifElseStatement);
		}

		public override void VisitBlockStatement (BlockStatement blockStatement)
		{
			base.VisitBlockStatement (blockStatement);

			List<Statement> statements = new List<Statement>();

			foreach (Statement statement in blockStatement.Statements)
			{
				bool loop = GetIsLoopStatement (statement);
				if (loop)
				{
					int nodeId = this.blockIds.Dequeue();
					this.lineMap[nodeId] = statement.StartLocation.Line;
				}

				if (this.loopLevel > 0)
				{
					if (statement is ContinueStatement || statement is BreakStatement)
						this.id++;
				}

				statements.Add (statement.Clone());

				if (loop)
					this.id++;
			}

			blockStatement.Statements.Clear();
			blockStatement.Statements.AddRange (statements);
		}

		protected int id;
		protected int loopLevel;
		protected readonly Queue<int> blockIds = new Queue<int>();
		protected readonly Dictionary<int, int> lineMap = new Dictionary<int, int>();
		
		private bool GetIsLoopStatement (Statement statement)
		{
			return	statement is ForStatement
					|| statement is ForeachStatement
					|| statement is WhileStatement
					|| statement is DoWhileStatement;
		}

		private Statement GetLoopBlock (Statement statement)
		{
			this.blockIds.Enqueue (this.id++);

			BlockStatement block = statement as BlockStatement;
			if (block == null)
			{
				if (statement == Statement.Null)
					block = new BlockStatement();
				else
					block = new BlockStatement { statement.Clone() };
			}

			this.id += 2;

			return block;
		}
	}
}