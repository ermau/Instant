//
// InstantVisualStudio.cs
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
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Cadenza.Collections;
using EnvDTE;
using Instant.Operations;
using Instant.VisualStudio.ViewModels;
using Instant.VisualStudio.Views;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Roslyn.Compilers;
using Roslyn.Compilers.CSharp;
using Task = System.Threading.Tasks.Task;

namespace Instant.VisualStudio
{
    public class InstantVisualStudio
    {
		public InstantVisualStudio (IWpfTextView view)
        {
            this.view = view;
            this.layer = view.GetAdornmentLayer("Instant.VisualStudio");

            //Listen to any event that changes the layout (text changes, scrolling, etc)
            this.view.LayoutChanged += OnLayoutChanged;

	        this.dispatcher = Dispatcher.CurrentDispatcher;
        }

		private readonly IAdornmentLayer layer;
		private readonly IWpfTextView view;

	    private CancellationTokenSource cancelSource;
	    private readonly Dispatcher dispatcher;

	    private ExecutionContext context;

		private class ExecutionContext
		{
			public string MethodSignature;
			public ITrackingSpan Span;
			public ITextVersion Version;
			public string TestCode;
			public IDictionary<int, MethodCall> LastData;
		}

		private readonly BidirectionalDictionary<ITrackingSpan, FrameworkElement> adorners = new BidirectionalDictionary<ITrackingSpan, FrameworkElement>();
	    private readonly _DTE dte = (_DTE)Package.GetGlobalService (typeof (DTE));

	    /// <summary>
        /// On layout change add the adornment to any reformatted lines
        /// </summary>
        private void OnLayoutChanged (object sender, TextViewLayoutChangedEventArgs e)
	    {
			var cancel = GetCancelSource();

			if (this.context != null)
			{
				Span currentSpan = this.context.Span.GetSpan (e.NewSnapshot);
				if (e.NewOrReformattedSpans.Any (s => currentSpan.Contains (s)))
				{
					if (this.context.Version != e.NewSnapshot.Version)
					{
						this.context.Version = e.NewSnapshot.Version;
						Execute (cancel.Token);
					}
					else
						AdornCode (cancel.Token);
				}
			}

		    LayoutButtons (e.NewSnapshot, cancel.Token);
	    }

		// We can likely set up a cache for all these, just need to ensure they're
		// cleared when the user changes them.
		private EnvDTE.Properties FontsAndColors
		{
			get { return this.dte.Properties["FontsAndColors", "TextEditor"]; }
		}

		private FontsAndColorsItems FontsAndColorsItems
		{
			get { return ((FontsAndColorsItems)FontsAndColors.Item ("FontsAndColorsItems").Object); }
		}

		private float FontSize
		{
			get { return (float)FontsAndColors.Item ("FontSize").Value; }
		}

		private Brush BorderBrush
		{
			get { return GetBrush (FontsAndColorsItems.Item ("Keyword").Foreground); }
		}

		private Brush Foreground
		{
			get { return GetBrush (FontsAndColorsItems.Item ("Plain Text").Foreground); }
		}

		private FontFamily FontFamily
		{
			get
			{
				string family = (string)FontsAndColors.Item("FontFamily").Value;
				return new FontFamily (family);
			}
		}

		private SolidColorBrush GetBrush (uint color)
		{
			int oleColor = Convert.ToInt32 (color);
			return new SolidColorBrush (Color.FromRgb (
					(byte)((oleColor) & 0xFF),
					(byte)((oleColor >> 8) & 0xFF),
					(byte)((oleColor >> 16) & 0xFF)));
		}

		private CancellationTokenSource GetCancelSource()
		{
			var cancel = new CancellationTokenSource();
			CancellationTokenSource oldCancel = Interlocked.Exchange (ref this.cancelSource, cancel);
			if (oldCancel != null)
			{
				oldCancel.Cancel();
				oldCancel.Dispose();
			}

			return cancel;
		}

	    private void LayoutButtons (ITextSnapshot newSnapshot, CancellationToken cancelToken)
	    {
		    Task.Factory.StartNew (s =>
		    {
			    var snapshot = (ITextSnapshot)s;

			    string code = snapshot.GetText();

			    CompilationUnitSyntax root = SyntaxTree.ParseText (code, cancellationToken: cancelToken).GetRoot (cancelToken);
			    
				if (root.GetDiagnostics().Any (d => d.Info.Severity == DiagnosticSeverity.Error))
					return;

			    foreach (var m in root.DescendantNodes (n => !(n is MethodDeclarationSyntax)).OfType<MethodDeclarationSyntax>())
			    {
				    if (cancelToken.IsCancellationRequested)
					    return;

				    Location location = m.GetLocation();

				    int index = location.SourceSpan.Start;
				    ITextSnapshotLine line = snapshot.GetLineFromPosition (index);
					
					// TODO: Fix this for multi-line method signatures
				    string methodSignature = line.GetText();
					if (this.context != null && methodSignature != this.context.MethodSignature)
						continue;
				    
				    this.dispatcher.BeginInvoke ((Action)(() =>
				    {
						if (cancelToken.IsCancellationRequested)
							return;

					    Span methodSpan = Span.FromBounds (index, location.SourceSpan.End + 1);
					    ITrackingSpan tracking;
					    
						Button button = FindAdorner<Button> (methodSpan, this.view.TextSnapshot, out tracking);

					    bool preexisting = false;
						if (tracking == null)
						{
							tracking = snapshot.CreateTrackingSpan (methodSpan, SpanTrackingMode.EdgeExclusive);
							button = new Button();
							button.FontSize = FontSize * 0.90;
							button.Cursor = Cursors.Arrow;
						}
						else
							preexisting = true;

					    if (this.context == null || methodSignature != this.context.MethodSignature)
						{
							if (!preexisting)
								button.Click += OnClickInstant;

							button.Content = "Instant";
							button.Tag = new ExecutionContext
							{
								MethodSignature = methodSignature,
								Span = tracking
							};
						}
						else
						{
							if (!preexisting)
								button.Click += OnClickStopInstant;

							button.Content = "Stop Instant";
						}

					    SnapshotSpan span = new SnapshotSpan (this.view.TextSnapshot, line.Start, line.Length);

					    Geometry g = this.view.TextViewLines.GetMarkerGeometry (span, true, new Thickness());
					    if (g != null)
					    {
						    Canvas.SetLeft (button, g.Bounds.Right + 10);
						    Canvas.SetTop (button, g.Bounds.Top);
						    button.MaxHeight = g.Bounds.Height;

							if (!preexisting)
							{
								this.adorners[tracking] = button;
								this.layer.AddAdornment (AdornmentPositioningBehavior.TextRelative, span, null, button, AdornerRemoved);
							}
					    }
				    }));
			    }
		    }, newSnapshot, cancelToken);
	    }

	    private void AdornerRemoved (object tag, UIElement element)
	    {
		    this.adorners.Inverse.Remove ((FrameworkElement)element);
	    }

	    private void OnClickStopInstant (object sender, RoutedEventArgs e)
	    {
		    this.context = null;
		    this.layer.RemoveAllAdornments();
		    LayoutButtons (this.view.TextSnapshot, GetCancelSource().Token);
	    }

	    private void OnClickInstant (object sender, RoutedEventArgs e)
	    {
		    var window = new TestCodeWindow();
		    window.Owner = Application.Current.MainWindow;
		    string testCode = window.ShowForTestCode();
			if (testCode == null)
				return;

			Button b = (Button)sender;
		    this.context = (ExecutionContext)b.Tag;
		    this.context.TestCode = testCode;

			this.layer.RemoveAllAdornments();

		    var source = GetCancelSource();
		    LayoutButtons (this.view.TextSnapshot, source.Token);
			Execute (source.Token);
	    }

		private static readonly Regex IdRegex = new Regex (@"/\*_(\d+)_\*/", RegexOptions.Compiled);
		private async void Execute (CancellationToken cancelToken)
		{
			try
			{
				string code = this.context.Span.GetText (this.view.TextSnapshot);

				Instantly.InstrumentAndEvaluate (code, this.context.TestCode, cancelToken).ContinueWith (t =>
				{
					if (t.IsCanceled || t.IsFaulted)
						return;

					var methods = t.Result ?? this.context.LastData;
					if (methods == null || methods.Count == 0)
						return;

					// BUG: These can arrive out of order
					this.dispatcher.BeginInvoke ((Action)(() =>
					{
						this.context.LastData = methods;
						AdornCode (code, methods, cancelToken);
					}));
				});
			}
			catch (Exception) // We don't have a way to show errors right now
			{
			}
		}

		private void AdornCode (CancellationToken token)
		{
			if (this.context == null || this.context.LastData == null)
				return;

			AdornCode (this.context.Span.GetText (this.view.TextSnapshot), this.context.LastData, token);
		}

	    private void AdornCode (string code, IDictionary<int, MethodCall> methods, CancellationToken cancelToken)
	    {
		    ITextSnapshot snapshot = this.view.TextSnapshot;

			try
			{
				SyntaxNode root = SyntaxTree.ParseText (code, cancellationToken: cancelToken).GetRoot (cancelToken);
				//root = new FixingRewriter().Visit (root);
				var ided = new IdentifyingVisitor().Visit (root);

				Dictionary<int, ITextSnapshotLine> lineMap = new Dictionary<int, ITextSnapshotLine>();

				string line;
				int ln = snapshot.GetLineNumberFromPosition (this.context.Span.GetStartPoint (this.view.TextSnapshot));
				StringReader reader = new StringReader (ided.ToString());
				while ((line = reader.ReadLine()) != null)
				{
					MatchCollection matches = IdRegex.Matches (line);
					foreach (Match match in matches)
					{
						cancelToken.ThrowIfCancellationRequested();

						int id;
						if (!Int32.TryParse (match.Groups[1].Value, out id))
							continue;

						ITextSnapshotLine lineSnapshot = snapshot.GetLineFromLineNumber (ln);
						lineMap[id] = lineSnapshot;
					}

					ln++;
				}

				if (lineMap.Count == 0)
					return;

				// TODO: Threads
				MethodCall container = methods.Values.First();
				AdornOperationContainer (container, snapshot, lineMap, cancelToken);
			}
			catch (OperationCanceledException)
			{
			}
	    }

		private void AdornOperationContainer (OperationContainer container, ITextSnapshot snapshot, IDictionary<int, ITextSnapshotLine> lineMap, CancellationToken cancelToken)
		{
			foreach (Operation operation in container.Operations)
			{
				ITextSnapshotLine line;
				if (!lineMap.TryGetValue (operation.Id, out line))
					continue;

				Geometry g = this.view.TextViewLines.GetMarkerGeometry (line.Extent);
				if (g == null)
					continue;

				OperationVisuals vs = Mapping[operation.GetType()];

				bool preexisted = true;
				ITrackingSpan span;
				InstantView adorner = (InstantView)FindAdorner (vs.ViewType, line.Extent, snapshot, out span);
				if (adorner == null)
				{
					adorner = vs.CreateView();
					adorner.FontSize = FontSize - 1;
					adorner.FontFamily = FontFamily;
					adorner.BorderBrush = BorderBrush;
					adorner.Foreground = Foreground;
					preexisted = false;
				}
				
				adorner.Tag = operation.Id;

				OperationViewModel model = adorner.DataContext as OperationViewModel;
				if (model == null)
					adorner.DataContext = model = vs.CreateViewModel();

				model.Operation = operation;

				if (operation is Loop)
				{
					Loop loop = (Loop)operation;

					var loopModel = (LoopViewModel)model;

					LoopIteration[] iterations = loopModel.Iterations;
					if (!preexisted || loopModel.Iteration > iterations.Length - 1)
						loopModel.Iteration = iterations.Length;

					if (!preexisted)
					{
						loopModel.IterationChanged += (sender, args) =>
						{
							LoopIteration iteration = args.PreviousIteration;
							if (iteration != null)
							{
								foreach (int id in iteration.Operations.Select (o => o.Id).Distinct())
								{
									FrameworkElement opAdorner = FindAdorner (id);
									if (opAdorner != null)
										this.layer.RemoveAdornment (opAdorner);
								}
							}

							//this.layer.RemoveAllAdornments();	
							AdornCode (cancelToken);
						};
					}

					if (iterations.Length > 0)
						AdornOperationContainer (iterations[loopModel.Iteration - 1], snapshot, lineMap, cancelToken);
				}

				Canvas.SetLeft (adorner, g.Bounds.Right + 10);
				Canvas.SetTop (adorner, g.Bounds.Top + 1);
				adorner.Height = g.Bounds.Height - 2;
				adorner.MaxHeight = g.Bounds.Height - 2;

				if (span == null)
					span = snapshot.CreateTrackingSpan (line.Extent, SpanTrackingMode.EdgeExclusive);

				this.adorners[span] = adorner;

				if (!preexisted)
					this.layer.AddAdornment (AdornmentPositioningBehavior.TextRelative, line.Extent, null, adorner, AdornerRemoved);
			}
		}

		private class OperationVisuals
		{
			public Type ViewType;
			public Func<InstantView> CreateView;
			public Type ViewModelType;
			public Func<OperationViewModel> CreateViewModel;

			public static OperationVisuals Create<TView,TViewModel> (Func<TView> createView, Func<TViewModel> createModel)
				where TView : InstantView
				where TViewModel : OperationViewModel
			{
				OperationVisuals visuals = new OperationVisuals();
				visuals.CreateView = createView;
				visuals.ViewType = typeof (TView);
				visuals.CreateViewModel = createModel;
				visuals.ViewModelType = typeof (TViewModel);

				return visuals;
			}
		}

	    private static readonly Dictionary<Type, OperationVisuals> Mapping = new Dictionary<Type, OperationVisuals>
	    {
			{ typeof(StateChange), OperationVisuals.Create (() => new StateChangeView(), () => new StateChangeViewModel()) },
			{ typeof(ReturnValue), OperationVisuals.Create (() => new ReturnValueView(), () => new ReturnValueViewModel()) },
			{ typeof(Loop), OperationVisuals.Create (() => new LoopView(), () => new LoopViewModel()) }
	    };

		private FrameworkElement FindAdorner (int id)
		{
			return this.adorners.Values.FirstOrDefault (e => e.Tag != null && (int)e.Tag == id);
		}

		private T FindAdorner<T> (Span span, ITextSnapshot snapshot, out ITrackingSpan tracking)
			where T : FrameworkElement
		{
			return (T)FindAdorner (typeof (T), span, snapshot, out tracking);
		}

		private FrameworkElement FindAdorner (Type viewType, Span span, ITextSnapshot snapshot, out ITrackingSpan tracking)
		{
			tracking = null;

			foreach (var kvp in this.adorners)
			{
				Span s = kvp.Key.GetSpan (snapshot);
				if (!s.Contains (span) || kvp.Value.GetType() != viewType)
					continue;

				tracking = kvp.Key;
				return kvp.Value;
			}

			return null;
		}
    }
}
