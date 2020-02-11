# Include Fixor

A CLI tool that can fix include directives in C/C++ source files based on a scan initiated from a configuration file.

You can use this tool to end-up with a single include directory to add to your C++ project configuration. 
Some game engine are poluted with soo many include directories that it become dangerous when you have header files with
the same name. You now have a dependency on the order in which you have provided your include directories which is hard
to read and understand.

What you generally want is this:
- Include Directory (relative to .sln) = ``src/``

All source files should use the following standard to include a header file:

``#include "render_engine/dx12/dx12.h"``

Where ``render_engine/dx12/dx12.h`` is a sub directory of ``src``.

