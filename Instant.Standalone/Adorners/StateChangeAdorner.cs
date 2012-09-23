//
// MainWindowViewModel.cs
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

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Instant.Operations;

namespace Instant.Standalone.Adorners
{
	public class StateChangeAdorner
		: AdornerBase
	{
		public StateChangeAdorner (TextBox adornedElement, StateChange change)
			: base (adornedElement)
		{
			this.text.FontSize = adornedElement.FontSize * 0.80;
			this.text.Text = change.Variable + " = " + change.Value;
			this.border.Child = this.text;
		}

		protected override FrameworkElement Element
		{
			get { return this.border; }
		}

		private readonly Border border = new Border
		{
			BorderBrush = new SolidColorBrush (Color.FromRgb (0, 122, 204)),
			BorderThickness = new Thickness (1),
			Padding = new Thickness (2, 0, 2, 0)
		};

		private readonly TextBlock text = new TextBlock();
	}
}
