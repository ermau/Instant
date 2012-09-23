//
// ObjectLogger.cs
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
using System.Threading;

namespace LiveCSharp
{
	public class ObjectLogger
	{
		public ObjectLogger (CancellationToken cancelToken)
		{
			CancelToken = cancelToken;
		}

		public MethodCall RootCall
		{
			get
			{
				if (this.operations.Count == 0)
					return null;

				return this.operations.Peek() as MethodCall;
			}
		}

		public int MaximumLoops
		{
			get;
			set;
		}

		public readonly CancellationToken CancelToken;

		private int loopLevel;
		public void BeginLoop (int id)
		{
			this.loopLevel++;
			this.operations.Push (new Loop (id));
		}

		public void BeginInsideLoop (int id)
		{
			this.operations.Push (new LoopIteration (id));

			if (CancelToken.IsCancellationRequested)
			{
				EndLoop (id);
				throw new OperationCanceledException (CancelToken);
			}
		}

		private static readonly SkippedIteration Skipped = new SkippedIteration();
		private int iteration;
		public void EndInsideLoop (int id)
		{
			LoopIteration iter = this.operations.Pop() as LoopIteration;
			if (iter == null)
				throw new InvalidOperationException();

			AddOperation (iter);

			this.iteration++;

			foreach (var kvp in this.values.Where (kvp => kvp.Value.Count != this.iteration))
				kvp.Value.Add (Skipped);
			
			if (IsLoggingInifiniteLoop())
			{
				EndLoop (id);
				/*MethodCall call = this.operations.Pop() as MethodCall;
				if (call != null)
					AddOperation (call);*/

				throw new OperationCanceledException ("Infinite loop detected", CancelToken);
			}
		}

		public void EndLoop (int id)
		{
			string[] names = this.values.Keys.ToArray();
			if (names.Length > 0)
			{
				/*List<object>[] valueLists = new List<object>[names.Length];
				for (int i = 0; i < names.Length; ++i)
					valueLists[i] = this.values[names[i]];

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

				this.values.Clear();
			}

			this.loopLevel--;

			Loop loop = this.operations.Pop() as Loop;
			AddOperation (loop);
		}

		public void LogReturn (int id)
		{
			while (this.loopLevel > 0)
				EndLoop (id);

			MethodCall call = this.operations.Pop() as MethodCall;
			if (call == null)
				throw new InvalidOperationException();

			AddOperation (call);
		}

		public T LogReturn<T> (int id, T value)
		{
			//AddOperation (new ReturnValue (id, Display.Object (value)));

			while (this.loopLevel > 0)
				EndLoop (id);

			//AddOperation (new ReturnValue (id, Display.Object (value)));

			if (this.operations.Count == 1)
				return value; // top level method

			MethodCall call = this.operations.Pop() as MethodCall;
			if (call == null)
				throw new InvalidOperationException();

			AddOperation (call);

			return value;
		}

		public T LogObject<T> (int id, string name, T value)
		{
			if (this.loopLevel > 0)
			{
				List<object> vs;
				if (!this.values.TryGetValue (name, out vs))
				{
					this.values [name] = vs = new List<object>();
					vs.AddRange (Enumerable.Repeat (Skipped, this.iteration));
				}

				vs.Add (value);
			}
			//else
			//{
				AddOperation (new StateChange (id, name, Display.Object (value)));
			//}

			return value;
		}
		
		public T LogPostfix<T> (int id, T expression, string name, T newValue)
		{
			LogObject (id, name, newValue);

			return expression;
		}

		public void LogEnterMethod (int id, string name, params StateChange[] arguments)
		{
			operations.Push (new MethodCall (id, name, arguments));
		}
		
		private readonly Dictionary<string, List<object>> values = new Dictionary<string, List<object>>();
		private readonly Stack<OperationContainer> operations = new Stack<OperationContainer>();

		private void AddOperation (Operation operation)
		{
			this.operations.Peek().Operations.Add (operation);
		}

		private bool IsLoggingInifiniteLoop()
		{
			bool multipleUnchangedValues = (this.values.Count == 0);
			
			foreach (List<object> history in this.values.Values)
			{
				if (history.Count > 1)
				{
					multipleUnchangedValues = true;

					object hvalue = history [history.Count - 1];
					if (hvalue != Skipped && !hvalue.Equals (history[history.Count - 2]))
						return false;
				}
			}

			return multipleUnchangedValues;
		}
	}
}