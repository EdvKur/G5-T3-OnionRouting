G5-T3-OnionRouting
==================
This project implements a simple verion of onion routing for HTTP traffic.

Compiling
---------
For compiling, you can either use Visual Studio or Mono.

In visual studio, just import `OnionRouting.sln` and compile.

When using mono, do

	xbuild OnionRouting.sln

which should create bin directories for each of the services.


Services
--------
G5-T3-OnionRouting uses multiple services that run on different machines (usually AWS instances) and communicate with
each other to create a distributed system. The services can be configured with their corresponding `.exe.config`
XML file.

Make sure to set your AWS credentials location in `directory.exe.config`.

Testing
-------
Some parts of the project can be locally tested via nunit.
To run the unit tests do

	nunit-console4 tests.dll
