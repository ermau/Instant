## Summary

This is a research project into whether or not it is feasible to enable
real time state output of a method while it is being written in a full
sized .NET project. The original inspiration for this comes from
[Bret Victor's Inventing on Principle talk](http://vimeo.com/36579366).

There is currently a prototype extension to Visual Studio 2012 as well as a
standalone client. Neither of these prototypes currently work on a full project,
but that is the eventual goal (along with a MonoDevelop addin).

## Limitations

- Isolated methods only (no class fields or other methods).
- There is no visualization for multiple threads.
- Infinite loop detection is not perfect (false positives, false negatives).
- Visual Studio extension performs poorly.

For more details on the limitations, see [Instant 0.1](http://ermau.com/instant-0-1/)

## Requirements
-	.NET 4.5 RTM
-	[Roslyn September CTP](http://msdn.com/roslyn)

## Using the Visual Studio extension prototype

Visual Studio 2012 RTM Pro is required for the extension. The Visual Studio 2012 SDK is
also required, but the Roslyn installer should prompt for that anyway.

1.	Launch the `Instant.VisualStudio` project from `Instant.sln`.
1.	Click an `Instant` button next to a method.
1.	Enter your code to call this method. This can be multiple lines to setup arguments,
	but remember that currently you can not use other types in your project.

## Using the standalone prototype

Currently the prototype expects all code that you wish to log to be contained in
methods, but there isn't a need for a containing class. Simply write your method,
and after it add a call to it with your test parameters, such as this:

```csharp
void DoStuff (int y)
{
	int x = y;
}

DoStuff (5);
```

Contact
-------
[@ermau](http://twitter.com/ermau)  
[me@ermau.com](mailto://me@ermau.com)
