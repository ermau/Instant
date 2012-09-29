﻿//
// ReturnValue.cs
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

namespace Instant.Operations
{
	[DebuggerDisplay ("return {Value}")]
	public class ReturnValue
		: Operation
	{
		public ReturnValue (int id, string value)
			: base (id)
		{
			Value = value;
		}

		public string Value
		{
			get;
			private set;
		}
	}
}