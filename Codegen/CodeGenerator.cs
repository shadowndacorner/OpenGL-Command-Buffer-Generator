using System;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Text;

namespace GLThreadGen
{
    public class CodeGenerator
    {
        public string BaseDir { get; private set; }
        public string IncludeDir { get; private set; }
        public string SourceDir { get; private set; }

        public HeaderParser Parser { get; private set; }

        public CodeGenerator(string baseDirectory, HeaderParser parser)
        {
            BaseDir = baseDirectory;
            Parser = parser;

            IncludeDir = Path.Combine(BaseDir, "include");
            SourceDir = Path.Combine(BaseDir, "src");
        }

        private void InitDirectories()
        {
            if (!Directory.Exists(BaseDir))
            {
                Directory.CreateDirectory(BaseDir);
            }

            if (!Directory.Exists(IncludeDir))
            {
                Directory.CreateDirectory(IncludeDir);
            }

            if (!Directory.Exists(SourceDir))
            {
                Directory.CreateDirectory(SourceDir);
            }
        }

        public void Generate()
        {
            InitDirectories();
            Task.WaitAll(GenerateSources(), GenerateHeaders());
        }

        public FileStream CreateHeader(string name)
        {
            var path = Path.Combine(IncludeDir, name);
            Console.WriteLine($"Creating file {path}...");
            return new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write);
        }

        public FileStream CreateSourceFile(string name)
        {
            var path = Path.Combine(SourceDir, name);
            Console.WriteLine($"Creating file {path}...");
            return new FileStream(path, FileMode.Create, FileAccess.Write);
        }

        public async Task GenerateSources()
        {
            using (var source = CreateSourceFile("gl_command_buffer.cpp"))
            {
                var context = new CodegenContext(source);
                await context.EmitLine("#include <gl_command_buffer.hpp>");
                context.EmitLine();

                await context.EmitLine("using namespace multigl;");

                await context.EmitLine($"CommandBuffer::CommandBuffer(){{}}");
                await context.EmitLine($"CommandBuffer::~CommandBuffer(){{}}");
                context.EmitLine();

                foreach (var function in Parser.Functions.Values)
                {
                    var noGLName = function.NoGLName;
                    await context.Emit($"{function.Type.ReturnType} CommandBuffer::{noGLName}(");

                    for (int i = 0; i < function.Type.Arguments.Count; ++i)
                    {
                        var arg = function.Type.Arguments[i];
                        await context.Emit($"{arg.Type} {arg.Name}");
                        if (i < function.Type.Arguments.Count - 1)
                        {
                            await context.Emit(", ");
                        }
                    }

                    await context.EmitLineUnindented(")");
                    if (function.Type.ReturnType == "void")
                    {
                        await context.EmitLine("{");
                        await context.EmitLine("}");
                    }
                    else
                    {
                        await context.EmitLine("{");
                        {
                            ++context.IndentLevel;
                            await context.EmitLine($"return 0;");
                            --context.IndentLevel;
                        }
                        await context.EmitLine("}");
                    }
                    context.EmitLine();
                }
            }
        }

        public async Task GenerateHeaders()
        {
            await Task.WhenAll(GenerateCommandBufferHeader(), GenerateEnumTypeHeader());
        }

        public async Task GenerateEnumTypeHeader()
        {
            string enumType = null;
            int numValues = Parser.Functions.Count;
            if (numValues < byte.MaxValue)
            {
                enumType = "uint8_t";
            }
            else if (numValues < ushort.MaxValue)
            {
                enumType = "uint16_t";
            }
            else if ((uint)numValues < uint.MaxValue)
            {
                enumType = "uint32_t";
            }

            using (var header = CreateHeader("gl_function_enums.hpp"))
            {
                var context = new CodegenContext(header);
                await context.EmitLine("#pragma once");
                await context.EmitLine("#include <stdint.h>");
                context.EmitLine();

                await context.EmitLine("namespace multigl");
                await context.EmitLine("{");
                {
                    ++context.IndentLevel;

                    await context.EmitLine($"typedef {enumType} gl_command_id_t;");
                    await context.EmitLine("namespace FunctionTypeEnum");
                    await context.EmitLine("{");
                    {
                        ++context.IndentLevel;
                        await context.EmitLine("enum Enum : gl_command_id_t");
                        await context.EmitLine("{");
                        {
                            ++context.IndentLevel;
                            foreach(var v in Parser.Functions)
                            {
                                await context.EmitLine($"{v.Value.NoGLName},");
                            }
                            await context.EmitLine("Count");
                            --context.IndentLevel;
                        }
                        await context.EmitLine("};");
                        --context.IndentLevel;
                    }
                    await context.EmitLine("}");

                    await context.EmitLine("typedef FunctionTypeEnum::Enum CommandId;");
                    --context.IndentLevel;
                }
                await context.EmitLine("}");
            }
        }

        public async Task GenerateCommandBufferHeader()
        {
            using (var header = CreateHeader("gl_command_buffer.hpp"))
            {
                var context = new CodegenContext(header);
                await context.EmitLine("#pragma once");
                await context.EmitLine("#include \"glad/glad.h\"");
                context.EmitLine();
                await context.EmitLine("namespace multigl");
                await context.EmitLine("{");
                {
                    ++context.IndentLevel;
                    await context.EmitLine("class CommandBuffer");
                    await context.EmitLine("{");
                    {
                        ++context.IndentLevel;
                        await context.EmitStructAccess("public");
                        await context.EmitLine("CommandBuffer();");
                        await context.EmitLine("~CommandBuffer();");
                        context.EmitLine();

                        await context.EmitStructAccess("public");
                        foreach (var function in Parser.Functions.Values)
                        {
                            var noGLName = function.NoGLName;
                            context.EmitIndent();

                            await context.Emit($"{function.Type.ReturnType} {noGLName}(");

                            for (int i = 0; i < function.Type.Arguments.Count; ++i)
                            {
                                var arg = function.Type.Arguments[i];
                                await context.Emit($"{arg.Type} {arg.Name}");
                                if (i < function.Type.Arguments.Count - 1)
                                {
                                    await context.Emit(", ");
                                }
                            }

                            await context.EmitLineUnindented(");");
                        }

                        --context.IndentLevel;
                    }
                    await context.EmitLine("};");
                    --context.IndentLevel;
                }
                await context.EmitLine("}");
            }
        }

        public void OpenDirectory()
        {
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                var startinfo = new ProcessStartInfo();
                startinfo.UseShellExecute = true;
                startinfo.FileName = "explorer.exe";
                startinfo.CreateNoWindow = true;
                startinfo.Arguments = BaseDir;
                Process.Start(startinfo);
            }
        }
    }
}
