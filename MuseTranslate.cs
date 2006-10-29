using System;
using System.IO;
using System.Web.UI;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace MuseTranslate
{
	public class Block
	{
		public bool Quoted;
		public bool Verse;
		public bool ListItem;
		public bool Numbered;
		public bool Tied;
		public bool Initial;	// only has meaning for paragraphs
		public int? Footnote;
		public int? Heading;

		public List<string> InputLines = new List<string>();
		public List<string> Lines = new List<string>();
		public List<string> Sentences;

		public static Regex footnoteRe = new Regex("^\\[([0-9]+)\\]\\s+");
		public static Regex headingRe  = new Regex("^(\\*+)\\s+");
		public static Regex listItemRe = new Regex("^\\s+(-|[0-9]\\.)\\s+");
		public static Regex verseTagRe = new Regex("^(\\s*)<verse>");
		public static Regex verseRe	   = new Regex("^(\\s+)?>\\s+");
		public static Regex quoteRe	   = new Regex("^\\s+");
		public static Regex sentEndRe  = new Regex("\\<(pp?|Drs?|Mrs?|Ms)?\\.\\s+[A-Z0-9`'\"]");
		public static Regex endingFnRe = new Regex("\\[[0-9]+\\]$");

		public void Validate()
		{
			if (! Verse)
				Debug.Assert(Lines.Count == 1);
		}

		public enum InlineStyle {
			Plain,
			DoubleQuote,
			Emphasis,
			Strong,
			StrongEmphasis,
			Literal,
			Underlined
		}

		public void Identify(Block previous, bool convertQuotes, bool allVerse)
		{
			if (Lines.Count == 0)
				return;

			if (allVerse) {
				Verse = Tied = true;
			}
			else if (previous != null && previous.Tied) {
				Verse = previous.Verse;
				Tied  = true;
			}

			if (Lines[0].Trim() == "Footnotes:" ||
				Lines[0].Trim() == "<contents>") {
				Lines.RemoveAt(0);
				if (Lines.Count == 0)
					return;
			}

			int trimLength = 0;

			Match m = verseTagRe.Match(Lines[0]);
			if (m.Success) {
				Lines.RemoveAt(0);
				if (Lines.Count == 0)
					return;
				Verse = true;
				Tied  = true;
				trimLength = m.Groups[1].Length;
				if (trimLength > 0)
					Quoted = true;
			}

			if (Tied && Lines[Lines.Count - 1].Trim() == "</verse>") {
				Tied = false;
				Lines.RemoveAt(Lines.Count - 1);
			}

			m = footnoteRe.Match(Lines[0]);
			if (m.Success) {
				Footnote = Int32.Parse(m.Groups[1].Value);
				Lines[0] = Lines[0].Substring(m.Groups[0].Length);
				trimLength = -1;
			}

			if (! m.Success && ! Verse) {
				m = headingRe.Match(Lines[0]);
				if (m.Success) {
					Heading = m.Groups[1].Length;
					trimLength = m.Groups[0].Length;
				}
			}

			if (! m.Success && ! Verse) {
				m = listItemRe.Match(Lines[0]);
				if (m.Success) {
					ListItem = true;
					if (m.Groups[1].Value != "-")
						Numbered = true;
					trimLength = m.Groups[0].Length;
				}
			}

			if (! m.Success && ! Verse) {
				m = verseRe.Match(Lines[0]);
				if (m.Success) {
					Verse = true;
					if (m.Groups[1].Success)
						Quoted = true;
					trimLength = m.Groups[0].Length;
				}
			}

			if (! m.Success && ! Verse) {
				m = quoteRe.Match(Lines[0]);
				if (m.Success) {
					Quoted = true;
					trimLength = m.Groups[0].Length;
				}
			}

			// Is this an initial paragraph?
			if ((previous == null || previous.Heading.HasValue) &&
				(! Quoted && ! Verse && ! ListItem &&
				 ! Footnote.HasValue && ! Heading.HasValue))
				Initial = true;

			// Check whether this is a continuation of a single quote
			if (Quoted && previous != null && previous.Quoted) {
				// Right now the only real way I can tell if a quote is broken is whether
				// the previous paragraph ends in a footnote reference -- jww (2006-02-12)
				string prevLastLine = previous.Lines[previous.Lines.Count - 1];
				if (! endingFnRe.IsMatch(prevLastLine))
					previous.Tied = true;
			}

			for (int i = 0; i < Lines.Count; i++) {
				if (trimLength > 0)
					Lines[i] = Lines[i].Substring(trimLength).TrimEnd(null);
				else if (trimLength == -1)
					Lines[i] = Lines[i].Trim(null);
				else
					Lines[i] = Lines[i].TrimEnd(null);
			}

			if (! Verse) {
				string merged = String.Join(" ", Lines.ToArray());
				Lines.Clear();
				Lines.Add(merged);
			}

			for (int i = 0; i < Lines.Count; i++) {
				Lines[i] = Regex.Replace(Lines[i], "<br>", "  \n");

				StringBuilder newLine = new StringBuilder();

				Stack<InlineStyle> currentStyle = new Stack<InlineStyle>();
				currentStyle.Push(InlineStyle.Plain);

				int length = Lines[i].Length;
				int bracketDepth = 0;
				for (int j = 0; j < length; j++) {
					switch (Lines[i][j]) {
					case '[':
						newLine.Append(Lines[i][j]);
						bracketDepth++;
						break;
					case ']':
						newLine.Append(Lines[i][j]);
						bracketDepth--;
						break;

					case '=':
						if (bracketDepth > 0) {
							newLine.Append(Lines[i][j]);
							break;
						}
						if (currentStyle.Peek() == InlineStyle.Literal) {
							newLine.Append("`");
							currentStyle.Pop();
						} else {
							newLine.Append("`");
							currentStyle.Push(InlineStyle.Literal);
						}
						break;

					default:
						newLine.Append(Lines[i][j]);
						break;
					}
				}
				Lines[i] = newLine.ToString();
			}

			// If this is not a quote, we are interested in breaking down sentences.
			if (! Quoted) {
				if (Verse) {
					Sentences = Lines;
				} else {
					Sentences = new List<string>();
					int startPos = 0;
					m = sentEndRe.Match(Lines[0]);
					while (m.Success) {
						if (! m.Groups[1].Success)
							Sentences.Add(Lines[0].Substring(startPos, (m.Groups[0].Index + 1) -
															 startPos));
						startPos = m.Groups[0].Index + (m.Groups[0].Length - 1);
						m = sentEndRe.Match(Lines[0], startPos);
					}
					Sentences.Add(Lines[0].Substring(startPos));
				}
			}
		}
	}

	public abstract class Element {}

	public class Heading : Element {
		public string Title;
		public int    Depth;

		public Heading() {}
		public Heading(int Depth) {
			this.Depth = Depth;
		}
	}

	public class Paragraph : Element {
		public Block block;
	}

	public class Verse : Element {
		public List<Block> blocks = new List<Block>();
	}

	public class Quote : Element {
		public List<Element> elements = new List<Element>();
	}

	public class Footnote : Element {
		public int	 Index;
		public Block Text;
	}

	public class Reader
	{
		public static List<Block> Parse(TextReader reader, bool convertQuotes,
										bool allVerse)
		{
			List<Block> blocks = new List<Block>();
			Block prevBlock = null;
			Block block = new Block();

			bool first = true;
			string line = reader.ReadLine();
			while (line != null) {
				if (line.Length == 0) {
					if (block.Lines.Count > 0) {
						if (! (first == true && block.Lines[0][0] == '#'))
							block.Identify(prevBlock, convertQuotes, allVerse);
						first = false;
						if (block.Lines.Count > 0) {
							blocks.Add(block);
							prevBlock = block;
						}
						block = new Block();
					}
				} else {
					block.InputLines.Add(line);
					block.Lines.Add(line);
				}
				line = reader.ReadLine();
			}

			if (block.Lines.Count > 0) {
				block.Identify(prevBlock, convertQuotes, allVerse);
				if (block.Lines.Count > 0)
					blocks.Add(block);
			}

			return blocks;
		}

		public static List<Element> Assemble(List<Block> blocks)
		{
			Block previous		= null;
			Verse previousVerse = null;
			Quote previousQuote = null;

			List<Element> elements = new List<Element>();

			foreach (Block block in blocks) {
				Element element = null;

				if (block.Verse) {
					if (previous != null && previous.Tied && previous.Verse) {
						Debug.Assert(previousVerse != null);
						previousVerse.blocks.Add(block);
					} else {
						previousVerse = new Verse();
						previousVerse.blocks.Add(block);
						element = previousVerse;
					}
				}
				else if (block.Heading.HasValue) {
					Heading head = new Heading();
					Debug.Assert(block.Lines.Count == 1);
					head.Title = block.Lines[0];
					head.Depth = block.Heading.Value;
					element = head;
				}
				else if (block.Footnote.HasValue) {
					Footnote fn = new Footnote();
					fn.Text	 = block;
					fn.Index = block.Footnote.Value;
					element = fn;
				}
				else {
					Paragraph para = new Paragraph();
					para.block = block;
					element = para;
				}

				bool added = false;

				if (block.Quoted && element != null) {
					if (previous != null && previous.Tied && previous.Quoted) {
						Debug.Assert(previousQuote != null);
						previousQuote.elements.Add(element);
						added = true;
					} else {
						previousQuote = new Quote();
						previousQuote.elements.Add(element);
						element = previousQuote;
					}
				}

				if (! added && element != null)
					elements.Add(element);

				previous = block;
			}

			return elements;
		}
	}

	public class Document
	{
		public string    Title;
		public string    Moniker;
		public string    Location;
		public string    Author;
		public string    Category;
		public DateTime? Written;
		public DateTime? Edited;
		public DateTime? Posted;
		public bool      Pending;

		public List<Block>   Source	 = new List<Block>();
		public List<Element> Content = new List<Element>();
		public List<string>  Sentences;

		public Document() {}
		public Document(string Moniker) {
			this.Moniker = Moniker;
		}

		public void OutputText(TextWriter writer)
		{
			bool first = true;
			foreach (Block block in Source) {
				if (first)
					first = false;
				else
					writer.WriteLine();

				foreach (string line in block.InputLines)
					writer.WriteLine(line);
			}
		}

		public void Output(TextWriter writer)
		{
			if (Title != null) {
				writer.Write("Title: ");
				writer.WriteLine(Title);
			}
			if (Posted.HasValue) {
				writer.Write("Date: ");
				writer.WriteLine(Posted.Value.ToShortDateString());
			}
			if (Author != null) {
				writer.Write("Author: ");
				writer.WriteLine(Author);
			}
			if (Pending) {
				writer.WriteLine("Pending: true");
			}
			if (Written.HasValue) {
				writer.Write("Written: ");
				writer.WriteLine(Written.Value.ToShortDateString());
			}
			if (Edited.HasValue) {
				writer.Write("Edited: ");
				writer.WriteLine(Edited.Value.ToShortDateString());
			}
			if (Location != null) {
				writer.Write("Location: ");
				writer.WriteLine(Location);
			}

			OutputText(writer);
		}

		private static Regex headerTag = new Regex("^#([a-z]+)(\\s+(.+))?");

		internal bool ParseHeader(string line)
		{
			Match m = headerTag.Match(line);
			if (! m.Success)
				return false;

			string tag = m.Groups[1].Value;
			string arg = m.Groups[3].Value;

			switch (tag) {
			case "title":
				Title = arg;
				break;
			case "date":
				try { Posted = DateTime.Parse(arg); }
				catch (Exception) {
					Console.WriteLine("Failed to read date '" + arg + "'");
				}
				break;
			case "author":
				Author = arg;
				break;
			case "pending":
				Pending = true;
				break;
			case "written":
				try { Written = DateTime.Parse(arg); }
				catch (Exception) {
					Console.WriteLine("Failed to read date '" + arg + "'");
				}
				break;
			case "edited":
				try { Edited = DateTime.Parse(arg); }
				catch (Exception) {
					Console.WriteLine("Failed to read date '" + arg + "'");
				}
				break;
			case "location":
				Location = arg;
				break;
			}

			return true;
		}

		public bool debug;

		public void Parse(TextReader reader, bool ignoreHeaders,
						  bool convertQuotes, bool allVerse)
		{
			List<Block> blocks = Reader.Parse(reader, convertQuotes, allVerse);
			if (blocks.Count == 0)
				return;

			if (blocks[0].Lines[0][0] == '#') {
				if (! ignoreHeaders)
					foreach (string line in blocks[0].InputLines)
						ParseHeader(line);
				blocks.RemoveAt(0);
			}
			if (blocks.Count == 0)
				return;

			// Patch up the first paragraph, in case we didn't mark it Initial because of
			// the header block.
			if (! blocks[0].Quoted && ! blocks[0].Verse && ! blocks[0].ListItem &&
				! blocks[0].Footnote.HasValue && ! blocks[0].Heading.HasValue)
				blocks[0].Initial = true;

			if (blocks[blocks.Count - 1].Lines[0][0] == '#') {
				if (! ignoreHeaders)
					foreach (string line in blocks[blocks.Count - 1].InputLines)
						ParseHeader(line);
				blocks.RemoveAt(blocks.Count - 1);
			}
			if (blocks.Count == 0)
				return;

			foreach (Block b in blocks) {
				b.Validate();

				if (b.Sentences != null) {
					if (Sentences == null)
						Sentences = new List<string>();

					foreach (string line in b.Sentences)
						Sentences.Add(line);
					b.Sentences = null;
				}
			}

			if (debug)
				Console.WriteLine();

			Source  = blocks;
			Content = Reader.Assemble(Source);
		}

		public void ReadFromFile(string path, bool ignoreHeaders, bool allVerse) {
			using (StreamReader reader = new StreamReader(path, Encoding.UTF8))
				Parse(reader, ignoreHeaders, false, allVerse);
		}
	}

	public class MultiMarkdownWriter
	{
		public static void Write(string line, TextWriter w)
		{
			// jww (2006-02-24): How to optimize these?

			line = Regex.Replace(line, "\\[([0-9]+)\\]", "[^${1}]");
			line = Regex.Replace(line, "\\[\\[([^]]+)\\]\\]", "<${1}>");
			line = Regex.Replace(line, "\\[\\[([^]]+)\\]\\[([^]]+)\\]\\]", "[${2}](${1})");
			w.Write(line);
		}

		public static void Write(Heading heading, TextWriter w)
		{
			for (int i = 0; i < heading.Depth; i++)
				w.Write("#");
			w.Write(" ");
			w.WriteLine(heading.Title);
			w.WriteLine();
		}

		public static void Write(Paragraph para, TextWriter w)
		{
			Debug.Assert(para.block.Lines.Count == 1);
			Write(para.block.Lines[0], w);
			w.WriteLine();
			w.WriteLine();
		}

		public static Regex initialSpace = new Regex("^ +");

		public static void Write(Verse verse, TextWriter w)
		{
			bool first = true;
			foreach (Block block in verse.blocks) {
				if (first)
					first = false;
				else
					w.WriteLine("> ");

				int len = block.Lines.Count;
				for (int i = 0; i < len; i++) {
					string line = block.Lines[i];

					Match m = initialSpace.Match(line);
					if (m.Success) {
						line = line.TrimStart();
						string prefix = "";
						for (int j = 0; j < m.Groups[0].Length; j++)
							prefix = "  " + prefix;
						line = prefix + line;
					}

					w.Write("> ");
					Write(line, w);
					w.WriteLine("  ");
				}
			}
			w.WriteLine();
		}

		public static void Write(Quote quote, TextWriter w)
		{
			// This is done literally because I can't stop TextWriter from indenting the
			// inner text.
			bool first = true;
			foreach (Element element in quote.elements) {
				if (first)
					first = false;
				else
					w.WriteLine("> ");

				Paragraph para = element as Paragraph;
				if (para != null) {
					w.Write("> ");
					Write(para.block.Lines[0], w);
					w.WriteLine();
				} else {
					Write(element, w);
				}
			}
			w.WriteLine();
		}

		public static void Write(Footnote fn, TextWriter w)
		{
			w.Write("[^" + fn.Index.ToString() + "]: ");

			foreach (string line in fn.Text.Lines)
				Write(line, w);
			w.WriteLine();
			w.WriteLine();
		}

		public static void Write(Element elem, TextWriter w)
		{
			Paragraph para = elem as Paragraph;
			if (para != null) {
				Write(para, w);
				return;
			}

			Quote quot = elem as Quote;
			if (quot != null) {
				Write(quot, w);
				return;
			}

			Verse vers = elem as Verse;
			if (vers != null) {
				Write(vers, w);
				return;
			}

			Heading head = elem as Heading;
			if (head != null) {
				Write(head, w);
				return;
			}

			Footnote foot = elem as Footnote;
			if (foot != null) {
				Write(foot, w);
				return;
			}

			throw new Exception("Cannot render unknown element type " +
								elem.GetType().Name);
		}

		public static void Write(List<Element> elements, TextWriter w) {
			Write(elements, w, 0);
		}
		public static void Write(List<Element> elements, TextWriter w,
								 int paragraphCount)
		{
			// Render the whole document if paragraphCount is 0.
			if (paragraphCount == 0) {
				foreach (Element element in elements)
					Write(element, w);
			} else {
				foreach (Element element in elements) {
					if (paragraphCount-- > 0) {
						Write(element, w);
						if (element as Heading != null)
							paragraphCount++;
					} else {
						break;
					}
				}
			}
  		}

		public static void RenderDocument(Document doc, TextWriter w) {
			RenderDocument(doc, w, 0);
		}
		public static void RenderDocument(Document doc, TextWriter w,
										  int paragraphCount)
		{
			Write(doc.Content, w, paragraphCount);
		}
	}

	public class Convert
	{
		public static string ToMultiMarkdown(Document doc)
		{
			using (StringWriter sw = new StringWriter()) {
				WriteMultiMarkdown(doc, sw);
				return sw.GetStringBuilder().ToString();
			}
		}

		public static string ToMultiMarkdown(string input)
		{
			using (StringWriter sw = new StringWriter()) {
				WriteMultiMarkdown(input, sw);
				return sw.GetStringBuilder().ToString();
			}
		}

		public static string ToMultiMarkdown(TextReader input)
		{
			using (StringWriter sw = new StringWriter()) {
				WriteMultiMarkdown(input, sw);
				return sw.GetStringBuilder().ToString();
			}
		}

		public static void WriteMultiMarkdown(Document doc, TextWriter writer) {
			WriteMultiMarkdown(doc, writer, 0);
		}
		public static void WriteMultiMarkdown(Document doc, TextWriter writer,
									 int paragraphCount)
		{
			MultiMarkdownWriter.RenderDocument(doc, writer, paragraphCount);
		}

		public static void WriteMultiMarkdown(string input, TextWriter writer)
		{
			StringReader sr = new StringReader(input);
			WriteMultiMarkdown(sr, writer);
		}

		public static void WriteMultiMarkdown(TextReader input, TextWriter writer)
		{
			List<Block>	  blocks   = Reader.Parse(input, false, false);
			List<Element> elements = Reader.Assemble(blocks);

			MultiMarkdownWriter.Write(elements, writer);
		}

		public static int Main(string[] args)
		{
			Document doc = new Document();
			doc.ReadFromFile(args[0], false, false);

			FileInfo info = new FileInfo(args[1]);
			if (info.Exists)
				info.Delete();

			info.Directory.Create();

			using (FileStream fs = info.OpenWrite())
				using (StreamWriter sw = new StreamWriter(fs, Encoding.UTF8))
					sw.Write(ToMultiMarkdown(doc));
			
			return 0;
		}
	}
}
