//
// IInstrumentationSink.cs
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

using Instant.Operations;

namespace Instant
{
	public interface IInstrumentationSink
	{
		void BeginLoop (int id);
		void BeginInsideLoop (int id);
		void EndInsideLoop (int id);
		void EndLoop (int id);
		
		void LogReturn (int id);
		void LogReturn (int id, string value);

		void LogVariableChange (int id, string variableName, string value);

		void LogEnterMethod (int id, string name, params StateChange[] arguments);
	}
}