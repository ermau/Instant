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

using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Instant.Operations;

namespace Instant.Standalone.Adorners
{
	public class LoopAdorner
		: AdornerBase
	{
		public LoopAdorner (TextBox adornedElement, Loop loop)
			: base (adornedElement)
		{
			this.children = new VisualCollection (this);
			this.children.Add (this.border);

			this.iterations = loop.Operations.OfType<LoopIteration>().ToArray();

			var grid = new Grid();
			grid.ColumnDefinitions.Add (new ColumnDefinition());
			grid.ColumnDefinitions.Add (new ColumnDefinition());
			grid.ColumnDefinitions.Add (new ColumnDefinition());
			grid.Background = Brushes.White;

			this.previous = new Button();
			this.previous.Content = "<";
			this.previous.Background = Brushes.Transparent;
			this.previous.Click += OnPreviousClick;
			
			Grid.SetColumn (this.previous, 0);
			grid.Children.Add (this.previous);

			this.text = new TextBlock();
			this.text.FontSize = adornedElement.FontSize * 0.8;
			this.text.Padding = new Thickness (5, 0, 5, 0);
			
			Grid.SetColumn (this.text, 1);
			grid.Children.Add (this.text);

			this.next = new Button();
			this.next.Content = ">";
			this.next.Background = Brushes.Transparent;
			this.next.Click += OnNextClick;

			Grid.SetColumn (this.next, 2);
			grid.Children.Add (this.next);
			
			this.border.Child = grid;

			Update();
		}

		public event EventHandler IterationChanged;

		public int CurrentIteration
		{
			get { return this.currentIteration; }
			set
			{
				if (value == this.currentIteration)
					return;

				if (value >= this.iterations.Length)
					throw new ArgumentOutOfRangeException();
				if (value < 0)
					throw new ArgumentOutOfRangeException();

				this.currentIteration = value;
				Update();
				OnIterationChanged (EventArgs.Empty);
			}
		}

		public LoopIteration[] Iterations
		{
			get { return this.iterations; }
		}

		protected override FrameworkElement Element
		{
			get { return this.border; }
		}

		private readonly VisualCollection children;

		private int currentIteration;
		private readonly LoopIteration[] iterations;

		private readonly TextBlock text;
		private readonly Button previous, next;

		private readonly Border border = new Border
		{
			BorderBrush = new SolidColorBrush (Color.FromRgb (0, 122, 204)),
			BorderThickness = new Thickness (1),
			Padding = new Thickness (2, 0, 2, 0)
		};

		private void OnNextClick (object sender, RoutedEventArgs e)
		{
			this.CurrentIteration++;
		}

		private void OnPreviousClick (object sender, RoutedEventArgs e)
		{
			this.CurrentIteration--;
		}

		private void Update()
		{
			this.text.Text = this.currentIteration.ToString();

			this.previous.IsEnabled = true;
			this.next.IsEnabled = true;

			if (this.currentIteration == 0)
				this.previous.IsEnabled = false;
			if (this.currentIteration + 1 == this.iterations.Length)
				this.next.IsEnabled = false;
		}

		private void OnIterationChanged(EventArgs e)
		{
			EventHandler handler = this.IterationChanged;
			if (handler != null)
				handler(this, e);
		}
	}
}
