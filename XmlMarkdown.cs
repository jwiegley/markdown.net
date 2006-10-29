using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace XmlMarkdown
{
	public class Configuration
	{
		public bool   PlainMarkdown;
		public bool   UseSmartyPants;
		public bool   UseWikiLinks;
		public bool   TechnicalStyle;
		public string WikiLinkFormat = "{0}";

		public enum SmartyDashes {
			DoubleEmdashNoEndash,
			TripleEmdashDoubleEndash,
			DoubleEmdashTripleEndash
		}

		public SmartyDashes DashesStyle = SmartyDashes.DoubleEmdashTripleEndash;
		public bool SpacesAroundDashes = true;
	}

	public class LinkData
	{
		public string Id;
		public string Ident;
		public string Url;
		public string Title;
		public string Text;
		public bool   IsImage;
		public bool   IsDefined;
	}
	
	public class XmlMarkdown
	{
		public static string XmlMarkdownNS = "http://www.firstlightsoftware.com";

		public static Regex headingRe  = new Regex("^(#+)\\s+(.+?)(\\s+#+)?\\s*$");
		public static Regex uheadingRe = new Regex("^[=-]+\\s*$");
		public static Regex rulerRe    =
			new Regex("^ ? ? ?(-\\s*-\\s*-(\\s*-)*|\\*\\s*\\*\\s*\\*(\\s*\\*)*|_\\s*_\\s*_(\\s*_)*)\\s*$");
		public static Regex listItemRe = new Regex("^ ? ? ?([*+-]|[1-9][0-9]*\\.)\\s+");
		public static Regex quoteRe	   = new Regex("^ ? ? ?>( |$)");
		public static Regex spaceRe	   = new Regex("^(    |\t)");
		public static Regex entityRe   = new Regex("^&([#0-9A-Za-z]+);");
		public static Regex urlRe      = new Regex("^<([a-z]+://[^>]+)>");
		public static Regex emailRe    = new Regex("^<([a-z_0-9.-]+@[^>]+)>");
		public static Regex htmlTagRe  = new Regex("^<(!--|/?[a-z0-9]+(\\s+[a-z0-9]+=|( ?/)?>))");
		public static Regex smartyRe   = new Regex("[\"'`.-]");
		public static Regex metaDataRe = new Regex("^([A-Za-z0-9 _/-]+):\\s+");
		public static Regex fnRefRe    = new Regex("^\\[\\^([^]]+)\\]");
		public static Regex freeLinkRe = new Regex("^\\[\\[(.+?)\\]\\]");
		public static Regex linkDefRe  =
			new Regex("^( ? ? ?\\[([^]]+)\\]:\\s*)(?:(\\S+)(\\s+\"([^\"]+)\")?)?");
		public static Regex linkRe     =
			new Regex("^\\[(.+?)\\](\\(([^ \t\")]+)?(\\s*\"(.+?)\")?\\)|\\s*\\[([^]]*)\\])");
		//public static Regex sentEndRe  =
		//  new Regex("\\<(pp?|Drs?|Mrs?|Ms)?\\.\\s+[A-Z0-9`'\"]");

		private TextReader Reader;
		private int		   LinkIndex;
		private bool	   FirstBlock;

		public XmlDocument   Document;
		public XmlElement    TopElement;
		public XmlElement    HeaderElement;
		public Configuration Config;

		public Dictionary<string, string>	Headers;
		public Dictionary<string, object>   Metadata;
		public Dictionary<string, LinkData> Links;

		public List<XmlNode> Footnotes;

		private Stack<string>  lines;
		private Stack<XmlNode> blocks;

		public XmlMarkdown(Configuration Config) {
			this.Config = Config;
			Reset();
		}

		private void PushNull() {
			lines.Push(null);
		}
		private void PopNull() {
			if (lines.Count > 0 && lines.Peek() == null)
				lines.Pop();
		}

		private string NextLine() {
			if (lines.Count > 0) {
				if (lines.Peek() == null)
					return null;
				return lines.Pop();
			}
			return Reader.ReadLine();
		}
		private void PushLine(string line) {
			lines.Push(line);
		}

		private string NextBlockLine()
		{
			string line = NextLine();
			while (line != null && line.Trim().Length == 0)
				line = NextLine();
			return line;
		}

		private XmlNode NextBlock(XmlNode context) {
			if (blocks.Count > 0)
				return blocks.Pop();
			return ReadBlock(context);
		}
		private void PushBlock(XmlNode block) {
			blocks.Push(block);
		}

		public enum BlockKind {
			Null,
			Document,
			Paragraph,
			Quotation,
			Footnote,
			Literal,
			Separator,
			Verse,
			EnumeratedList,
			ItemizedList,
			ListItem,
			DefinitionList,
			DefinitionTag,
			DefinitionItem,
			Heading,
			Section,
		}

		public static BlockKind KindOf(XmlNode elem)
		{
			switch (elem.Name) {
			case "p":
				return BlockKind.Paragraph;
			case "blockquote":
			    return BlockKind.Quotation;
			case "note":
			    return BlockKind.Footnote;
			case "hr":
			    return BlockKind.Separator;
			case "pre":
			    return BlockKind.Literal;
			case "li":
			    return BlockKind.ListItem;
			case "ol":
			    return BlockKind.EnumeratedList;
			case "ul":
			    return BlockKind.ItemizedList;
			case "dl":
			    return BlockKind.DefinitionList;
			case "dt":
			    return BlockKind.DefinitionTag;
			case "dd":
			    return BlockKind.DefinitionItem;
			case "fn":
			    return BlockKind.Footnote;
			case "h1":
			case "h2":
			case "h3":
			case "h4":
			case "h5":
			case "h6":
			    return BlockKind.Heading;
			case "sect":
			    return BlockKind.Section;
			default:
				return BlockKind.Null;
			}
		}

		public XmlElement CreateElement(string tag)
		{
			return Document.CreateElement(tag);
		}

		public XmlElement NewBlock(BlockKind kind)
		{
			string tag = null;
			switch (kind) {
			case BlockKind.Paragraph:
				tag = "p";
				break;
			case BlockKind.Quotation:
				tag = "blockquote";
				break;
			case BlockKind.Footnote:
				tag = "note";
				break;
			case BlockKind.Separator:
				tag = "hr";
				break;
			case BlockKind.Literal:
			case BlockKind.Verse:
				tag = "pre";
				break;
			case BlockKind.ListItem:
				tag = "li";
				break;
			case BlockKind.EnumeratedList:
				tag = "ol";
				break;
			case BlockKind.ItemizedList:
				tag = "ul";
				break;
			case BlockKind.DefinitionList:
				tag = "dl";
				break;
			case BlockKind.DefinitionTag:
				tag = "dt";
				break;
			case BlockKind.DefinitionItem:
				tag = "dd";
				break;
			case BlockKind.Section:
				tag = "sect";
				break;
			default:
				return null;
			}
			
			return CreateElement(tag);
		}

		public enum InlineKind {
			NotInline,
			Emphasis,
			Strong,
			StrongEmphasis,
			Literal,
			Monospace,
			Underline,
			Strikeout,
			Strikeover
		}

		public XmlElement NewInline(InlineKind kind)
		{
			string tag = null;
			switch (kind) {
			case InlineKind.Emphasis:
				tag = "em";
				break;
			case InlineKind.Strong:
				tag = "strong";
				break;
			case InlineKind.StrongEmphasis:
				tag = "emstrong";
				break;
			case InlineKind.Literal:
				tag = "code";
				break;
			case InlineKind.Monospace:
				tag = "tt";
				break;
			case InlineKind.Underline:
				tag = "u";
				break;
			case InlineKind.Strikeout:
			case InlineKind.Strikeover:
			default:
				return null;
			}
			
			return CreateElement(tag);
		}

		public static InlineKind KindOfInline(XmlNode elem)
		{
			switch (elem.Name) {
			case "em":
				return InlineKind.Emphasis;
			case "strong":
			    return InlineKind.Strong;
			case "emstrong":
			    return InlineKind.StrongEmphasis;
			case "code":
			    return InlineKind.Literal;
			case "tt":
			    return InlineKind.Monospace;
			case "u":
			    return InlineKind.Underline;
			//    return InlineKind.Strikeout;
			//    return InlineKind.Strikeover;
			default:
				return InlineKind.NotInline;
			}
		}

		public enum SpecialKind {
			HardReturn,
			UnbreakableSpace,
			HyphenationClue,
			OpenDoubleQuote,
			CloseDoubleQuote,
			OpenSingleQuote,
			CloseSingleQuote,
			Ellipsis,
			Emdash,
			Endash
		}

		public XmlElement NewSpecial(SpecialKind kind)
		{
			string tag = null;
			switch (kind) {
			case SpecialKind.HardReturn:
				tag = "br";
				break;
			case SpecialKind.UnbreakableSpace:
				tag = "space";
				break;
			case SpecialKind.HyphenationClue:
				tag = "hyphen";
				break;
			case SpecialKind.OpenDoubleQuote:
				tag = "ldq";
				break;
			case SpecialKind.CloseDoubleQuote:
				tag = "rdq";
				break;
			case SpecialKind.OpenSingleQuote:
				tag = "lsq";
				break;
			case SpecialKind.CloseSingleQuote:
				tag = "rsq";
				break;
			case SpecialKind.Ellipsis:
				tag = "ellipsis";
				break;
			case SpecialKind.Emdash:
				tag = "emdash";
				break;
			case SpecialKind.Endash:
				tag = "endash";
				break;
			default:
				return null;
			}
			
			return CreateElement(tag);
		}

		public void Reset()
		{
			Document = null;
			Reader			= null;

			LinkIndex  = 0;
			FirstBlock = true;

			lines  = new Stack<string>();
			blocks = new Stack<XmlNode>();

			Headers  = new Dictionary<string, string>();
			Metadata = new Dictionary<string, object>();
			Links    = new Dictionary<string, LinkData>();

			Footnotes = new List<XmlNode>();
		}

		public static XmlMarkdown Parse(TextReader reader)
		{
			return Parse(reader, new Configuration());
		}
		public static XmlMarkdown Parse(TextReader reader, Configuration Config)
		{
			XmlMarkdown MMD = new XmlMarkdown(Config);
			MMD.Reader = reader;

			XmlDocument doc = new XmlDocument();
			MMD.Document = doc;
			MMD.TopElement = doc.CreateElement("markdown");
			doc.AppendChild(MMD.TopElement);
			XmlElement body = doc.CreateElement("body");
			MMD.TopElement.AppendChild(body);
			MMD.Parse(body);

			MMD.InsertMetadata();
			MMD.InsertLinks();
			MMD.InsertFootnotes();

			return MMD;
		}

		private void Parse(XmlNode context)
		{
			for (XmlNode block = NextBlock(context);
				 block != null;
				 block = NextBlock(context))
				context.AppendChild(block);
		}

		private List<string> ReadListItem()
		{
			string line = NextLine();
			if (line == null)
				return null;

			Match m = listItemRe.Match(line);
			if (! m.Success)
				return null;

			List<string> lines = new List<string>();

			lines.Add(line.Substring(m.Groups[0].Value.Length));

			// The remaining lines may be indented by 4 spaces or a tab.

			line = NextLine();
			while (line != null) {
				if (line.Trim().Length == 0) {
					List<string> saveLines = new List<string>();

					// If an indented block (again, by 4 spaces or a tab)
					// follows this paragraph, it continues the list item.

					saveLines.Add(line);

					line = NextLine();
					while (line != null && line.Trim().Length == 0) {
						saveLines.Add(line);
						line = NextLine();
					}
					if (line == null)
						break;

					m = spaceRe.Match(line);
					if (! m.Success) {
						saveLines.Add(line);

						saveLines.Reverse();
						foreach (string saved in saveLines)
							PushLine(saved);
						break;
					}
					lines.Add("");
				}

				string origLine = line;

				m = spaceRe.Match(line);
				if (m.Success)
					line = line.Substring(m.Groups[0].Value.Length);

				// Another list item ends this one (including an indented one)
				m = listItemRe.Match(line);
				if (m.Success) {
					PushLine(origLine);
					break;
				}

				lines.Add(line);

				line = NextLine();
			}

			return lines;
		}

		private List<string> ReadFootnote()
		{
			string line = NextLine();
			if (line == null)
				return null;

			Match m = linkDefRe.Match(line);
			if (! m.Success)
				return null;

			List<string> lines = new List<string>();

			lines.Add(line.Substring(m.Groups[1].Value.Length));

			// The remaining lines may be indented by 4 spaces or a tab.

			line = NextLine();
			while (line != null) {
				if (line.Trim().Length == 0) {
					List<string> saveLines = new List<string>();

					// If an indented block (again, by 4 spaces or a tab)
					// follows this paragraph, it continues the list item.

					saveLines.Add(line);

					line = NextLine();
					while (line != null && line.Trim().Length == 0) {
						saveLines.Add(line);
						line = NextLine();
					}
					if (line == null)
						break;

					m = spaceRe.Match(line);
					if (! m.Success) {
						saveLines.Add(line);

						saveLines.Reverse();
						foreach (string saved in saveLines)
							PushLine(saved);
						break;
					}
					lines.Add("");
				}

				// Another link definition ends this one
				m = linkDefRe.Match(line);
				if (m.Success) {
					PushLine(line);
					break;
				}

				m = spaceRe.Match(line);
				if (m.Success)
					line = line.Substring(m.Groups[0].Value.Length);

				lines.Add(line);

				line = NextLine();
			}

			return lines;
		}

		public static void StripChildNodes(XmlNode node)
		{
			if (node.Attributes.Count > 0) {
				List<XmlAttribute> attrs = new List<XmlAttribute>();
				foreach (XmlAttribute attr in node.Attributes)
					attrs.Add(attr);

				node.RemoveAll();

				foreach (XmlAttribute attr in attrs)
					node.Attributes.SetNamedItem(attr);
			} else {
				node.RemoveAll();
			}
		}

		private void StripListItem(XmlElement listItem)
		{
			if (listItem.ChildNodes != null &&
				listItem.ChildNodes.Count > 0) {
				XmlNode subElem = listItem.ChildNodes[0];
				if (subElem != null &&
					KindOf(subElem) == BlockKind.Paragraph) {
					listItem.RemoveChild(listItem.FirstChild);

					List<XmlNode> newElements = new List<XmlNode>();
					foreach (XmlNode elem in subElem.ChildNodes)
						newElements.Add(elem);
					foreach (XmlNode elem in listItem.ChildNodes)
						newElements.Add(elem);

					StripChildNodes(listItem);

					foreach (XmlNode elem in newElements)
						listItem.AppendChild(elem);
				}
			}
		}

		private string NextListItemLine(XmlElement listItem, out bool immediate)
		{
			int blanks = 0;

			immediate = false;

			string line = NextLine();
			while (line != null && line.Trim().Length == 0) {
				blanks++;
				line = NextLine();
			}
			if (line == null)
				return null;

			// Sometimes a ruler can look like a list item!
			Match m = rulerRe.Match(line);
			if (m.Success) {
				PushLine(line);
				return null;
			}

			string origLine = line;

			m = spaceRe.Match(line);
			if (m.Success)
				line = line.Substring(m.Groups[0].Value.Length);

			m = listItemRe.Match(line);
			if (! m.Success) {
				PushLine(origLine);
				return null;
			}

			// We may have a nested list...
			if (line != origLine) {
				PushLine(origLine);
				List<string> ilines = ReadIndented();

				PushNull();
				ilines.Reverse();
				foreach (string l in ilines)
					PushLine(l);

				Parse(listItem);
				PopNull();

				line = NextListItemLine(listItem, out immediate);
			} else {
				immediate = blanks == 0;
			}

			return line;
		}

		private List<string> ReadQuotation()
		{
			List<string> lines = new List<string>();

			string line = NextLine();
			while (line != null) {
				if (line.Trim().Length == 0) {
					PushLine(line);
					break;
				}

				Match m = quoteRe.Match(line);
				if (m.Success)
					lines.Add(line.Substring(m.Groups[0].Value.Length));
				else
					lines.Add(line);

				line = NextLine();
			}

			return lines;
		}

		private List<string> ReadIndented()
		{
			List<string> lines = new List<string>();

			string line = NextLine();
			while (line != null) {
				if (line.Trim().Length == 0) {
					PushLine(line);
					break;
				}

				Match m = spaceRe.Match(line);
				if (m.Success) {
					lines.Add(line.Substring(m.Groups[0].Value.Length));
				} else {
					PushLine(line);
					break;
				}

				line = NextLine();
			}

			return lines;
		}

		private List<string> ReadLiteralBlock()
		{
			List<string> lines = new List<string>();

			Match m;

			string line = NextLine();
			while (line != null) {
				if (line.Trim().Length == 0) {
					List<string> saveLines = new List<string>();

					// If an indented block (again, by 4 spaces or a tab)
					// follows this paragraph, it continues the code block.

					saveLines.Add(line);

					line = NextLine();
					while (line != null && line.Trim().Length == 0) {
						saveLines.Add(line);
						line = NextLine();
					}
					if (line == null)
						break;

					m = spaceRe.Match(line);
					if (! m.Success) {
						saveLines.Add(line);

						saveLines.Reverse();
						foreach (string saved in saveLines)
							PushLine(saved);
						break;
					}
					lines.Add("");
				}

				m = spaceRe.Match(line);
				if (m.Success)
					line = line.Substring(m.Groups[0].Value.Length);

				lines.Add(ExpandTabs(line));

				line = NextLine();
			}

			return lines;
		}

		private string ReadParagraph()
		{
			StringBuilder sb = new StringBuilder();

			string line = NextLine();
			while (line != null) {
				if (line.Trim().Length == 0) {
					PushLine(line);
					break;
				}

				if (sb.Length > 0)
					sb.Append("\n");

				sb.Append(line);

				line = NextLine();
			}

			return sb.ToString();
		}

		private string ReduceString(string title)
		{
			StringBuilder sb = new StringBuilder();

			for (int i = 0; i < title.Length; i++) {
				char c = title[i];
				if (char.IsLetter(c) || char.IsDigit(c) ||
					c == ':' || c == '_' || c == '-' || c == '#')
					sb.Append(char.ToLower(c));
			}

			return sb.ToString();
		}

		private XmlElement ReadSection(string title, int depth)
		{
			XmlElement section = NewBlock(BlockKind.Section);
			section.SetAttribute("depth", depth.ToString());

			XmlElement heading = CreateElement("h" + depth.ToString());
			string simpleTitle = ReduceString(title);
			heading.SetAttribute("id", simpleTitle);
			Headers[simpleTitle] = title;
			ParseBlock(title, heading);
			section.AppendChild(heading);

			for (XmlNode block = NextBlock(section);
				 block != null;
				 block = NextBlock(section)) {
				if (KindOf(block) == BlockKind.Section &&
					Convert.ToInt32(block.Attributes["depth"].Value) <= depth) {
					PushBlock(block);
					break;
				}
				section.AppendChild(block);
			}
			return section;
		}

		public void InsertMetadata()
		{
			if (Metadata.Count == 0)
				return;

			XmlElement metadata = CreateElement("metadata");
			if (HeaderElement == null) {
				HeaderElement = CreateElement("header");
				TopElement.PrependChild(HeaderElement);
			}
			HeaderElement.PrependChild(metadata);

			foreach (string key in Metadata.Keys)
				InsertMetadata(metadata, key, Metadata[key].ToString());
		}

		public void InsertMetadata(XmlElement metadata, string key, string value)
		{
			XmlElement itemElement = CreateElement("item");
			itemElement.SetAttribute("id", key);
			XmlText valueTextNode = Document.CreateTextNode(value);
			itemElement.AppendChild(valueTextNode);
			metadata.AppendChild(itemElement);
		}

		public void InsertLinks()
		{
			if (Links.Count == 0)
				return;

			XmlElement links = CreateElement("links");
			if (HeaderElement == null) {
				HeaderElement = CreateElement("header");
				TopElement.PrependChild(HeaderElement);
			}
			HeaderElement.PrependChild(links);

			foreach (string key in Links.Keys)
				InsertLink(links, Links[key]);
		}

		public void InsertLink(XmlElement links, LinkData link)
		{
			XmlElement itemElement = CreateElement("link");
			itemElement.SetAttribute("id", link.Ident);
			if (link.Url != null)
				itemElement.SetAttribute("url", link.Url);
			if (link.Title != null)
				itemElement.SetAttribute("title", link.Title);
			if (link.IsImage)
				itemElement.SetAttribute("image", "true");
			if (link.Text != null) {
				XmlText valueTextNode = Document.CreateTextNode(link.Text);
				itemElement.AppendChild(valueTextNode);
			}
			links.AppendChild(itemElement);
		}

		public void InsertFootnotes()
		{
			if (Footnotes.Count == 0)
				return;

			XmlElement notes = CreateElement("notes");
			if (HeaderElement == null) {
				HeaderElement = CreateElement("header");
				TopElement.PrependChild(HeaderElement);
			}
			HeaderElement.PrependChild(notes);

			foreach (XmlNode note in Footnotes)
				notes.AppendChild(note);
		}

		public void SetMetadata(string key, object value)
		{
			Metadata[key] = value;

			switch (key) {
			case "Style":
				if (value.ToString() == "technical")
					Config.TechnicalStyle = true;
				break;

			case "Use WikiLinks":
				Config.UseSmartyPants = true;
				Config.UseWikiLinks   = Convert.ToBoolean(value.ToString());
				break;

			case "Base Url":
				Config.WikiLinkFormat = value.ToString();
				break;
			}
		}

		// This routine parses a single block, of possibly nested structure
		private XmlNode ReadBlock(XmlNode context)
		{
			string line = NextBlockLine();
			if (line == null)
				return null;

			//
			// Link definition
			//

			Match m = linkDefRe.Match(line);
			if (m.Success) {
				if (! Config.PlainMarkdown && m.Groups[2].Value[0] == '^') {
					PushLine(line);
					List<string> lines = ReadFootnote();

					// Seeding quoted lines
					PushNull();
					lines.Reverse();
					foreach (string l in lines)
						PushLine(l);

					// Read them back as blocks
					XmlElement note = NewBlock(BlockKind.Footnote);
					Parse(note);
					PopNull();

					string key = m.Groups[2].Value.Substring(1);
					note.SetAttribute("id", key);
					Footnotes.Add(note);
				} else {
					DefineLink(m.Groups[3].Value, m.Groups[5].Value,
							   m.Groups[2].Value, true, null);
				}
				return ReadBlock(context); // recurse, basically ignoring this text
			}

			//
			// Quotation
			//

			m = quoteRe.Match(line);
			if (m.Success) {
				// Read quotation lines
				PushLine(line);
				List<string> lines = ReadQuotation();

				// Seeding quoted lines
				PushNull();
				lines.Reverse();
				foreach (string l in lines)
					PushLine(l);

				// Read them back as blocks
				XmlElement quotation = NewBlock(BlockKind.Quotation);
				Parse(quotation);
				PopNull();

				return quotation;
			}

			//
			// Literal (code block)
			//

			m = spaceRe.Match(line);
			if (m.Success) {
				if (Config.TechnicalStyle) {
					PushLine(line);
					List<string> lines = ReadLiteralBlock();

					// Read them back as blocks
					XmlElement codeBlock = NewBlock(BlockKind.Literal);
					XmlElement codeSpan  = NewInline(InlineKind.Literal);
					foreach (string l in lines)
						codeSpan.AppendChild(Document.CreateTextNode(l + "\n"));
					codeBlock.AppendChild(codeSpan);

					return codeBlock;
				} else {
					// Read quotation lines
					PushLine(line);
					List<string> lines = ReadIndented();

					// Seeding quoted lines
					PushNull();
					lines.Reverse();
					foreach (string l in lines)
						PushLine(l);

					// Read them back as blocks
					XmlElement quotation = NewBlock(BlockKind.Quotation);
					Parse(quotation);
					PopNull();

					return quotation;
				}
			}

			//
			// Horizontal ruler
			//

			m = rulerRe.Match(line);
			if (m.Success)
				return NewBlock(BlockKind.Separator);

			//
			// Itemized or enumerated List
			//

			m = listItemRe.Match(line);
			if (m.Success) {
				string leader		= m.Groups[1].Value;
				bool   numeric		= char.IsDigit(m.Groups[1].Value[0]);
				XmlElement  listing		= NewBlock(numeric ? BlockKind.EnumeratedList :
												BlockKind.ItemizedList);
				bool   hadImmediate = false;
				XmlElement  lastItem		= null;

				do {
					// If the entry begins another list time, unread the list
					// item and leave
					if (numeric != char.IsDigit(m.Groups[1].Value[0]) ||
						(! numeric && leader != m.Groups[1].Value))
						break;

					PushLine(line);
					List<string> lines = ReadListItem();

					PushNull();
					lines.Reverse();
					foreach (string l in lines)
						PushLine(l);

					XmlElement listItem = NewBlock(BlockKind.ListItem);
					Parse(listItem);
					PopNull();

					listing.AppendChild(listItem);
					lastItem = listItem;

					bool immediate;
					line = NextListItemLine(listItem, out immediate);
					if (line == null)
						break;

					// If the list item is followed immediately by another list item (save
					// for the last one), then strip the single paragraph from within the
					// item.
					if (immediate) {
						StripListItem(listItem);
						hadImmediate = true;
					}
				} while (true);

				if (hadImmediate ||
					(listing.ChildNodes != null && listing.ChildNodes.Count == 1))
					StripListItem(lastItem);

				return listing;
			}

			//
			// Heading
			//

			m = headingRe.Match(line);
			if (m.Success)
				return ReadSection(m.Groups[2].Value, m.Groups[1].Value.Length);

			//
			// Heading in paragraph form
			//

			string following = NextLine();
			if (following != null) {
				m = uheadingRe.Match(following);
				if (m.Success)
					return ReadSection(line, m.Groups[0].Value[0] == '=' ? 1 : 2);
				else
					PushLine(following);
			}

			//
			// Meta data (for XmlMarkdown)
			//

			if (! Config.PlainMarkdown && FirstBlock) {
				if (FirstBlock)
					FirstBlock = false;
				m = metaDataRe.Match(line);
				if (m.Success) {
					PushLine(line);
					string[] lines = ReadParagraph().Split("\n".ToCharArray());

					string currentKey = null;
					StringBuilder currentData = new StringBuilder();
					for (int i = 0; i < lines.Length; i++) {
						string l = lines[i].TrimEnd();
						m = metaDataRe.Match(l);
						if (m.Success) {
							if (currentKey != null) {
								SetMetadata(currentKey, currentData.ToString());
								currentData = new StringBuilder();
							}
							currentKey = m.Groups[1].Value;
							currentData.Append(l.Substring(m.Groups[0].Value.Length));
						} else {
							currentData.Append("\n");
							currentData.Append(l);
						}
					}

					if (currentKey != null)
						SetMetadata(currentKey, currentData.ToString());

					return ReadBlock(context);
				}
			}

			//
			// Paragraph (the default case)
			//

			line = line.TrimStart(' ');
			bool inlineXml = line.StartsWith("<");

			PushLine(line);
			string text = ReadParagraph();
			if (text == null)
				return null;

			if (inlineXml) {
				ParseBlock(text, context);
				return ReadBlock(context);
			} else {
				XmlElement paragraph = NewBlock(BlockKind.Paragraph);
				ParseBlock(text, paragraph);
				return paragraph;
			}
		}

		public LinkData DefineLink(string Url, string Title, string Ident,
								   bool Definition, bool? Image)
		{
			if (! string.IsNullOrEmpty(Url) &&
				Url[0] == '<' && Url[Url.Length - 1] == '>')
				Url = Url.Substring(1, Url.Length - 2);

			string id = Ident;
			if (string.IsNullOrEmpty(id)) {
				id = Ident = "#" + LinkIndex.ToString();
				LinkIndex++;
			} else {
				id = ReduceString(id);
			}				

			LinkData data;
			if (Links.ContainsKey(id)) {
				data = Links[id];
			} else {
				data = new LinkData();
				Links[id] = data;
			}

			data.Id	   = id;
			data.Ident = Ident;

			if (! string.IsNullOrEmpty(Url))
				data.Url = Url;
			if (! string.IsNullOrEmpty(Title))
				data.Title = Title;

			if (Image.HasValue && Image.Value)
				data.IsImage = Image.Value;
			if (Definition)
				data.IsDefined = Definition;

			return data;
		}

		public void AppendText(ref Stack<XmlNode> contextStack,
							ref StringBuilder textBuf)
		{
			if (textBuf.Length == 0)
				return;

			string text = textBuf.ToString();
			textBuf = new StringBuilder();
			
			contextStack.Peek().AppendChild(Document.CreateTextNode(text));
		}

		public static string ExpandTabs(string text)
		{
			StringBuilder sb = new StringBuilder();
			int col = 0;
			for (int i = 0; i < text.Length; i++, col++) {
				if (text[i] == '\t') {
					do {
						sb.Append(" ");
					} while (++col % 4 != 0);
				} else {
					if (text[i] == '\n')
						col = -1;
					sb.Append(text[i]);
				}
			}
			return sb.ToString();
		}

		private void ParseBlock(string text, XmlNode context)
		{
			Stack<XmlNode> contextStack = new Stack<XmlNode>();
			contextStack.Push(context);

			StringBuilder textBuf = new StringBuilder();

			bool sawDoubleTick = false;
			bool codeStyle	   = false;

			Match m;
			char lastChar = '@';
			int column = 0;
			for (int i = 0; i < text.Length; i++, column++) {
				char c = text[i];

				if (codeStyle && c != '`' && c != '\'' && c != '\t') {
					textBuf.Append(c);
					continue;
				}

				// jww (2006-10-24): In the code below, use literal character inline
				// elements instead of entities (and Entity in that case!).

				switch (c) {
				case '\t':
					do {
						textBuf.Append(" ");
					} while (++column % 4 != 0);
					break;

				case '\n':
					column = 0;
					textBuf.Append(c);
					break;

				case ' ':
					if (i + 1 < text.Length && text[i + 1] == ' ' &&
						(i + 2 == text.Length || text[i + 2] == '\n')) {
						i += 2;
						column = 0;

						AppendText(ref contextStack, ref textBuf);

						XmlElement elem = context as XmlElement;
						if (context != null)
							elem.SetAttribute("verse", "true");

						if (i == text.Length)
							break;

						XmlNode top = contextStack.Peek();
						top.AppendChild(NewSpecial(SpecialKind.HardReturn));

						while (i + 1 < text.Length &&
							   (text[i + 1] == ' ' || text[i + 1] == '\t')) {
							if (text[i + 1] == ' ') {
								top.AppendChild(NewSpecial(SpecialKind.UnbreakableSpace));
								column++;
							} else {
								top.AppendChild(NewSpecial(SpecialKind.UnbreakableSpace));
								top.AppendChild(NewSpecial(SpecialKind.UnbreakableSpace));
								top.AppendChild(NewSpecial(SpecialKind.UnbreakableSpace));
								top.AppendChild(NewSpecial(SpecialKind.UnbreakableSpace));
								column += 4;
							}
							i++;
						}
					} else {
						textBuf.Append(c);
					}
					break;

				case '\\':
					if (i + 1 < text.Length) {
						if (Config.UseSmartyPants)
							textBuf.Append(c);
						textBuf.Append(text[++i]);
						break;
					} else {
						textBuf.Append(c);
					}
					break;

				case '<':
					string remainder = text.Substring(i);

					m = htmlTagRe.Match(remainder);
					if (m.Success) {
						AppendText(ref contextStack, ref textBuf);

						// jww (2006-10-27): We add a space so that Mono's
						// ReadNode method won't die while reading comments.
						remainder += " ";

						// Append the HTML element directly, so that it
						// undergoes no further scanning or transformation.
						StringReader sr = new StringReader(remainder);
						XmlNode node;
						try {
							XmlTextReader xtr = new XmlTextReader(sr);
							node = Document.ReadNode(xtr);

							int line  = 1;
							int pos	  = 1;
							int index = 0;
							for (int j = 0; j < remainder.Length; j++) {
								if (line == xtr.LineNumber &&
									pos  == xtr.LinePosition)
									break;

								if (remainder[j] == '\n') {
									line++;
									pos = 1;
								} else {
									pos++;
								}
								index++;
							}

							i += index - 1;
						}
						catch (Exception Ex) {
							bool quoted = false;
							int j;
							for (j = i; j < text.Length; j++) {
								if (text[j] == '\\')
									j++;
								else if (text[j] == '"')
									quoted = ! quoted;
								else if (! quoted && text[j] == '>')
									break;
							}

							string tag = text.Substring(i, (j + 1) - i);
							i = j;

							node = Document.CreateTextNode(tag);
						}
						contextStack.Peek().AppendChild(node);
						break;
					}

					m = emailRe.Match(remainder);
					if (m.Success) {
						AppendText(ref contextStack, ref textBuf);

						XmlElement elem = CreateElement("email");
						XmlText value = Document.CreateTextNode(m.Groups[1].Value);
						elem.AppendChild(value);
						contextStack.Peek().AppendChild(elem);

						i += m.Groups[0].Value.Length - 1;
						break;
					}

					m = urlRe.Match(remainder);
					if (m.Success) {
						AppendText(ref contextStack, ref textBuf);

						XmlElement elem = CreateElement("hlink");
						XmlText value = Document.CreateTextNode(m.Groups[1].Value);
						elem.AppendChild(value);
						contextStack.Peek().AppendChild(elem);

						i += m.Groups[0].Value.Length - 1;
						break;
					}

					textBuf.Append("<");
					break;

				case '&':
					m = entityRe.Match(text.Substring(i));
					if (m.Success) {
						AppendText(ref contextStack, ref textBuf);

						XmlNode entity =
							Document.CreateEntityReference(m.Groups[1].Value);
						contextStack.Peek().AppendChild(entity);
						i += m.Groups[0].Value.Length - 1;
					} else {
						textBuf.Append(c);
					}
					break;

				case '!':
				case '[':
					bool isImage = false;
					if (c == '!') {
						if (i + 1 < text.Length && text[i + 1] == '[') {
							isImage = true;
							c = text[++i];
						} else {
							textBuf.Append(c);
							break;
						}
					}
					else if (Config.UseWikiLinks &&
							 i + 1 < text.Length && text[i + 1] == '[') {
						m = freeLinkRe.Match(text.Substring(i));
						if (m.Success) {
							AppendText(ref contextStack, ref textBuf);

							XmlElement elem = CreateElement("wikilink");
							XmlText value =
								Document.CreateTextNode(m.Groups[1].Value);
							elem.AppendChild(value);
							context.AppendChild(elem);

							i += m.Groups[0].Value.Length;
							break;
						}
					}

					if (! Config.PlainMarkdown) {
						m = fnRefRe.Match(text.Substring(i));
						if (m.Success) {
							AppendText(ref contextStack, ref textBuf);

							XmlElement fnref = CreateElement("fnref");
							fnref.SetAttribute("key", m.Groups[1].Value);
							contextStack.Peek().AppendChild(fnref);

							i += m.Groups[0].Value.Length - 1;
							break;
						}
					}

					m = linkRe.Match(text.Substring(i));
					if (! m.Success) {
						textBuf.Append(c);
						break;
					}

					i += m.Groups[0].Value.Length - 1;

					string desc  = m.Groups[1].Value;
					string url   = m.Groups[3].Value;
					string ident = m.Groups[6].Value;

					bool defined = (! string.IsNullOrEmpty(m.Groups[2].Value) &&
									m.Groups[2].Value[0] == '(');

					if (defined)
						ident = null;
					else if (string.IsNullOrEmpty(ident))
						ident = desc;

					if (! Config.PlainMarkdown && desc[0] == '^')
						desc = desc.Substring(1);

					LinkData data = DefineLink(url, m.Groups[5].Value, ident,
											   defined, isImage);
					data.Text = desc;

					AppendText(ref contextStack, ref textBuf);

					XmlElement link = CreateElement("linkref");
					link.SetAttribute("key", data.Id);
					contextStack.Peek().AppendChild(link);

					// Process the link title, to allow for emphasis, etc.
					ParseBlock(desc, link);
					break;

				case '\'':
					if (sawDoubleTick &&
						i + 1 < text.Length && text[i + 1] == c) {
						XmlNode codeSpan = contextStack.Peek();
						if (KindOfInline(codeSpan) == InlineKind.Literal) {
							contextStack.Pop();
							foreach (XmlNode elem in codeSpan.ChildNodes)
								contextStack.Peek().AppendChild(elem);
							textBuf.Insert(0, "``");
							sawDoubleTick = false;
							codeStyle = false;
						}
					}
					textBuf.Append(c);
					break;

				case '`':
					if (! codeStyle && i > 0 && ! char.IsWhiteSpace(text[i - 1])) {
						textBuf.Append(c);
						break;
					}
					else if (i + 1 < text.Length && text[i + 1] == c) {
						sawDoubleTick = true;
						i++;
					}
					else if (sawDoubleTick || ! Config.TechnicalStyle) {
						textBuf.Append(c);
						break;
					}

					AppendText(ref contextStack, ref textBuf);

					XmlNode codeSpan = contextStack.Peek();
					if (KindOfInline(codeSpan) == InlineKind.NotInline) {
						contextStack.Push(NewInline(InlineKind.Literal));
						codeStyle = true;
					} else {
						contextStack.Pop();
						if (sawDoubleTick && codeSpan.ChildNodes.Count > 0) {
							XmlText txt = codeSpan.ChildNodes[0] as XmlText;
							if (txt != null) {
								if (txt.Value.Length > 0 && txt.Value[0] == ' ')
									txt.Value = txt.Value.Substring(1);
								if (txt.Value.Length > 0 &&
									txt.Value[txt.Value.Length - 1] == ' ')
									txt.Value = txt.Value.Substring(0, txt.Value.Length - 1);
							}
						}
						codeStyle = false;
						contextStack.Peek().AppendChild(codeSpan);
					}
					break;

				case '_':
				case '*':
					XmlNode span = contextStack.Peek();
					if (! char.IsWhiteSpace(lastChar) ||
						(i + 1 < text.Length && char.IsWhiteSpace(text[i + 1]))) {
						textBuf.Append(c);
						break;
					}

					AppendText(ref contextStack, ref textBuf);

					InlineKind style = InlineKind.Emphasis;
					if (i + 1 < text.Length && text[i + 1] == c) {
						i++;
						if (i + 1 < text.Length && text[i + 1] == c) {
							i++;
							style = InlineKind.StrongEmphasis;
						} else {
							style = InlineKind.Strong;
						}
					}

					if (KindOfInline(span) == InlineKind.NotInline) {
						contextStack.Push(NewInline(style));
					} else {
						contextStack.Pop();
						contextStack.Peek().AppendChild(span);
					}
					break;

				default:
					textBuf.Append(c);
					break;
				}
				lastChar = c;
			}

			AppendText(ref contextStack, ref textBuf);

			// See if there is anything left over.  This can happen if a `
			// appears with a closing match.
			while (contextStack.Peek() != context) {
				XmlNode node = contextStack.Pop();
				contextStack.Peek().AppendChild(node);
			}

			if (Config.UseSmartyPants)
				SmartyPants.Transform(this, context);
		}

		public static XmlMarkdown ReadFromFile(string path, Encoding code,
												 Configuration config) {
			using (StreamReader reader = new StreamReader(path, code))
				return Parse(reader, config);
		}
		public static XmlMarkdown ReadFromFile(string path, Configuration config) {
			return ReadFromFile(path, Encoding.UTF8, config);
		}

		public static XmlMarkdown ReadFromFile(string path, Encoding code) {
			return ReadFromFile(path, code, new Configuration());
		}
		public static XmlMarkdown ReadFromFile(string path) {
			return ReadFromFile(path, new Configuration());
		}

		public static string ToXml(string text) {
			return ToXml(text, new Configuration());
		}
		public static string ToXml(string text, bool useSmartyPants) {
			Configuration config = new Configuration();
			config.UseSmartyPants = useSmartyPants;
			return ToXml(text, config);
		}
		public static string ToXml(string text, Configuration config)
		{
			using (StringReader reader = new StringReader(text)) {
				XmlMarkdown doc = Parse(reader, config);
				StringWriter sw = new StringWriter();
				XmlTextWriter xw = new XmlTextWriter(sw);
				doc.Document.WriteTo(xw);
				return sw.ToString();
			}
		}

		public static string ToIndentedXml(string text) {
			return ToIndentedXml(text, new Configuration());
		}
		public static string ToIndentedXml(string text, bool useSmartyPants) {
			Configuration config = new Configuration();
			config.UseSmartyPants = useSmartyPants;
			return ToIndentedXml(text, config);
		}
		public static string ToIndentedXml(string text, Configuration config)
		{
			using (StringReader reader = new StringReader(text)) {
				XmlMarkdown doc = Parse(reader, config);
				StringWriter sw = new StringWriter();
				XmlTextWriter xw = new XmlTextWriter(sw);
				xw.Formatting = Formatting.Indented;
				doc.Document.WriteTo(xw);
				return sw.ToString();
			}
		}

		public static string ToXhtml(string text) {
			return ToXhtml(text, new Configuration());
		}
		public static string ToXhtml(string text, bool useSmartyPants) {
			Configuration config = new Configuration();
			config.UseSmartyPants = useSmartyPants;
			return ToXhtml(text, config);
		}
		public static string ToXhtml(string text, Configuration config)
		{
			using (StringReader reader = new StringReader(text)) {
				XmlMarkdown doc = Parse(reader, config);
				XhtmlWriter xhw = new XhtmlWriter(doc);
				StringWriter sw = new StringWriter();
				XmlTextWriter xw = new XmlTextWriter(sw);
				xhw.WriteTo(xw);
				return sw.ToString();
			}
		}

		public static string ToIndentedXhtml(string text) {
			return ToIndentedXhtml(text, new Configuration());
		}
		public static string ToIndentedXhtml(string text, bool useSmartyPants) {
			Configuration config = new Configuration();
			config.UseSmartyPants = useSmartyPants;
			return ToIndentedXhtml(text, config);
		}
		public static string ToIndentedXhtml(string text, Configuration config)
		{
			using (StringReader reader = new StringReader(text)) {
				XmlMarkdown doc = Parse(reader, config);
				XhtmlWriter xhw = new XhtmlWriter(doc);
				StringWriter sw = new StringWriter();
				XmlTextWriter xw = new XmlTextWriter(sw);
				xw.Formatting = Formatting.Indented;
				xhw.WriteTo(xw);
				return sw.ToString();
			}
		}

		public static int Main(string[] arguments) {
			bool outputXhtml	= true;
			bool indented		= false;
			bool wholeDocument	= false;
			bool useSmartyPants = false;

			string outputFile = null;

			List<string> args = new List<string>();
			foreach (string arg in arguments)
				args.Add(arg);

			while (args.Count > 0 && args[0][0] == '-') {
				switch (args[0]) {
				case "-x":
					outputXhtml = false;
					indented = true;
					break;
				case "-i":
					outputXhtml = true;
					indented = true;
					break;
				case "-h":
					outputXhtml = true;
					indented = true;
					wholeDocument = true;
					break;
				case "-o":
					outputFile = args[1];
					args.RemoveAt(0);
					break;
				case "-s":
					useSmartyPants = true;
					break;
				}
				args.RemoveAt(0);
			}

			Configuration config = new Configuration();
			config.UseSmartyPants = useSmartyPants;

			XmlMarkdown doc = XmlMarkdown.ReadFromFile(args[0], config);

			TextWriter outStream = Console.Out;

			if (outputFile != null) {
				FileInfo info = new FileInfo(outputFile);
				if (info.Exists)
					info.Delete();

				info.Directory.Create();

				FileStream fs = info.OpenWrite();
				StreamWriter sw = new StreamWriter(fs, Encoding.UTF8);
				outStream = sw;
			}

			XmlTextWriter xw = new XmlTextWriter(outStream);
			if (indented)
				xw.Formatting = Formatting.Indented;

			if (outputXhtml) {
				XhtmlWriter xhw = new XhtmlWriter(doc);
				if (wholeDocument) {
					xhw.WriteDocumentTo(xw);
				} else {
					xhw.UseClasses = false;
					xhw.WriteTo(xw);
				}
			} else {
				xw.WriteStartDocument();
				doc.Document.WriteTo(xw);
			}

			outStream.Close();

			return 0;
		}
	}
}
