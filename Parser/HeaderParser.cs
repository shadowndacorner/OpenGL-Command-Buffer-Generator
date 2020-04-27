using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

namespace GLThreadGen
{
	public struct LineParser
	{
		public LineParser(string l, int lNum)
		{
			Line = l;
			LineNum = lNum;
			CurIdx = 0;
		}

		public string DebugCurrentString
		{
			get
			{
				return Line.Substring(CurIdx);
			}
		}

		public string Line;
		public int CurIdx;
		public int LineNum;

		public static bool IsNewLine(char c)
		{
			return c == '\n';
		}

		public static bool IsWhitespace(char c)
		{
			// TODO: Add more here
			return c == ' ' || c == '\t' || c == '\r';
		}

		public static bool IsAlpha(char c)
		{
			return c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z';
		}

		public static bool IsNumeric(char c)
		{
			return c >= '0' && c <= '9';
		}

		public static bool IsAlphaNumeric(char c)
		{
			return IsAlpha(c) || IsNumeric(c);
		}

		public static bool IsValidIdentifierCharacter(char c)
		{
			return IsAlphaNumeric(c) || c == '_';
		}

		public char ReadChar()
		{
			return Line[CurIdx++];
		}

		public char PeekChar()
		{
			return Line[CurIdx];
		}

		private bool IsEOF()
		{
			return CurIdx >= Line.Length;
		}

		public void SkipWhitespace()
		{
			while (IsWhitespace(PeekChar()))
			{
				ReadChar();
			}
		}

		public void SkipNextToken()
		{
			SkipWhitespace();

			int start = CurIdx;
			int len = 1;
			char c = ReadChar();
			if (!IsAlpha(c))
			{
				throw new ArgumentException("Invalid token");
			}

			while (!IsEOF())
			{
				c = ReadChar();
				if (!IsValidIdentifierCharacter(c))
				{
					break;
				}
				++len;
			}
		}

		public bool IsSymbol(char sym)
		{
			SkipWhitespace();

			var c = PeekChar();
			if (sym == c)
			{
				c = ReadChar();
				return true;
			}
			return false;
		}

		public string ReadToken()
		{
			SkipWhitespace();

			int start = CurIdx;
			int len = 1;
			char c = ReadChar();
			if (!IsAlpha(c) && c != '_')
			{
				throw new ArgumentException("Invalid token - got char " + c + " for line " + Line);
			}

			while (!IsEOF())
			{
				c = ReadChar();
				if (!IsValidIdentifierCharacter(c))
				{
					--CurIdx;
					break;
				}
				++len;
			}

			return Line.Substring(start, len);
		}

		public string ReadUntil(char endChar)
		{
			SkipWhitespace();
			
			int start = CurIdx;
			int len = 0;
			char c;
			while((c = PeekChar()) != endChar)
			{
				ReadChar();
				++len;
			}
			return Line.Substring(start, len).Trim();
		}

		public string ReadUntil(char endChar, char endChar2)
		{
			SkipWhitespace();

			int start = CurIdx;
			int len = 0;
			char c;
			while ((c = PeekChar()) != endChar && c != endChar2)
			{
				ReadChar();
				++len;
			}
			return Line.Substring(start, len).Trim();
		}
	}

	public class GLADHeaderParser
    {
        public GLADHeaderParser(Stream stream, GLDataRegistry registry)
        {
            if (!stream.CanRead)
            {
                throw new ArgumentException("Input stream must be readable");
            }

            Stream = stream;
			Registry = registry;
        }

		public GLDataRegistry Registry { get; private set; }

        public Stream Stream { get; private set; }
		public Dictionary<string, FunctionDeclaration> FunctionTypes
		{
			get

			{
				return Registry.FunctionTypes;
			}
		}

		public Dictionary<string, FunctionEntry> Functions
		{
			get
			{
				return Registry.Functions;
			}
		}

		private FunctionDeclaration ReadFuncType(string line, int curLine)
		{
			var parser = new LineParser(line, curLine);

			// Skip the typedef
			parser.SkipNextToken();

			// Read the return type
			var returnType = parser.ReadUntil('(');
			if (!parser.IsSymbol('('))
			{
				Console.Error.WriteLine($"Failed to parse function type declaration at line {curLine}:{parser.CurIdx} -  Expected (, got {parser.ReadChar()}");
				return null;
			}

			var apiEntry = parser.ReadToken();
			if (apiEntry != "APIENTRYP")
			{
				return null;
			}

			var typedefName = parser.ReadToken();
			if (!parser.IsSymbol(')'))
			{
				Console.Error.WriteLine($"Failed to parse function type declaration for declaration {typedefName} at line {curLine}:{parser.CurIdx} - Expected ), got {parser.ReadChar()}");
				return null;
			}

			if (!parser.IsSymbol('('))
			{
				Console.Error.WriteLine($"Failed to parse function type declaration for declaration {typedefName} at line {curLine}:{parser.CurIdx} - Expected (, got {parser.ReadChar()}");
				return null;
			}

			var funcType = new FunctionDeclaration();
			funcType.TypedefName = typedefName;
			funcType.ReturnType = returnType;
			while (!parser.IsSymbol(')'))
			{
				var argString = parser.ReadUntil(',', ')');
				var arg = new FunctionArgument(argString);
				if (arg.Name != null && arg.Type != null && arg.Name.Length > 0 && arg.Type.Length > 0)
					funcType.Arguments.Add(arg);

				parser.IsSymbol(',');
			}

			FunctionTypes.Add(funcType.TypedefName, funcType);
			return funcType;
		}

		public void Parse()
		{
			int curLine = 1;
			using (var reader = new StreamReader(Stream))
			{
				while (!reader.EndOfStream)
				{
					var line = reader.ReadLine();
					if (line.StartsWith("#define"))
					{
						var parser = new LineParser(line, curLine);
						parser.IsSymbol('#');
						if (parser.ReadToken() != "define")
						{
							throw new Exception("Invalid definition");
						}
						
						var def = parser.ReadToken();
						if (!Registry.Defines.Contains(def))
							Registry.Defines.Add(def);
					}
					else if (line.StartsWith("typedef") && line.Contains("APIENTRYP PFN"))
					{
						ReadFuncType(line, curLine);
					}
					else if (line.StartsWith("GLAPI PFN"))
					{
						var parser = new LineParser(line, curLine);
						
						// Skip GLAPI
						parser.SkipNextToken();
						
						var typeName = parser.ReadToken();
						var funcName = parser.ReadToken();
						if (!funcName.StartsWith("glad_gl"))
						{
							Console.Error.WriteLine($"Failed to read function of type {typeName} at line {curLine} - invallid name");
							continue;
						}

						funcName = funcName.Substring("glad_".Length);

						var type = FunctionTypes[typeName];
						var func = new FunctionEntry();
						func.Type = type;
						func.Name = funcName;

						Functions.Add(funcName, func);
					}
					++curLine;
				}
			}
		}
	}
}
