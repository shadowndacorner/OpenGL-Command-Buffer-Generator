# OpenGL Command Buffer Generator

A utility that parses the generated [Glad](https://glad.dav1d.de/) header to emit C++ classes to buffer OpenGL commands.  The intention is to allow for multithreaded command creation, then single threaded command submission.

For an example of a simple project structure using this, see [here](https://github.com/shadowndacorner/OpenGL-Command-Buffer-Generator-Sample).

## Usage
| -i, --input  | Required. Header file to read.  Must be generated by GLAD.   |
|--------------|--------------------------------------------------------------|
| -o, --outdir | (Default: generated) Directory in which to generate headers. |

### Example Usage

`glthreadgen.exe -i "glad/include/glad.h" -o "generated/gl_cmd_buf"`

This will result in two new directories - `generated/gl_cmd_buf/src` and `generated/gl_cmd_buf/include`.  Simply add the include directory to your build system and compile the files in `/src/`.