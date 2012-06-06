//
// StringObjectLogger.cs
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
	public class StringObjectLogger
	{
		public StringObjectLogger (CancellationToken cancelToken)
		{
			CancelToken = cancelToken;
		}

		public int MaximumLoops
		{
			get;
			set;
		}

		public readonly CancellationToken CancelToken;

		private int loopLevel;
		public void BeginLoop()
		{
			this.loopLevel++;
		}

		public void BeginInsideLoop()
		{
			if (CancelToken.IsCancellationRequested)
			{
				EndLoop();
				throw new OperationCanceledException (CancelToken);
			}
		}

		private static readonly SkippedIteration Skipped = new SkippedIteration();
		private int iteration;
		public void EndInsideLoop()
		{
			this.iteration++;

			foreach (var kvp in this.values.Where (kvp => kvp.Value.Count != this.iteration))
				kvp.Value.Add (Skipped);
			
			if (IsLoggingInifiniteLoop())
			{
				EndLoop();
				throw new OperationCanceledException ("Infinite loop detected", CancelToken);
			}
		}

		public void EndLoop()
		{
			string[] names = this.values.Keys.ToArray();
			if (names.Length == 0)
			{
				this.loopLevel--;
				return;
			}

			StringBuilder fBuilder = new StringBuilder();
			fBuilder.Append ("| ");
			for (int i = 0; i < names.Length; ++i)
			{
				int size = names[i].Length;
				if (size < 5)
					size = 5;

				fBuilder.Append ("{" + i + ",-" + size + "} | ");
			}

			string format = fBuilder.ToString();

			this.builder.AppendLine (String.Format (format, this.values.Keys.Cast<object>().ToArray()));

			List<object>[] valueLists = new List<object>[names.Length];
			for (int i = 0; i < names.Length; ++i)
				valueLists[i] = this.values[names[i]];

			int lcount = valueLists.Max (l => l.Count);
			for (int i = 0; i < lcount; ++i)
			{
				if (i == MaximumLoops && i != 0)
				{
					this.builder.AppendLine (String.Format ("{0:N0} loops not shown", lcount - MaximumLoops));
					break;
				}

				object[] vs = new object[valueLists.Length];
				for (int n = 0; n < valueLists.Length; ++n)
				{
					List<object> nvs = valueLists[n];
					if (nvs.Count > i)
						vs[n] = Display.Object (valueLists[n][i]);
				}

				this.builder.AppendLine (String.Format (format, vs));
			}

			this.values.Clear();

			this.loopLevel--;
		}

		public void LogReturn()
		{
			while (this.loopLevel > 0)
				EndLoop();
		}

		public T LogReturn<T> (T value)
		{
			while (this.loopLevel > 0)
				EndLoop();

			this.builder.Append ("return ");
			this.builder.AppendLine (Display.Object (value));

			return value;
		}

		public T LogObject<T> (string name, T value)
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
			else
			{
				this.builder.Append (name);
				this.builder.Append (" = ");
				this.builder.AppendLine (Display.Object (value));
			}

			return value;
		}
		
		public T LogPostfix<T> (T expression, string name, T newValue)
		{
			LogObject (name, newValue);

			return expression;
		}

		public string Output
		{
			get { return this.builder.ToString(); }
		}

		private readonly StringBuilder builder = new StringBuilder();
		private readonly Dictionary<string, List<object>> values = new Dictionary<string, List<object>>();

		private bool IsLoggingInifiniteLoop()
		{
			bool multipleUnchangedValues = !(this.values.Count > 0);
			
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