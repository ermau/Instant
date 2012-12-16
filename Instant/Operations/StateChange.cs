//
// StateChange.cs
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

using System.Diagnostics;

namespace Instant
{
	[DebuggerDisplay ("[{Id}] {Variable} = {Value}")]
	public class StateChange
		: Operation
	{
		public StateChange (int id, string variable, object value)
			: base (id)
		{
			Variable = variable;
			Value = Display.Object (value);
		}

		public StateChange (int id, string variable, string value)
			: base (id)
		{
			Variable = variable;
			Value = value;
		}

		public string Variable
		{
			get;
			private set;
		}

		public string Value
		{
			get;
			private set;
		}
	}
}
