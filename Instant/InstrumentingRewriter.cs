//
// InstrumentingRewriter.cs
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
using ICSharpCode.NRefactory;
using ICSharpCode.NRefactory.CSharp;

namespace Instant
{
	public class InstrumentingRewriter
		: DepthFirstAstVisitor
	{
		public InstrumentingRewriter (Submission submission)
		{
			if (submission == null)
				throw new ArgumentNullException ("submission");

			this.submission = submission;
		}

		public override void VisitMethodDeclaration (MethodDeclaration methodDeclaration)
		{
			base.VisitMethodDeclaration (methodDeclaration);

			var body = methodDeclaration.Body;
			if (!body.HasChildren)
				return;

			var hook =	GetHookExpression ("LogEnterMethod",
							new PrimitiveExpression (this.submission.SubmissionId),
							new PrimitiveExpression (this.id++),
							new PrimitiveExpression (methodDeclaration.Name));

			body.InsertChildBefore (body.FirstChild, new ExpressionStatement (hook), BlockStatement.StatementRole);
		}

		public override void VisitVariableInitializer (VariableInitializer initializer)
		{
			base.VisitVariableInitializer (initializer);

			Identifier identifier = FindIdentifier (initializer);
			if (identifier == null)
				return;

			initializer.Initializer = GetAssignmentExpression (identifier, initializer.Initializer);
		}

		private BinaryOperatorType GetComplexAssignOperator (AssignmentOperatorType type)
		{
			switch (type)
			{
				case AssignmentOperatorType.BitwiseOr:
					return BinaryOperatorType.BitwiseOr;
				case AssignmentOperatorType.BitwiseAnd:
					return BinaryOperatorType.BitwiseAnd;
				case AssignmentOperatorType.ExclusiveOr:
					return BinaryOperatorType.ExclusiveOr;
				case AssignmentOperatorType.Add:
					return BinaryOperatorType.Add;
				case AssignmentOperatorType.Subtract:
					return BinaryOperatorType.Subtract;
				case AssignmentOperatorType.Divide:
					return BinaryOperatorType.Divide;
				case AssignmentOperatorType.Modulus:
					return BinaryOperatorType.Modulus;
				case AssignmentOperatorType.Multiply:
					return BinaryOperatorType.Multiply;
				case AssignmentOperatorType.ShiftLeft:
					return BinaryOperatorType.ShiftLeft;
				case AssignmentOperatorType.ShiftRight:
					return BinaryOperatorType.ShiftRight;

				default:
					throw new ArgumentException();
			}
		}

		public override void VisitAssignmentExpression (AssignmentExpression expression)
		{
			base.VisitAssignmentExpression (expression);

			Identifier identifier = FindIdentifier (expression);
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
				{
					BinaryOperatorType op = GetComplexAssignOperator (expression.Operator);
					Expression right = new BinaryOperatorExpression (new IdentifierExpression (identifier.Name), op, expression.Right.Clone());
					
					expression.Operator = AssignmentOperatorType.Assign;
					expression.Right = GetAssignmentExpression (identifier, right);

					break;
				}

				case AssignmentOperatorType.Assign:
					expression.Right = GetAssignmentExpression (identifier, expression.Right);
					break;
			}
		}

		public override void VisitReturnStatement (ReturnStatement returnStatement)
		{
			base.VisitReturnStatement (returnStatement);

			if (returnStatement.Expression == Expression.Null)
				return;

			returnStatement.Expression = GetHookExpression ("LogReturn", GetSubmissionId(), GetId(), returnStatement.Expression.Clone());
		}

		private readonly Submission submission;
		private int id;

		private PrimitiveExpression GetSubmissionId()
		{
			return new PrimitiveExpression (this.submission.SubmissionId);
		}

		private PrimitiveExpression GetId()
		{
			return new PrimitiveExpression (this.id++);
		}

		private Identifier FindIdentifier (AstNode node)
		{
			var ident = node as IdentifierExpression;
			if (ident != null)
				return ident.IdentifierToken;

			var init = node as VariableInitializer;
			if (init != null)
				return init.NameToken;

			var assignment = node as AssignmentExpression;
			if (assignment != null)
				return FindIdentifier (assignment.Left);

			return null;
		}

		private Expression GetHookExpression (string method, params Expression[] parameters)
		{
			return new InvocationExpression (
				new MemberReferenceExpression (
					new MemberReferenceExpression (
						new IdentifierExpression ("Instant"), "Hook"
					),
					method
				),
				parameters
			);
		}

		private Expression GetAssignmentExpression (Identifier identifier, Expression expression)
		{
			return GetHookExpression ("LogObject",
				GetSubmissionId(),
				GetId(),
				new PrimitiveExpression (identifier.Name),
				expression.Clone()
			);
		}
	}
}