using System;
using System.Web;
using System.Diagnostics;
using System.Xml;
using System.Text;

namespace OpenMarkdown
{
	public class XhtmlWriter
	{
		public OpenMarkdown Document;

		public bool UseClasses = true;

		public XhtmlWriter(OpenMarkdown doc) {
			this.Document = doc;
		}

		public void WriteDocumentTo(XmlWriter writer)
		{
			writer.WriteDocType("html", "-//W3C//DTD XHTML 1.1//EN", null, null);

			writer.WriteStartElement("html");
			writer.WriteAttributeString("xmlns", "http://www.w3.org/1999/xhtml");

			writer.WriteStartElement("head");
			if (Document.Metadata.ContainsKey("Title")) {
				writer.WriteStartElement("title");
				writer.WriteString(Document.Metadata["Title"].ToString());
				writer.WriteEndElement();
			}
			writer.WriteStartElement("meta");
			writer.WriteAttributeString("http-equiv", "ContentType");
			writer.WriteAttributeString("context", "text/xhtml; charset=utf-8");
			writer.WriteEndElement();
			writer.WriteEndElement(); // head

			writer.WriteStartElement("body");
			WriteTo(writer);
			writer.WriteEndElement(); // body

			writer.WriteEndElement(); // html
		}

		public void WriteTo(XmlWriter writer)
		{
			WriteNode(Document.TopElement.LastChild, writer, true);

			bool first = true;

			XmlNode node = Document.TopElement.FirstChild;
			if (node.Name != "header")
				return;
			
			foreach (XmlNode child in node.ChildNodes) {
				if (child.Name != "notes")
					continue;
				
				foreach (XmlNode note in child.ChildNodes) {
					if (first) {
						writer.WriteStartElement("hr");
						writer.WriteEndElement();
						writer.WriteStartElement("dl");
						if (UseClasses)
							writer.WriteAttributeString("class", "notelist");
						first = false;
					}
					
					writer.WriteStartElement("dt");
					if (UseClasses)
						writer.WriteAttributeString("class", "notekey");
					writer.WriteStartElement("a");
					if (UseClasses)
						writer.WriteAttributeString("class", "notedef");
					writer.WriteAttributeString("name", "fn." +
												note.Attributes["id"].Value);
					writer.WriteString(note.Attributes["id"].Value);
					writer.WriteEndElement();
					writer.WriteEndElement();

					writer.WriteStartElement("dd");
					if (UseClasses)
						writer.WriteAttributeString("class", "notebody");
					WriteOnlyChildren(note, writer);
					writer.WriteEndElement();
				}
			}

			if (! first)
				writer.WriteEndElement(); // dl
		}

		public void WriteChildren(XmlNode node, XmlWriter writer)
		{
			foreach (XmlAttribute attr in node.Attributes)
				attr.WriteTo(writer);

			WriteOnlyChildren(node, writer);
		}

		public void WriteOnlyChildren(XmlNode node, XmlWriter writer)
		{
			bool first = true;
			foreach (XmlNode child in node.ChildNodes) {
				WriteNode(child, writer, first);

				if (child.Name[0] == 'h' &&
					char.IsDigit(child.Name[1]))
					first = true;
				else
					first = false;
			}
		}

		public void WriteNode(XmlNode node, XmlWriter writer, bool first)
		{
			XmlText textNode;

			switch (node.Name) {
			case "p":
				writer.WriteStartElement(node.Name);
				if (UseClasses) {
					if (node.Attributes["verse"] != null)
						writer.WriteAttributeString("class", "verse");
					else if (first)
						writer.WriteAttributeString("class", "first");
				}
				WriteOnlyChildren(node, writer);
				writer.WriteEndElement();
				break;

			case "emstrong":
				writer.WriteStartElement("strong");
				writer.WriteStartElement("em");
				WriteChildren(node, writer);
				writer.WriteEndElement();
				writer.WriteEndElement();
				break;

			case "wikilink":
				writer.WriteStartElement("a");
				if (UseClasses)
					writer.WriteAttributeString("class", "wikilink");

				StringBuilder sb = new StringBuilder();
				foreach (XmlText txt in node.ChildNodes)
					sb.Append(txt.Value);

				string href = String.Format(Document.Config.WikiLinkFormat,
											sb.ToString());
				writer.WriteAttributeString("href", href);
				WriteOnlyChildren(node, writer);
				writer.WriteEndElement();
				break;

			case "email":
				textNode = node.FirstChild as XmlText;
				if (textNode != null) {
					writer.WriteStartElement("a");
					if (UseClasses)
						writer.WriteAttributeString("class", "email");
					writer.WriteAttributeString("href", "mailto:" + textNode.Value);
					textNode.WriteTo(writer);
					writer.WriteEndElement();
				}
				break;

			case "hlink":
				textNode = node.FirstChild as XmlText;
				if (textNode != null) {
					writer.WriteStartElement("a");
					if (UseClasses)
						writer.WriteAttributeString("class", "hlink");
					writer.WriteAttributeString("href", textNode.Value);
					textNode.WriteTo(writer);
					writer.WriteEndElement();
				}
				break;

			case "linkref":
				string key = node.Attributes["key"].Value;

				string xref = null;
				if (Document.Headers.ContainsKey(key))
					xref = Document.Headers[key];

				if (xref != null) {
					writer.WriteStartElement("a");
					if (UseClasses)
						writer.WriteAttributeString("class", "xref");
					writer.WriteAttributeString("href", "#" + key);
					writer.WriteAttributeString("title", xref);

					WriteOnlyChildren(node, writer);

					writer.WriteEndElement();
				}

				if (! Document.Links.ContainsKey(key)) {
					node.WriteTo(writer);
					break;
				}
				
				LinkData link = Document.Links[key];
				if (! link.IsDefined) {
					writer.WriteString("[");
					writer.WriteString(link.Text);
					writer.WriteString("][");
					if (link.Text != link.Ident)
						writer.WriteString(link.Ident);
					writer.WriteString("]");
					return;
				}

				writer.WriteStartElement(link.IsImage ? "img" : "a");
				if (UseClasses)
					writer.WriteAttributeString("class", link.IsImage ?
												"image" : "link");
				writer.WriteAttributeString(link.IsImage ? "src" : "href",
											link.Url);
				if (! string.IsNullOrEmpty(link.Title))
					writer.WriteAttributeString("title", link.Title);

				if (link.IsImage) {
					StringBuilder sbi = new StringBuilder();
					foreach (XmlText txt in node.ChildNodes)
						sbi.Append(txt.Value);
					writer.WriteAttributeString("alt", sbi.ToString());
				} else {
					WriteOnlyChildren(node, writer);
				}

				writer.WriteEndElement();
				break;

			case "fnref":
				writer.WriteStartElement("a");
				if (UseClasses)
					writer.WriteAttributeString("class", "fnref");
				writer.WriteAttributeString("href", "#fn." +
											node.Attributes["key"].Value);
				writer.WriteString("[" + node.Attributes["key"].Value + "]");
				writer.WriteEndElement();
				break;

			case "space":
				writer.WriteEntityRef("nbsp");
				break;

			case "eos":
				writer.WriteEntityRef("nbsp");
				writer.WriteEntityRef("nbsp");
				break;

			case "hyphen":
				break;

			case "ldq":
				writer.WriteEntityRef("#8220");
				break;
			case "rdq":
				writer.WriteEntityRef("#8221");
				break;
			case "lsq":
				writer.WriteEntityRef("#8216");
				break;
			case "rsq":
				writer.WriteEntityRef("#8217");
				break;
			case "ellipsis":
				writer.WriteEntityRef("#8230");
				break;
			case "emdash":
				writer.WriteEntityRef("nbsp");
				writer.WriteEntityRef("#8212");
				writer.WriteEntityRef("nbsp");
				break;
			case "endash":
				writer.WriteEntityRef("#8211");
				break;

			case "body":
			case "sect":
				WriteOnlyChildren(node, writer);
				break;

			case "h1":
			case "h2":
			case "h3":
			case "h4":
			case "h5":
			case "h6":
			case "blockquote":
			case "br":
			case "hr":
			case "pre":
			case "li":
			case "ol":
			case "ul":
			case "dl":
			case "dt":
			case "dd":
			case "em":
			case "strong":
			case "code":
			case "tt":
			case "u":
				writer.WriteStartElement(node.Name);
				WriteChildren(node, writer);
				writer.WriteEndElement();
				break;

			default:
				node.WriteTo(writer);
				break;
			}
		}
	}
}
