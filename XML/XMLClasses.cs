   /* 
    Licensed under the Apache License, Version 2.0
    
    http://www.apache.org/licenses/LICENSE-2.0
    */
using System;
using System.Xml.Serialization;
using System.Collections.Generic;
namespace OpenGL_XML_Specification
{
	[XmlRoot(ElementName="type")]
	public class Type {
		[XmlAttribute(AttributeName="name")]
		public string _Name { get; set; }
		[XmlElement(ElementName="name")]
		public string Name { get; set; }
		[XmlText]
		public string Text { get; set; }
		[XmlAttribute(AttributeName="comment")]
		public string Comment { get; set; }
		[XmlAttribute(AttributeName="requires")]
		public string Requires { get; set; }
		[XmlElement(ElementName="apientry")]
		public string Apientry { get; set; }
	}

	[XmlRoot(ElementName="types")]
	public class Types {
		[XmlElement(ElementName="type")]
		public List<Type> Type { get; set; }
	}

	[XmlRoot(ElementName="enum")]
	public class Enum {
		[XmlAttribute(AttributeName="name")]
		public string Name { get; set; }
		[XmlAttribute(AttributeName="value")]
		public string Value { get; set; }
		[XmlAttribute(AttributeName="comment")]
		public string Comment { get; set; }
		[XmlAttribute(AttributeName="alias")]
		public string Alias { get; set; }
		[XmlAttribute(AttributeName="type")]
		public string Type { get; set; }
		[XmlAttribute(AttributeName="api")]
		public string Api { get; set; }
	}

	[XmlRoot(ElementName="group")]
	public class Group {
		[XmlElement(ElementName="enum")]
		public List<Enum> Enum { get; set; }
		[XmlAttribute(AttributeName="name")]
		public string Name { get; set; }
		[XmlAttribute(AttributeName="comment")]
		public string Comment { get; set; }
	}

	[XmlRoot(ElementName="groups")]
	public class Groups {
		[XmlElement(ElementName="group")]
		public List<Group> Group { get; set; }
	}

	[XmlRoot(ElementName="enums")]
	public class Enums {
		[XmlElement(ElementName="enum")]
		public List<Enum> Enum { get; set; }
		[XmlAttribute(AttributeName="namespace")]
		public string Namespace { get; set; }
		[XmlAttribute(AttributeName="group")]
		public string Group { get; set; }
		[XmlAttribute(AttributeName="type")]
		public string Type { get; set; }
		[XmlElement(ElementName="unused")]
		public List<Unused> Unused { get; set; }
		[XmlAttribute(AttributeName="comment")]
		public string Comment { get; set; }
		[XmlAttribute(AttributeName="vendor")]
		public string Vendor { get; set; }
		[XmlAttribute(AttributeName="start")]
		public string Start { get; set; }
		[XmlAttribute(AttributeName="end")]
		public string End { get; set; }
	}

	[XmlRoot(ElementName="unused")]
	public class Unused {
		[XmlAttribute(AttributeName="start")]
		public string Start { get; set; }
		[XmlAttribute(AttributeName="end")]
		public string End { get; set; }
		[XmlAttribute(AttributeName="comment")]
		public string Comment { get; set; }
		[XmlAttribute(AttributeName="vendor")]
		public string Vendor { get; set; }
	}

	[XmlRoot(ElementName="proto")]
	public class Proto {
		[XmlElement(ElementName="name")]
		public string Name { get; set; }
		[XmlElement(ElementName="ptype")]
		public string Ptype { get; set; }
		[XmlAttribute(AttributeName="group")]
		public string Group { get; set; }
	}

	[XmlRoot(ElementName="param")]
	public class Param {
		[XmlElement(ElementName="ptype")]
		public string Ptype { get; set; }
		[XmlElement(ElementName="name")]
		public string Name { get; set; }
		[XmlAttribute(AttributeName="group")]
		public string Group { get; set; }
		[XmlAttribute(AttributeName="len")]
		public string Len { get; set; }
	}

	[XmlRoot(ElementName="glx")]
	public class Glx {
		[XmlAttribute(AttributeName="type")]
		public string Type { get; set; }
		[XmlAttribute(AttributeName="opcode")]
		public string Opcode { get; set; }
		[XmlAttribute(AttributeName="name")]
		public string Name { get; set; }
		[XmlAttribute(AttributeName="comment")]
		public string Comment { get; set; }
	}

	[XmlRoot(ElementName="command")]
	public class Command {
		[XmlElement(ElementName="proto")]
		public Proto Proto { get; set; }
		[XmlElement(ElementName="param")]
		public List<Param> Param { get; set; }
		[XmlElement(ElementName="glx")]
		public List<Glx> Glx { get; set; }
		[XmlElement(ElementName="alias")]
		public Alias Alias { get; set; }
		[XmlElement(ElementName="vecequiv")]
		public Vecequiv Vecequiv { get; set; }
		[XmlAttribute(AttributeName="comment")]
		public string Comment { get; set; }
		[XmlAttribute(AttributeName="name")]
		public string Name { get; set; }
	}

	[XmlRoot(ElementName="alias")]
	public class Alias {
		[XmlAttribute(AttributeName="name")]
		public string Name { get; set; }
	}

	[XmlRoot(ElementName="vecequiv")]
	public class Vecequiv {
		[XmlAttribute(AttributeName="name")]
		public string Name { get; set; }
	}

	[XmlRoot(ElementName="commands")]
	public class Commands {
		[XmlElement(ElementName="command")]
		public List<Command> Command { get; set; }
		[XmlAttribute(AttributeName="namespace")]
		public string Namespace { get; set; }
	}

	[XmlRoot(ElementName="require")]
	public class Require {
		[XmlElement(ElementName="type")]
		public List<Type> Type { get; set; }
		[XmlElement(ElementName="enum")]
		public List<Enum> Enum { get; set; }
		[XmlElement(ElementName="command")]
		public List<Command> Command { get; set; }
		[XmlAttribute(AttributeName="comment")]
		public string Comment { get; set; }
		[XmlAttribute(AttributeName="profile")]
		public string Profile { get; set; }
		[XmlAttribute(AttributeName="api")]
		public string Api { get; set; }
	}

	[XmlRoot(ElementName="feature")]
	public class Feature {
		[XmlElement(ElementName="require")]
		public List<Require> Require { get; set; }
		[XmlAttribute(AttributeName="api")]
		public string Api { get; set; }
		[XmlAttribute(AttributeName="name")]
		public string Name { get; set; }
		[XmlAttribute(AttributeName="number")]
		public string Number { get; set; }
		[XmlElement(ElementName="remove")]
		public List<Remove> Remove { get; set; }
	}

	[XmlRoot(ElementName="remove")]
	public class Remove {
		[XmlElement(ElementName="command")]
		public List<Command> Command { get; set; }
		[XmlAttribute(AttributeName="profile")]
		public string Profile { get; set; }
		[XmlAttribute(AttributeName="comment")]
		public string Comment { get; set; }
		[XmlElement(ElementName="enum")]
		public List<Enum> Enum { get; set; }
	}

	[XmlRoot(ElementName="extension")]
	public class Extension {
		[XmlElement(ElementName="require")]
		public List<Require> Require { get; set; }
		[XmlAttribute(AttributeName="name")]
		public string Name { get; set; }
		[XmlAttribute(AttributeName="supported")]
		public string Supported { get; set; }
		[XmlAttribute(AttributeName="comment")]
		public string Comment { get; set; }
	}

	[XmlRoot(ElementName="extensions")]
	public class Extensions {
		[XmlElement(ElementName="extension")]
		public List<Extension> Extension { get; set; }
	}

	[XmlRoot(ElementName="registry")]
	public class Registry {
		[XmlElement(ElementName="comment")]
		public string Comment { get; set; }
		[XmlElement(ElementName="types")]
		public Types Types { get; set; }
		[XmlElement(ElementName="groups")]
		public Groups Groups { get; set; }
		[XmlElement(ElementName="enums")]
		public List<Enums> Enums { get; set; }
		[XmlElement(ElementName="commands")]
		public Commands Commands { get; set; }
		[XmlElement(ElementName="feature")]
		public List<Feature> Feature { get; set; }
		[XmlElement(ElementName="extensions")]
		public Extensions Extensions { get; set; }
	}

}
