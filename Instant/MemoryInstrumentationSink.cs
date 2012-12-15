//
// MemoryInstrumentationSink.cs
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Instant.Operations;

namespace Instant
{
	public class MemoryInstrumentationSink
		: MarshalByRefObject, IInstrumentationSink
	{
		public MemoryInstrumentationSink (Func<bool> getIsCanceled)
		{
			if (getIsCanceled == null)
				throw new ArgumentNullException ("getIsCanceled");

			this.getIsCanceled = getIsCanceled;
		}

		public IDictionary<int, MethodCall> GetRootCalls()
		{
			if (this.operations.Count == 0)
				return null;

			Dictionary<int, MethodCall> methods = new Dictionary<int, MethodCall>();
			foreach (var kvp in operations)
			{
				if (kvp.Value.Count == 0)
					continue;

				OperationContainer container = kvp.Value.Pop();
				while (!(container is MethodCall) && kvp.Value.Count > 0)
				{
					var c = container;
					container = kvp.Value.Pop();
					container.Operations.Add (c);
				}

				var method = container as MethodCall;
				if (method != null)
					methods.Add (kvp.Key, method);
			}

			return methods;
		}

		public void BeginLoop (int id)
		{
			this.loopLevel++;
			Operations.Push (new Loop (id));
		}

		public void BeginInsideLoop (int id)
		{
			Operations.Push (new LoopIteration (id));
		}

		public void EndInsideLoop (int id)
		{
			LoopIteration iter = Operations.Pop() as LoopIteration;
			if (iter == null)
				throw new InvalidOperationException();

			AddOperation (iter);

			if (IsLoggingInifiniteLoop())
			{
				EndLoop (id);
				throw new OperationCanceledException ("Infinite loop detected");
			}
		}

		public void EndLoop (int id)
		{
			this.loopLevel--;

			Loop loop = Operations.Pop() as Loop;
			if (loop == null)
				throw new InvalidOperationException();

			AddOperation (loop);
		}

		public void LogReturn (int id)
		{
			while (loopLevel > 0)
			{
				EndInsideLoop (id);
				EndLoop (id);
			}

			MethodCall call = Operations.Pop() as MethodCall;
			if (call == null)
				throw new InvalidOperationException();

			AddOperation (call);
		}

		public void LogReturn (int id, string value)
		{
			AddOperation (new ReturnValue (id, value));

			while (loopLevel > 0)
			{
				EndInsideLoop (id);
				EndLoop (id);
			}

			if (Operations.Count == 1)
				return; // top level method

			MethodCall call = Operations.Pop() as MethodCall;
			if (call == null)
				throw new InvalidOperationException();

			AddOperation (call);
		}

		public void LogVariableChange (int id, string variableName, string value)
		{
			AddOperation (new StateChange (id, variableName, value));
		}

		public void LogEnterMethod (int id, string name, params StateChange[] arguments)
		{
			Operations.Push (new MethodCall (id, name, arguments));
		}
		
		private int loopLevel;
		private readonly Func<bool> getIsCanceled;
		private readonly ConcurrentDictionary<int,Stack<OperationContainer>> operations = new ConcurrentDictionary<int, Stack<OperationContainer>>();		

		private Stack<OperationContainer> Operations
		{
			get { return this.operations.GetOrAdd (Thread.CurrentThread.ManagedThreadId, id => new Stack<OperationContainer>()); }
		}

		private void AddOperation (Operation operation)
		{
			var ops = Operations;
			if (ops.Count == 0)
				return;

			ops.Peek().Operations.Add (operation);
		}

		private bool IsLoggingInifiniteLoop()
		{
			OperationContainer container = Operations.Peek();

			var loop = container as Loop;
			if (loop != null)
				return GetIsLoggingInfiniteLoop (loop, null);

			foreach (Loop l in container.Operations.OfType<Loop>())
			{
				if (!GetIsLoggingInfiniteLoop (l, null))
					return false;
			}

			return true;
		}

		private bool GetIsLoggingInfiniteLoop (Loop loop, Dictionary<string, StateChange> changes)
		{
			if (changes == null)
				changes = new Dictionary<string, StateChange>();

			if (loop.Operations.OfType<LoopIteration>().Take(2).Count() <= 1)
				return false;

			foreach (Operation operation in loop.Operations)
			{
				if (this.getIsCanceled())
					throw new OperationCanceledException();

				var change = operation as StateChange;
				if (change != null)
				{
					if (GetDidChange (changes, change))
						return false;

					continue;
				}

				var iter = operation as LoopIteration;
				if (iter != null)
				{
					foreach (Operation iterOp in iter.Operations)
					{
						if (this.getIsCanceled())
							throw new OperationCanceledException();

						if (iterOp is Loop)
						{
							if (!GetIsLoggingInfiniteLoop ((Loop)iterOp, null))
								return false;
						}

						if (iterOp is MethodCall || iterOp is ReturnValue)
							return false;

						if (iterOp is StateChange)
						{
							if (GetDidChange (changes, (StateChange)iterOp))
								return false;
						}
					}
				}
			}

			return true;
		}

		private bool GetDidChange (Dictionary<string, StateChange> changes, StateChange change)
		{
			StateChange previous;
			if (!changes.TryGetValue (change.Variable, out previous))
			{
				changes[change.Variable] = change;
				return false;
			}

			return !Equals (previous.Value, change.Value);
		}
	}
}