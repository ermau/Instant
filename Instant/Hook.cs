//
// Hook.cs
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Instant.Operations;

namespace Instant
{
	public static class Hook
	{
		public static int CreateSubmission (CancellationToken cancelToken)
		{
			int id = Interlocked.Increment (ref currentSubmission);
			CancelToken = cancelToken;

			lock (SubmissionLock)
			{
				values.Clear();
				operations.Clear();
			}

			loopLevel = 0;
			iteration = 0;

			return id;
		}

		public static IDictionary<int, MethodCall> RootCalls
		{
			get
			{
				if (Operations.Count == 0)
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
		}

		public static CancellationToken CancelToken;

		public static void BeginLoop (int submissionId, int id)
		{
			if (submissionId < currentSubmission)
				throw new OperationCanceledException();

			loopLevel++;
			Operations.Push (new Loop (id));
		}

		public static void BeginInsideLoop (int submissionId, int id)
		{
			if (submissionId < currentSubmission)
				throw new OperationCanceledException();

			Operations.Push (new LoopIteration (id));

			if (CancelToken.IsCancellationRequested)
			{
				EndLoop (submissionId, id);
				throw new OperationCanceledException (CancelToken);
			}
		}

		private static readonly SkippedIteration Skipped = new SkippedIteration();

		public static void EndInsideLoop (int submissionId, int id)
		{
			if (submissionId < currentSubmission)
				throw new OperationCanceledException();

			LoopIteration iter = Operations.Pop() as LoopIteration;
			if (iter == null)
				throw new InvalidOperationException();

			AddOperation (iter);

			iteration++;

			foreach (var kvp in Values.Where (kvp => kvp.Value.Count != iteration))
				kvp.Value.Add (Skipped);
			
			if (CancelToken.IsCancellationRequested)
			{
				EndLoop (submissionId, id);
				throw new OperationCanceledException (CancelToken);
			}

			if (IsLoggingInifiniteLoop())
			{
				EndLoop (submissionId, id);
				/*MethodCall call = Operations.Pop() as MethodCall;
				if (call != null)
					AddOperation (call);*/

				throw new OperationCanceledException ("Infinite loop detected", CancelToken);
			}
		}

		public static void EndLoop (int submissionId, int id)
		{
			if (submissionId < currentSubmission)
				throw new OperationCanceledException();

			string[] names = Values.Keys.ToArray();
			if (names.Length > 0)
			{
				/*List<object>[] valueLists = new List<object>[names.Length];
				for (int i = 0; i < names.Length; ++i)
					valueLists[i] = this.Values[names[i]];

				int lcount = valueLists.Max (l => l.Count);
				for (int i = 0; i < lcount; ++i)
				{
					if (i == MaximumLoops && i != 0)
						break;

					object[] vs = new object[valueLists.Length];
					for (int n = 0; n < valueLists.Length; ++n)
					{
						List<object> nvs = valueLists[n];
						if (nvs.Count > i)
							vs[n] = Display.Object (valueLists[n][i]);
					}
				}*/

				Values.Clear();
			}

			loopLevel--;

			Loop loop = Operations.Pop() as Loop;
			AddOperation (loop);
		}

		public static void LogReturn (int submissionId, int id)
		{
			if (submissionId < currentSubmission)
				throw new OperationCanceledException();

			while (loopLevel > 0)
			{
				EndInsideLoop (submissionId, id);
				EndLoop (submissionId, id);
			}

			MethodCall call = Operations.Pop() as MethodCall;
			if (call == null)
				throw new InvalidOperationException();

			AddOperation (call);
		}

		public static T LogReturn<T> (int submissionId, int id, T value)
		{
			if (submissionId < currentSubmission)
				throw new OperationCanceledException();

			AddOperation (new ReturnValue (id, Display.Object (value)));

			while (loopLevel > 0)
			{
				EndInsideLoop (submissionId, id);
				EndLoop (submissionId, id);
			}

			if (Operations.Count == 1)
				return value; // top level method

			MethodCall call = Operations.Pop() as MethodCall;
			if (call == null)
				throw new InvalidOperationException();

			AddOperation (call);

			return value;
		}

		public static T LogObject<T> (int submissionId, int id, string name, T value)
		{
			if (submissionId < currentSubmission)
				throw new OperationCanceledException();

			AddOperation (new StateChange (id, name, Display.Object (value)));

			return value;
		}
		
		public static T LogPostfix<T> (int submissionId, int id, T expression, string name, T newValue)
		{
			LogObject (submissionId, id, name, newValue);

			return expression;
		}

		public static void LogEnterMethod (int submissionId, int id, string name, params StateChange[] arguments)
		{
			if (submissionId < currentSubmission)
				throw new OperationCanceledException();

			Operations.Push (new MethodCall (id, name, arguments));
		}

		[ThreadStatic] private static int loopLevel;
		[ThreadStatic] private static int iteration;

		private static readonly object SubmissionLock = new object();

		private static readonly ConcurrentDictionary<int,Dictionary<string, List<object>>> values = new ConcurrentDictionary<int, Dictionary<string, List<object>>>();
		private static readonly ConcurrentDictionary<int,Stack<OperationContainer>> operations = new ConcurrentDictionary<int, Stack<OperationContainer>>();
		private static int currentSubmission;
		

		private static Stack<OperationContainer> Operations
		{
			get { return operations.GetOrAdd (Thread.CurrentThread.ManagedThreadId, id => new Stack<OperationContainer>()); }
		}

		private static Dictionary<string, List<object>> Values
		{
			get { return values.GetOrAdd (Thread.CurrentThread.ManagedThreadId, id => new Dictionary<string, List<object>>()); }
		}

		private static void AddOperation (Operation operation)
		{
			lock (SubmissionLock)
			{
				var ops = Operations;
				if (ops.Count == 0)
					return;

				ops.Peek().Operations.Add (operation);
			}
		}

		private static bool IsLoggingInifiniteLoop()
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

		private static bool GetIsLoggingInfiniteLoop (Loop loop, Dictionary<string, StateChange> changes)
		{
			if (changes == null)
				changes = new Dictionary<string, StateChange>();

			if (loop.Operations.OfType<LoopIteration>().Count() <= 1)
				return false;

			foreach (Operation operation in loop.Operations)
			{
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

		private static bool GetDidChange (Dictionary<string, StateChange> changes, StateChange change)
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