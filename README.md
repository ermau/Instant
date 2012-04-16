Summary
--------
This is a research project into whether or not it is feasible to enable
real time state output of a method while it is being written in a full
sized .NET project. The original idea for this comes from 
[Bret Victor's Inventing on Principle talk](http://vimeo.com/36579366).

There is currently a standalone prototype, and while this demos nicely,
it really doesn't prove that it's possible to enable this type of feature
on a full project. So the goal is to expand to multiple files, and eventually
a Visual Studio and MonoDevelop plugin. The prototype is built on .NET 4.0 and
the [Roslyn CTP](http://msdn.com/roslyn).

Using the prototype
-----
Currently the prototype expects all code that you wish to log to be contained in
methods, but there isn't a need for a containing class. Simply write your method,
and after it add a call to it with your test parameters, such as this:

	void DoStuff (int y)
	{
		int x = y;
	}

	DoStuff (5);

Contact
-------
[@ermau](http://twitter.com/ermau)  
[me@ermau.com](mailto://me@ermau.com)
