# Include Fixor

A CLI tool that can fix include directives in C/C++ source files based on a scan initiated from a configuration file.

You can use this tool to end-up with a single include directory to add to your C++ project configuration. 
Some projects like old game engines are careless with their include directories and end-up with soo many of them that it 
become dangerous when you have header files with the same name. 
You now have a dependency on the order in which you have provided your include directories which is hard to read and understand.

What you actually really want is a single directory called something like `source`, and under that folder, assuming you are
using multiple libraries could look a bit like this:

- `source/cpp/renderer`
- `source/cpp/filesystem`
- `source/cpp/imgui`
- `source/cpp/oodle`

- `source/include/renderer`
- `source/include/filesystem`
- `source/include/imgui`
- `source/include/oodle`

The include directory that should be registered in the project is:

- `source/include`

So source and header files should use the following standard to include a header file:

```c++
#include "renderer/dx12/dx12.h"
#include "oodle/oodle.h"
```

Where `renderer/dx12/dx12.h` is a sub directory under `source/include`.

