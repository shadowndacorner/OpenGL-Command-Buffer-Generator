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

        public async Task GenerateHeaders()
        {
            await Task.WhenAll(GenerateCommandBufferHeader(), GenerateEnumTypeHeader(), GenerateRWBuffer(), GenerateResourceManager(), GenerateSlotmap(), GenerateGLUtil());
        }

        #region Sources
        public async Task GenerateReadSource()
        {
            using (var source = CreateSourceFile("gl_command_buffer_read.cpp"))
            {
                var context = new CodegenContext(source);
                await context.EmitLine("#include <gl_command_buffer.hpp>");
                await context.EmitLine("#include <gl_utilities.hpp>");
                context.EmitLine();

                await context.EmitLine("using namespace multigl;");
                
                await context.EmitLine("void CommandBuffer::ProcessCommands()");
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
                                    };

                                    if (overrideList == null || overrideList.Count == 0)
                                    {
                                        var args = function.Type.Arguments;
                                        await defaultReadFunc();
                                        context.EmitIndent();
                                        await context.Emit($"GL_CHECK({function.Name}(");

                                        for (int i = 0; i < args.Count; ++i)
                                        {
                                            var arg = args[i];
                                            await context.Emit($"{arg.Name}");
                                            if (i < args.Count - 1)
                                            {
                                                await context.Emit(", ");
                                            }
                                        }

                                        await context.EmitLineUnindented($"));");
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
                                            var args = function.Type.Arguments;
                                            await defaultReadFunc();
                                            context.EmitIndent();
                                            await context.Emit($"GL_CHECK({function.Name}(");

                                            for (int i = 0; i < args.Count; ++i)
                                            {
                                                var arg = args[i];
                                                await context.Emit($"{arg.Name}");
                                                if (i < args.Count - 1)
                                                {
                                                    await context.Emit(", ");
                                                }
                                            }

                                            await context.EmitLineUnindented($"));");
                                        }
                                    }
                                    await context.EmitLine($"break;");
                                });
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
        #endregion

        #region Headers
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
                await context.EmitScope(async ()=>
                {

                    await context.EmitLine($"typedef {enumType} gl_command_id_t;");
                    await context.EmitLine("namespace CommandIdEnum");
                    await context.EmitScope(async () =>
                    {
                        await context.EmitEnum("Enum : gl_command_id_t", async () =>
                        {
                            foreach (var v in Parser.Functions)
                            {
                                await context.EmitLine($"{v.Value.NoGLName},");
                            }
                            await context.EmitLine("Count");
                        });
                    });

                    await context.EmitLine("typedef CommandIdEnum::Enum CommandId;");
                });
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
                await context.EmitScope(async () =>
                {
                    await context.EmitClass("CommandBuffer", async ()=>
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
                        await context.EmitLine("void ProcessCommands();");
                        context.EmitLine();

                        await context.EmitStructAccess("private");
                        await context.EmitLine($"ResourceManager& {ResourceManager};");
                        await context.EmitLine($"raw_rw_buffer {DataBuffer};");

                    });
                });
            }
        }
        #endregion

        #region Static files
        public async Task GenerateGLUtil()
        {
            using (var header = CreateHeader("gl_utilities.hpp"))
            {
                using (var writer = new StreamWriter(header))
                {
                    await writer.WriteAsync(@"#pragma once
#include ""glad/glad.h""
#include <stdio.h>

#ifndef NDEBUG
#define DEBUG
#endif

inline const char* GetGLErrorStr(GLenum err)
{
	switch (err)
	{
		case GL_NO_ERROR:
			return ""No error"";
		case GL_INVALID_ENUM:
			return ""Invalid enum"";
		case GL_INVALID_VALUE:
			return ""Invalid value"";
		case GL_INVALID_OPERATION:
			return ""Invalid operation"";
#ifdef GL_STACK_OVERFLOW
		case GL_STACK_OVERFLOW:
			return ""Stack overflow"";
#endif
#ifdef GL_STACK_UNDERFLOW
		case GL_STACK_UNDERFLOW:
			return ""Stack underflow"";
#endif
		case GL_OUT_OF_MEMORY:
			return ""Out of memory"";
		case GL_INVALID_FRAMEBUFFER_OPERATION:
			return ""INVALID_FRAMEBUFFER_OPERATION"";
		default:
			return ""Unknown error"";
	}
}

inline void ForceCheckGLError()
{
	bool found = false;
	while (true)
	{
		const GLenum err = glGetError();
		if (GL_NO_ERROR == err)
			break;

		found = true;
		fprintf(stderr, ""OpenGL Error: %s\n"", GetGLErrorStr(err));
		break;
	}
#ifdef DEBUGa
	assert(!found && ""Graphics backend error(s).  Check console for details."");
#else
	if (found)
	{
		fprintf(stderr, ""Graphics backend error(s).  Check console for details.\n"");
	}
#endif
}

inline void CheckGLError()
{
#ifdef DEBUG
	ForceCheckGLError();
#endif
}

#define GL_CHECK(func) \
	func;              \
	CheckGLError()

");
                }
            }
        }

        public async Task GenerateSlotmap()
        {
            using (var header = CreateHeader("gl_slotmap.hpp"))
            {
                using (var writer = new StreamWriter(header))
                {
                    await writer.WriteAsync(@"#pragma once
#include <type_traits>
#include <algorithm>
#include <vector>
#include <mutex>

#if !defined MGL_ASSERT
#include <cassert>
#define MGL_ASSERT(...) assert( __VA_ARGS__ );
#endif

namespace multigl
{
    template <typename T, typename handle_t>
    class slot_map
    {
    public:
        using value_t = T;

    private:
        struct entry
        {
            value_t value;
            uint32_t generation;
            bool occupied;
        };

    public:
        using allocator_t = std::allocator<entry>;

    public:
        inline slot_map()
            : m_Data(NULL)
            , m_Size(0)
            , m_NextID(0)
            , m_Generation(0)
        {
        }

        inline ~slot_map()
        {
            if (!std::is_trivially_destructible<value_t>())
            {
                for (uint32_t i = 0; i < m_Size; ++i)
                {
                    auto& from = m_Data[i];
                    if (from.occupied)
                    {
                        from.value.~value_t();
                    }
                }
            }
            m_Allocator.deallocate(m_Data, m_Size);
        }

    public:
        template <typename... Args>
        inline handle_t create(Args&&... arg)
        {
            std::lock_guard<std::mutex> lock(m_Mutex);
            handle_t handle = allocate_id();
            ensure_size(handle.index + 1);
            MGL_ASSERT(!m_Data[handle.index].occupied && ""Attempted to recreate pre-existing value"");
            new (&m_Data[handle.index].value) value_t(std::forward<Args>(arg)...);
            m_Data[handle.index].occupied = true;
            handle.generation = ++m_Data[handle.index].generation;
            return handle;
        }

        template <typename... Args>
        inline T* create_and_get(Args&&... arg)
        {
            std::lock_guard<std::mutex> lock(m_Mutex);
            handle_t handle = allocate_id();
            ensure_size(handle.index + 1);
            MGL_ASSERT(!m_Data[handle.index].occupied && ""Attempted to recreate pre-existing value"");
            new (&m_Data[handle.index].value) value_t(std::forward<Args>(arg)...);
            m_Data[handle.index].occupied = true;
            handle.generation = ++m_Data[handle.index].generation;
            return &m_Data[handle.index].value;
        }

        inline void destroy(const handle_t& handle)
        {
            std::lock_guard<std::mutex> lock(m_Mutex);
            ++m_Generation;
            MGL_ASSERT(is_valid(handle) && ""Attempted to destroy invalid handle"");
            m_Data[handle.index].value.~value_t();
            m_Data[handle.index].occupied = false;
            m_FreeIDs.push_back(handle.index);
        }

    public:
        inline value_t* get(const handle_t& handle)
        {
            MGL_ASSERT(m_Data[handle.index].generation == handle.generation);
            return &m_Data[handle.index].value;
        }

        inline value_t* get_nogencheck(const handle_t& handle)
        {
            return &m_Data[handle.index].value;
        }

        inline bool is_index_resident(const handle_t& handle)
        {
            return m_Data[handle.index].occupied;
        }

        inline bool is_valid(const handle_t& handle)
        {
            return handle.index < m_Size && m_Data[handle.index].occupied && handle.generation == m_Data[handle.index].generation;
        }

        inline void reserve(uint32_t size)
        {
            ensure_size(size);
        }

        inline size_t max_index()
        {
            return m_Size;
        }

    private:
        inline uint32_t allocate_id()
        {
            if (m_FreeIDs.size() > 0)
            {
                uint32_t val = m_FreeIDs.back();
                m_FreeIDs.pop_back();
                return val;
            }
            return m_NextID++;
        }

        inline void ensure_size(uint32_t tgSize)
        {
            uint32_t size = tgSize;
            if (size <= m_Size)
            {
                return;
            }
            
            ++m_Generation;

            // TODO: allocation strategy?
            entry* next = m_Allocator.allocate(size);
            memset(next, 0, sizeof(entry) * size);
            for (uint32_t i = 0; i < m_Size; ++i)
            {
                auto& tg = next[i];
                auto& from = m_Data[i];
                tg.generation = from.generation;
                if ((tg.occupied = from.occupied) == true)
                {
                    new (&tg.value) value_t(std::move(from.value));
                }

                if (!std::is_trivially_destructible<value_t>())
                {
                    if (from.occupied)
                    {
                        from.value.~value_t();
                    }
                }
            }

            if (m_Data)
            {
                m_Allocator.deallocate(m_Data, m_Size);
            }

            m_Size = size;
            m_Data = next;
        }
    
    // Iterators
    public:
        class iterator
        {
        public:
            inline iterator(slot_map& map, uint32_t index) : m_Map(map), m_Index(index) {}
            inline iterator(const iterator& rhs) : m_Map(rhs.m_Map), m_Index(rhs.m_Index) {}

            inline bool operator==(const iterator& rhs)
            {
                return rhs.m_Index == m_Index && &rhs.m_Map == &m_Map;
            }

            inline bool operator!=(const iterator& rhs)
            {
                return rhs.m_Index != m_Index || &rhs.m_Map != &m_Map;
            }

            inline T* operator->()
            {
                return m_Map.get(m_Index);
            }

            inline T* operator*()
            {
                return m_Map.get(m_Index);
            }

            inline iterator operator++()
            {
                ++m_Index;
                while (m_Index < m_Size && !is_index_resident(m_Index))
                {
                    ++m_Index;
                }
                return *this;
            }

            inline iterator operator++(int)
            {
                auto old = *this;
                ++*this;
                return old;
            }

        private:
            slot_map& m_Map;
            uint32_t m_Index;
        };

        class const_iterator
        {
        public:
            inline const_iterator(const slot_map& map, uint32_t index) : m_Map(map), m_Index(index) {}
            inline const_iterator(const const_iterator& rhs) : m_Map(rhs.m_Map), m_Index(rhs.m_Index) {}

            inline bool operator==(const const_iterator& rhs) const
            {
                return rhs.m_Index == m_Index && &rhs.m_Map == &m_Map;
            }

            inline bool operator!=(const const_iterator& rhs) const
            {
                return rhs.m_Index != m_Index || &rhs.m_Map != &m_Map;
            }

            inline const T* operator->() const
            {
                return m_Map.get(m_Index);
            }

            inline const T* operator*() const
            {
                return m_Map.get(m_Index);
            }

            inline const_iterator operator++()
            {
                ++m_Index;
                while (m_Index < m_Size && !is_index_resident(m_Index))
                {
                    ++m_Index;
                }
                return *this;
            }

            inline const_iterator operator++(int)
            {
                auto old = *this;
                ++*this;
                return old;
            }

        private:
            const slot_map& m_Map;
            uint32_t m_Index;
        };

        inline iterator begin()
        {
            uint32_t index = 0;
            while (!is_index_resident(index) && index < m_Size)
            {
                ++index;
            }
            return iterator(*this, index);
        }

        inline iterator end()
        {
            return iterator(*this, m_Size);
        }

        inline const_iterator begin() const
        {
            uint32_t index = 0;
            while (!is_index_resident(index) && index < m_Size)
            {
                ++index;
            }
            return const_iterator(*this, index);
        }

        inline const_iterator end() const
        {
            return const_iterator(*this, m_Size);
        }

        class caching_handle
        {
        public:
            inline caching_handle() : map(0), handle(0) {}

            inline caching_handle(slot_map& map, const handle_t& handle) : map(&map), handle(handle) {
                update();
            }

            inline caching_handle(slot_map& map, uint64_t handle) : map(&map), handle(handle) {
                update();
            }

            inline caching_handle(const caching_handle& rhs) : cached(rhs.cached), map(rhs.map), handle(rhs.handle), generation(rhs.generation) {}

            inline caching_handle& operator=(const caching_handle& rhs)
            {
                cached = rhs.cached;
                map = rhs.map;
                handle = rhs.handle;
                generation = rhs.generation;
            }

            inline T* operator->()
            {
                return get();
            }

            inline T* operator*()
            {
                return get();
            }

            inline T* get()
            {
                if (generation != map->m_Generation) { update(); }
                return cached;
            }

            inline bool is_valid()
            {
                return map && map->is_valid(handle);
            }

        private:
            void update()
            {
                cached = map->get(handle);
                generation = map->m_Generation;
            }

        private:
            T* cached;
            slot_map* map;
            handle_t handle;
            int generation;
        };

        inline caching_handle get_handle(const handle_t& handle)
        {
            return caching_handle(*this, handle);
        }

        inline caching_handle get_handle(const uint64_t& handle)
        {
            return caching_handle(*this, handle);
        }

    private:
        entry* m_Data;
        std::mutex m_Mutex;
        std::vector<uint32_t> m_FreeIDs;
        uint32_t m_Size;
        uint32_t m_NextID;
        allocator_t m_Allocator;
        int m_Generation;
    };
}");
                }
            }
        }

        public async Task GenerateResourceManager()
        {
            using (var header = CreateHeader("gl_resource_manager.hpp"))
            {
                using (var writer = new StreamWriter(header))
                {
                    await writer.WriteAsync(@"#pragma once
#include <stdint.h>
#include <glad/glad.h>
#include ""gl_slotmap.hpp""


#define CREATE_GL_RESOURCE_HANDLE(name, resourceType) \
struct name##Handle \
{ \
    inline name##Handle() \
        : handle_value(0) \
    { \
    } \
    inline name##Handle(uint32_t ind) \
        : index(ind) \
        , generation(0) \
    { \
    } \
    inline name##Handle(uint64_t ind) \
        : handle_value(ind) \
    { \
    } \
    union { \
        struct \
        { \
            uint32_t generation; \
            uint32_t index; \
        }; \
        uint64_t handle_value; \
    }; \
 \
    inline bool operator==(const name##Handle& rhs) \
    { \
        return handle_value == rhs.handle_value; \
    } \
 \
    inline bool operator!=(const name##Handle& rhs) \
    { \
        return handle_value != rhs.handle_value; \
    } \
    using resource_type = resourceType;\
};

#define CREATE_GL_RESOURCE_MANAGER(name) slot_map<name##Handle::resource_type, name##Handle> name##s

namespace multigl
{
    CREATE_GL_RESOURCE_HANDLE(Buffer, GLuint);
    CREATE_GL_RESOURCE_HANDLE(Texture, GLuint);
    CREATE_GL_RESOURCE_HANDLE(Shader, GLuint);
    CREATE_GL_RESOURCE_HANDLE(ShaderProgram, GLuint);
    CREATE_GL_RESOURCE_HANDLE(VertexArray, GLuint);
    CREATE_GL_RESOURCE_HANDLE(Renderbuffer, GLuint);
    CREATE_GL_RESOURCE_HANDLE(Framebuffer, GLuint);

    struct ResourceManager
    {
        CREATE_GL_RESOURCE_MANAGER(Buffer);
        CREATE_GL_RESOURCE_MANAGER(Texture);
        CREATE_GL_RESOURCE_MANAGER(Shader);
        CREATE_GL_RESOURCE_MANAGER(ShaderProgram);
        CREATE_GL_RESOURCE_MANAGER(VertexArray);
        CREATE_GL_RESOURCE_MANAGER(Renderbuffer);
        CREATE_GL_RESOURCE_MANAGER(Framebuffer);
    };
}

#undef CREATE_GL_RESOURCE_MANAGER
#undef CREATE_RESOURCE_MANAGER");
                }
            }
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

        #endregion
    }
}
