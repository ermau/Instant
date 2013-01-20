## Summary

This is a research project into whether or not it is feasible to enable
real time state output of a method while it is being written in a full
sized .NET project. The original inspiration for this comes from
[Bret Victor's Inventing on Principle talk](http://vimeo.com/36579366).

There is currently a prototype extension to Visual Studio 2012 as well as a
standalone client.

This is mostly undocumented, hack-filled prototype code. You have been warned.

## Limitations

- Portable library projects are not supported.
- There is no visualization for multiple threads.
- Infinite loop detection is not perfect (false positives, false negatives).
- Visual Studio extension performs poorly (especially when debugging it).
- Method signatures must be on a single line

For more details on the limitations, see [Instant 0.1](http://ermau.com/instant-0-1/)

## Requirements
-	.NET 4.5 RTM

## Using the Visual Studio extension prototype

Visual Studio 2012 RTM Pro is required for the extension. For hacking on it,
the Visual Studio 2012 SDK is also required.

1.	Launch the `Instant.VisualStudio` project from `Instant.sln`.
1.	Bring up the quick fix menu for a method (either by hovering over the line, or
	pressing CTRL+. with your cursor on the method).
1.	Enter your code to call this method. This can be multiple lines to setup arguments.

Contact
-------
[@ermau](http://twitter.com/ermau)  
[me@ermau.com](mailto://me@ermau.com)
