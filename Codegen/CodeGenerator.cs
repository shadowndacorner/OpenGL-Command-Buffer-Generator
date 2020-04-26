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
        public const string DataBuffer = "m_Buffer";
        public const string ResourceManager = "m_ResourceManager";

        public string BaseDir { get; private set; }
        public string IncludeDir { get; private set; }
        public string SourceDir { get; private set; }

        public GLADHeaderParser Parser { get; private set; }
        public CodegenOverrideTracker Tracker { get; private set; }

        public CodeGenerator(string baseDirectory, GLADHeaderParser parser, CodegenOverrideTracker tracker)
        {
            Tracker = tracker;
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
            return new FileStream(path, FileMode.Create, FileAccess.Write);
        }

        public FileStream CreateSourceFile(string name)
        {
            var path = Path.Combine(SourceDir, name);
            Console.WriteLine($"Creating file {path}...");
            return new FileStream(path, FileMode.Create, FileAccess.Write);
        }

        public async Task GenerateSources()
        {
            await Task.WhenAll(GenerateWriteSource(), GenerateReadSource());
        }


        public async Task GenerateReadSource()
        {
            using (var source = CreateSourceFile("gl_command_buffer_read.cpp"))
            {
                var context = new CodegenContext(source);
                await context.EmitLine("#include <gl_command_buffer.hpp>");
                context.EmitLine();

                await context.EmitLine("using namespace multigl;");
                
                await context.EmitLine("void CommandBuffer::ProcessCommands(ResourceManager& resourceManager)");
                await context.EmitScope(async () =>
                {
                    await context.EmitLine($"while({DataBuffer}.has_commands())");
                    await context.EmitScope(async () =>
                    {
                        await context.EmitLine($"auto cmd = {DataBuffer}.read_command();");
                        await context.EmitLine("switch(cmd)");
                        await context.EmitScope(async () =>
                        {
                            foreach(var fk in Parser.Functions)
                            {
                                var overrideList = Tracker.GetOverrideList(fk.Key);
                                var function = fk.Value;
                                await context.EmitLine($"case CommandId::{function.NoGLName}:");
                                await context.EmitScope(async () => {
                                    Func<Task> defaultReadFunc = async () =>
                                    {
                                        var args = function.Type.Arguments;
                                        for (int i = 0; i < args.Count; ++i)
                                        {
                                            var arg = args[i];
                                            var argReadOverride = Tracker.GetArgumentTypeReadOverride(arg.Type);
                                            if (argReadOverride != null)
                                            {
                                                await argReadOverride(context, function, arg);
                                            }
                                            else
                                            {
                                                await context.EmitLine($"{arg.Type} {arg.Name} = {DataBuffer}.read<{arg.Type}>();");
                                            }
                                        }
                                        context.EmitIndent();
                                        await context.Emit($"{function.Name}(");

                                        for (int i = 0; i < args.Count; ++i)
                                        {
                                            var arg = args[i];
                                            await context.Emit($"{arg.Name}");
                                            if (i < args.Count - 1)
                                            {
                                                await context.Emit(", ");
                                            }
                                        }

                                        await context.EmitLineUnindented($");");
                                    };

                                    if (overrideList == null || overrideList.Count == 0)
                                    {
                                        await defaultReadFunc();
                                    }
                                    else
                                    {
                                        bool hasRun = false;
                                        foreach (var v in overrideList)
                                        {
                                            if (v.ModifyReadFunction != null)
                                            {
                                                await v.ModifyReadFunction(context, defaultReadFunc, function);
                                                hasRun = true;
                                            }
                                        }

                                        if (!hasRun)
                                        {
                                            await defaultReadFunc();
                                        }
                                    }
                                });
                                await context.EmitLine($"break;");
                            }
                        });
                    });
                    await context.EmitLine($"{DataBuffer}.reset();");
                });
            }
        }

        public async Task GenerateWriteSource()
        {
            using (var source = CreateSourceFile("gl_command_buffer_write.cpp"))
            {
                var context = new CodegenContext(source);
                await context.EmitLine("#include <gl_command_buffer.hpp>");
                context.EmitLine();

                await context.EmitLine("using namespace multigl;");

                await context.EmitLine($"CommandBuffer::CommandBuffer(ResourceManager& mgr) : {ResourceManager}(mgr) {{}}");
                await context.EmitLine($"CommandBuffer::~CommandBuffer(){{}}");
                context.EmitLine();

                foreach (var fk in Parser.Functions)
                {
                    var overrideList = Tracker.GetOverrideList(fk.Key);
                    var function = fk.Value;

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

                    await context.EmitScope(async () =>
                    {
                        await context.EmitLine($"{DataBuffer}.write_command(CommandId::{function.NoGLName});");
                        Func<Task> defaultWriteFunc = async () =>
                        {
                            if (function.Type.ReturnType == "void")
                            {
                                for (int i = 0; i < function.Type.Arguments.Count; ++i)
                                {
                                    var arg = function.Type.Arguments[i];
                                    var argWriteOverride = Tracker.GetArgumentTypeWriteOverride(arg.Type);
                                    if (argWriteOverride != null)
                                    {
                                        await argWriteOverride(context, function, arg);
                                    }
                                    else
                                    {
                                        await context.EmitLine($"{DataBuffer}.write({arg.Name});");
                                    }
                                }
                            }
                            else
                            {
                                await context.EmitLine($"#if defined(MGL_STRICT_COMPILATION)");
                                await context.EmitLine($"#error Unimplemented function with return value");
                                await context.EmitLine($"#endif");
                                await context.EmitLine($"return 0;");
                            }
                        };

                        if (overrideList == null || overrideList.Count == 0)
                        {
                            await defaultWriteFunc();
                        }
                        else
                        {
                            bool hasRun = false;
                            foreach (var v in overrideList)
                            {
                                if (v.ModifyWriteFunction != null)
                                {
                                    await v.ModifyWriteFunction(context, defaultWriteFunc, function);
                                    hasRun = true;
                                }
                            }

                            if (!hasRun)
                            {
                                await defaultWriteFunc();
                            }
                        }
                    });
                    context.EmitLine();
                }
            }
        }

        public async Task GenerateHeaders()
        {
            await Task.WhenAll(GenerateCommandBufferHeader(), GenerateEnumTypeHeader(), GenerateRWBuffer());
        }

        public async Task GenerateRWBuffer()
        {
            using (var header = CreateHeader("raw_rw_buffer.hpp"))
            {
                using (var writer = new StreamWriter(header))
                {
                    await writer.WriteAsync(@"#pragma once
#include <vector>
#include ""gl_function_enums.hpp""

namespace multigl
{
    class raw_rw_buffer
    {
    public:
        inline raw_rw_buffer() : m_WriteIdx(0), m_ReadIdx(0) { }
        inline ~raw_rw_buffer() { }
    
    public:
        inline void reset()
        {
            m_Buffer.resize(0);
            m_WriteIdx = 0;
            m_ReadIdx = 0;
        }

    public:
        inline void write_command(const CommandId& cmd)
        {
            write<gl_command_id_t>(gl_command_id_t(cmd));
        }

        template<typename T>
        inline void write(const T& val)
        {
            auto align = alignof(T);
            auto mod = m_WriteIdx % align;
            if (mod == 0)
            {
                ensure_write_capacity(sizeof(T));
            }
            else
            {
                ensure_write_capacity(sizeof(T) + mod);
                write_padding(mod);
            }

            write_unchecked(val);
        }

    private:
        inline void ensure_write_capacity(size_t amount)
        {
            auto tgCapacity = m_WriteIdx + amount;
            if (m_Buffer.capacity() < tgCapacity)
            {
                m_Buffer.reserve(tgCapacity);
            }
        }

        inline void write_padding(size_t padding)
        {
            m_Buffer.resize(m_WriteIdx + padding);
            m_WriteIdx += padding;
        }

        template<typename T>
        inline void write_unchecked(const T& val)
        {
            m_Buffer.resize(m_WriteIdx + sizeof(T));
            *(reinterpret_cast<T*>(&m_Buffer[m_WriteIdx])) = val;
            m_WriteIdx += sizeof(T);
        }

    public:
        inline bool has_commands()
        {
            return m_ReadIdx < m_WriteIdx;
        }


        inline CommandId read_command()
        {
            return read<CommandId>();
        }

        template<typename T>
        inline T read()
        {
            auto align = alignof(T);
            auto mod = m_ReadIdx % align;
            if (mod != 0)
            {
                read_padding(mod);
            }

            return read_unchecked<T>();
        }

    private:
        inline void read_padding(size_t padding)
        {
            m_ReadIdx += padding;
        }

        template<typename T>
        inline T read_unchecked()
        {
            auto old = m_ReadIdx;
            m_ReadIdx += sizeof(T);
            return *(reinterpret_cast<T*>(&m_Buffer[old]));
        }

        private:
        std::vector<char> m_Buffer;
        size_t m_WriteIdx;
        size_t m_ReadIdx;
    };
}");
                }
            }
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
                ++context.IndentLevel;
                {

                    await context.EmitLine($"typedef {enumType} gl_command_id_t;");
                    await context.EmitLine("namespace CommandIdEnum");
                    await context.EmitLine("{");
                    ++context.IndentLevel;
                    {
                        await context.EmitLine("enum Enum : gl_command_id_t");
                        await context.EmitLine("{");
                        ++context.IndentLevel;
                        {
                            foreach(var v in Parser.Functions)
                            {
                                await context.EmitLine($"{v.Value.NoGLName},");
                            }
                            await context.EmitLine("Count");
                        }
                        --context.IndentLevel;
                        await context.EmitLine("};");
                    }
                    --context.IndentLevel;
                    await context.EmitLine("}");

                    await context.EmitLine("typedef CommandIdEnum::Enum CommandId;");
                }
                --context.IndentLevel;
                await context.EmitLine("}");
            }
        }

        public async Task GenerateCommandBufferHeader()
        {
            using (var header = CreateHeader("gl_command_buffer.hpp"))
            {
                var context = new CodegenContext(header);
                await context.EmitLine("#pragma once");
                await context.EmitLine("#include <glad/glad.h>");
                await context.EmitLine("#include \"gl_resource_manager.hpp\"");
                await context.EmitLine("#include \"raw_rw_buffer.hpp\"");

                context.EmitLine();
                await context.EmitLine("namespace multigl");
                await context.EmitLine("{");
                ++context.IndentLevel;
                {
                    await context.EmitLine("class CommandBuffer");
                    await context.EmitLine("{");
                    ++context.IndentLevel;
                    {
                        await context.EmitStructAccess("public");
                        await context.EmitLine("CommandBuffer(ResourceManager& manager);");
                        await context.EmitLine("~CommandBuffer();");
                        context.EmitLine();

                        await context.EmitStructAccess("public");

                        var accessTracker = context.CreateAccessTracker("public");
                        foreach (var function in Parser.Functions.Values)
                        {
                            await accessTracker.WriteAccess(function.Access);

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
                        context.EmitLine();

                        await context.EmitStructAccess("public");
                        await context.EmitLine("void ProcessCommands(ResourceManager& resourceManager);");
                        context.EmitLine();

                        await context.EmitStructAccess("private");
                        await context.EmitLine($"ResourceManager& {ResourceManager};");
                        await context.EmitLine($"raw_rw_buffer {DataBuffer};");

                    }
                    --context.IndentLevel;
                    await context.EmitLine("};");
                }
                --context.IndentLevel;
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
