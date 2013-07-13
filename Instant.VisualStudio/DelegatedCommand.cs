//
// DelegatedCommand.cs
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
using System.Windows.Input;

namespace Instant
{
	public class DelegatedCommand
		: ICommand
	{
		public DelegatedCommand (Action<object> execute, Func<object, bool> canExecute)
		{
			if (execute == null)
				throw new ArgumentNullException ("execute");
			if (canExecute == null)
				throw new ArgumentNullException ("canExecute");

			this.execute = execute;
			this.canExecute = canExecute;
		}

		public event EventHandler CanExecuteChanged;

		public void ChangeCanExecute()
		{
			var changed = CanExecuteChanged;
			if (changed != null)
				changed (this, EventArgs.Empty);
		}

		public bool CanExecute (object parameter)
		{
			return this.canExecute (parameter);
		}

		public void Execute (object parameter)
		{
			this.execute (parameter);
		}

		private readonly Action<object> execute;
		private readonly Func<object, bool> canExecute;
	}
}
