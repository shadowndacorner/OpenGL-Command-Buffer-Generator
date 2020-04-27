# OpenGL Command Buffer Generator

A utility that parses the generated [Glad](https://glad.dav1d.de/) header to emit C++ classes to buffer OpenGL commands.  The intention is to allow for multithreaded command creation, then single threaded command submission, similar to D3D11 deferred contexts.

By default, this utility also generates type-safe enums based on the [OpenGL Registry's XML specification](https://raw.githubusercontent.com/KhronosGroup/OpenGL-Registry/master/xml/gl.xml).  Command buffer arguments automatically use the appropriate enum based on the XML.  This behavior can be disabled with the `--noxml` flag.

For an example of a simple project structure using this, see [here](https://github.com/shadowndacorner/OpenGL-Command-Buffer-Generator-Sample).

## Usage
**Command**|**Description**
------|-----
--noxml|Indicates that XML should not be used.  If this is set, no enums will be generated and the command buffer API will use standard GLenums.
-x, --inputxml|Input XML file to read.  Used to generate type-safe bindings for command buffers.  If this is not provided and the --noxml flag is not set, the XML will be downloaded from GitHub.
-i, --input|Input file to read.  Must be either a header file generated by GLAD.
-o, --outdir|(Default: generated) Directory in which to generate headers.

### Example

`glthreadgen.exe -i "glad/include/glad.h" -o "generated/gl_cmd_buf"`

This will result in two new directories - `generated/gl_cmd_buf/src` and `generated/gl_cmd_buf/include`.  Simply add the include directory to your build system and compile the files in `/src/`.

## Building
This project is based on .NET Core 3.1.  With the SDK installed, compile using the .NET Core CLI (`dotnet build -c Release`) or using Visual Studio 2019.  It should run on any operating system supported by .NET Core 3.1.