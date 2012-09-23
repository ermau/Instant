//
// OperationViewModel.cs
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
using Instant.Operations;

namespace Instant.VisualStudio.ViewModels
{
	public abstract class OperationViewModel
		: ViewModel
	{
		public Operation Operation
		{
			get { return this.operation; }
			set
			{
				if (this.operation == value)
					return;

				this.operation = value;
				OnOperationChanged();
				OnPropertyChanged ("Operation");
			}
		}

		private Operation operation;
		private Loop loop;

		protected virtual void OnOperationChanged()
		{
		}
	}
}