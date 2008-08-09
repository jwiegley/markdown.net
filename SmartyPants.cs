using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Xml;
using System.Text;
using System.Text.RegularExpressions;

namespace OpenMarkdown
{
	public class SmartyPants
	{
		private class Tokenizer
		{
			private string Data;
			private int	   Index;
			private Token  LastToken;

			public class Token
			{
				public enum Kind {
					Backslash,
					SingleDash,
					DoubleDash,
					TripleDash,
					Ellipsis,
					Whitespace,
					UnbreakableSpace,
					DoubleQuote,
					OpenDoubleQuote,
					CloseDoubleQuote,
					SingleQuote,
					BackQuote,
					QuestionMark,
					ExclamationMark,
					Comma,
					Period,
					Semicolon,
					Colon,
					OpenParen,
					CloseParen,
					Text,
					Referral	// token is just a placeholder
				}

				public Kind	   TokenKind;
				public string  Content;
				public XmlNode Referral;

				public Token(Kind TokenKind) {
					this.TokenKind = TokenKind;
				}
				public Token(Kind TokenKind, string Content) {
					this.TokenKind = TokenKind;
					this.Content   = Content;
				}
				public Token(XmlNode Referral) {
					this.TokenKind = Kind.Referral;
					this.Referral  = Referral;
				}

				public void Print() {
					switch (TokenKind) {
					case Kind.Backslash:
						Console.WriteLine("[Backslash]");
						break;
					case Kind.SingleDash:
						Console.WriteLine("[SingleDash]");
						break;
					case Kind.DoubleDash:
						Console.WriteLine("[DoubleDash]");
						break;
					case Kind.TripleDash:
						Console.WriteLine("[TripleDash]");
						break;
					case Kind.Ellipsis:
						Console.WriteLine("[Ellipsis]");
						break;
					case Kind.Whitespace:
						Console.WriteLine("[Whitespace]");
						break;
					case Kind.UnbreakableSpace:
						Console.WriteLine("[UnbreakableSpace]");
						break;
					case Kind.DoubleQuote:
						Console.WriteLine("[DoubleQuote]");
						break;
					case Kind.OpenDoubleQuote:
						Console.WriteLine("[OpenDoubleQuote]");
						break;
					case Kind.CloseDoubleQuote:
						Console.WriteLine("[CloseDoubleQuote]");
						break;
					case Kind.SingleQuote:
						Console.WriteLine("[SingleQuote]");
						break;
					case Kind.BackQuote:
						Console.WriteLine("[BackQuote]");
						break;
					case Kind.QuestionMark:
						Console.WriteLine("[QuestionMark]");
						break;
					case Kind.ExclamationMark:
						Console.WriteLine("[ExclamationMark]");
						break;
					case Kind.Comma:
						Console.WriteLine("[Comma]");
						break;
					case Kind.Period:
						Console.WriteLine("[Period]");
						break;
					case Kind.Semicolon:
						Console.WriteLine("[Semicolon]");
						break;
					case Kind.Colon:
						Console.WriteLine("[Colon]");
						break;
					case Kind.OpenParen:
						Console.WriteLine("[OpenParen]");
						break;
					case Kind.CloseParen:
						Console.WriteLine("[CloseParen]");
						break;
					case Kind.Text:
						Console.WriteLine("[Text: " + Content + "]");
						break;
					}
				}
			}

			public Tokenizer(string Data) {
				this.Data = Data;
			}

			public Token NextToken()
			{
				LastToken = ReadToken();
				return LastToken;
			}

			public bool IsSpecial(char c)
			{
				switch (c) {
				case '\\':
				case '\'':
				case '"':
				case '`':
				case '-':
				case '.':
				case '?':
				case '!':
				case ',':
				case ';':
				case ':':
				case '(':
				case ')':
				case ' ':
				case '\t':
				case '\n':
				case '\r':
					return true;

				default:
					return false;
				}
			}

			public Token ReadToken()
			{
				if (Index >= Data.Length)
					return null;

				int startIndex = Index;

				char c = Data[Index++];
				switch (c) {
				case '\'':
					if (Index < Data.Length && Data[Index] == '\'') {
						Index++;
						return new Token(Token.Kind.CloseDoubleQuote, "''");
					} else {
						return new Token(Token.Kind.SingleQuote, "'");
					}

				case '"':
					return new Token(Token.Kind.DoubleQuote, "\"");

				case '`':
					if (Index < Data.Length && Data[Index] == '`') {
						Index++;
						return new Token(Token.Kind.OpenDoubleQuote, "``");
					} else {
						return new Token(Token.Kind.BackQuote, "`");
					}

				case '-':
					if (Index + 1 < Data.Length &&
						Data[Index + 1] == '-' && Data[Index] == '-') {
						Index += 2;
						return new Token(Token.Kind.TripleDash, "---");
					}
					else if (Index < Data.Length && Data[Index] == '-') {
						Index++;
						return new Token(Token.Kind.DoubleDash, "--");
					}
					else {
						return new Token(Token.Kind.SingleDash, "-");
					}

				case '.':
					if (Index + 4 < Data.Length &&
						Data[Index + 3] == '.' &&
						Data[Index + 2] == ' ' &&
						Data[Index + 1] == '.' &&
						Data[Index] == ' ') {
						Index += 4;
						return new Token(Token.Kind.Ellipsis, ". . .");
					}
					else if (Index + 1 < Data.Length &&
							 Data[Index + 1] == '.' &&
							 Data[Index] == '.') {
						Index += 2;
						return new Token(Token.Kind.Ellipsis, "...");
					}
					else {
						return new Token(Token.Kind.Period, ".");
					}

				case '\\':
					return new Token(Token.Kind.Backslash, "\\");
				case '?':
					return new Token(Token.Kind.QuestionMark, "?");
				case '!':
					return new Token(Token.Kind.ExclamationMark, "!");
				case ',':
					return new Token(Token.Kind.Comma, ",");
				case ';':
					return new Token(Token.Kind.Semicolon, ";");
				case ':':
					return new Token(Token.Kind.Colon, ":");
				case '(':
					return new Token(Token.Kind.OpenParen, "(");
				case ')':
					return new Token(Token.Kind.CloseParen, ")");

				case ' ':
				case '\t':
				case '\n':
				case '\r':
					for (; Index < Data.Length; Index++)
						if (! char.IsWhiteSpace(Data[Index]))
							break;
					return new Token(Token.Kind.Whitespace,
									 Data.Substring(startIndex, Index - startIndex));

				default:
					for (; Index < Data.Length; Index++)
						if (IsSpecial(Data[Index]))
							break;
					return new Token(Token.Kind.Text,
									 Data.Substring(startIndex, Index - startIndex));
				}
			}
		}

		private static Regex wikiLinkRe = new Regex("^[A-Z][a-z]+[A-Z][a-z]+$");
		private static Regex sentEndRe  = new Regex("(^|\\s+)(pp?|Drs?|Mrs?|Ms)\\.$");
		private static Regex capOrDigRe = new Regex("^[A-Z0-9]");

		private static void AppendText(OpenMarkdown doc, XmlNode context,
									   ref StringBuilder accum)
		{
			if (accum.Length > 0) {
				context.AppendChild(doc.Document.CreateTextNode(accum.ToString()));
				accum = new StringBuilder();
			}
		}

		private static void AppendSpecial(OpenMarkdown.SpecialKind kind,
										  OpenMarkdown doc, XmlNode context,
										  ref StringBuilder accum)
		{
			AppendText(doc, context, ref accum);
			context.AppendChild(doc.NewSpecial(kind));
		}

		private static void AppendNode(XmlNode node, OpenMarkdown doc, XmlNode context,
									   ref StringBuilder accum)
		{
			AppendText(doc, context, ref accum);
			context.AppendChild(node);
		}

		private static void AppendSentenceEnd(List<Tokenizer.Token> tokens,
											  ref int index, ref Tokenizer.Token tok,
											  OpenMarkdown doc, XmlNode context,
											  ref StringBuilder accum)
		{
			// Test whether this is a sentence-ending period.
			if (index + 1 == tokens.Count) {
				return;
			}
			else if (tokens[index + 1].TokenKind == Tokenizer.Token.Kind.Whitespace) {
				if (index + 2 == tokens.Count) {
					index++;
					return;
				}

				Tokenizer.Token ntok = tokens[index + 2];
				if (ntok.TokenKind == Tokenizer.Token.Kind.BackQuote ||
					ntok.TokenKind == Tokenizer.Token.Kind.SingleQuote ||
					ntok.TokenKind == Tokenizer.Token.Kind.DoubleQuote ||
					ntok.TokenKind == Tokenizer.Token.Kind.OpenDoubleQuote) {
					AppendSpecial(OpenMarkdown.SpecialKind.EndOfSentence,
								  doc, context, ref accum);
					tok = ntok;
					index++;
					return;
				}

				if (capOrDigRe.IsMatch(ntok.Content) &&
					! sentEndRe.IsMatch(accum.ToString())) {
					AppendSpecial(OpenMarkdown.SpecialKind.EndOfSentence,
								  doc, context, ref accum);
					tok = ntok;
					index++;
				}
			}
		}

		private static void ProcessTokens(List<Tokenizer.Token> tokens,
										  OpenMarkdown doc, XmlNode context)
		{
			// Reset the elements list, and then restore what it should look
			// like from the token stream
			OpenMarkdown.StripChildNodes(context);

			StringBuilder accum = new StringBuilder();

			Tokenizer.Token lastToken = null;
			for (int i = 0; i < tokens.Count; i++) {
				Tokenizer.Token tok = tokens[i];
				switch (tok.TokenKind) {
				case Tokenizer.Token.Kind.Backslash:
					if (i + 1 < tokens.Count) {
						accum.Append(tokens[i + 1].Content);
						i++;
					}
					break;

				case Tokenizer.Token.Kind.Referral:
					AppendNode(tok.Referral, doc, context, ref accum);
					break;

				case Tokenizer.Token.Kind.DoubleDash:
					switch (doc.Config.DashesStyle) {
					case Configuration.SmartyDashes.DoubleEmdashNoEndash:
					case Configuration.SmartyDashes.DoubleEmdashTripleEndash:
						AppendSpecial(OpenMarkdown.SpecialKind.Emdash,
									  doc, context, ref accum);
						break;
					case Configuration.SmartyDashes.TripleEmdashDoubleEndash:
						AppendSpecial(OpenMarkdown.SpecialKind.Endash,
									  doc, context, ref accum);
						break;
					}
					break;

				case Tokenizer.Token.Kind.TripleDash:
					switch (doc.Config.DashesStyle) {
					case Configuration.SmartyDashes.DoubleEmdashTripleEndash:
						AppendSpecial(OpenMarkdown.SpecialKind.Endash,
									  doc, context, ref accum);
						break;
					case Configuration.SmartyDashes.TripleEmdashDoubleEndash:
						AppendSpecial(OpenMarkdown.SpecialKind.Emdash,
									  doc, context, ref accum);
						break;
					}
					break;

				case Tokenizer.Token.Kind.Ellipsis:
					AppendSpecial(OpenMarkdown.SpecialKind.Ellipsis,
								  doc, context, ref accum);
					AppendSentenceEnd(tokens, ref i, ref tok,
									  doc, context, ref accum);
					break;

				case Tokenizer.Token.Kind.UnbreakableSpace:
					AppendSpecial(OpenMarkdown.SpecialKind.UnbreakableSpace,
								  doc, context, ref accum);
					break;

				case Tokenizer.Token.Kind.OpenDoubleQuote:
					AppendSpecial(OpenMarkdown.SpecialKind.OpenDoubleQuote,
								  doc, context, ref accum);
					break;

				case Tokenizer.Token.Kind.CloseDoubleQuote:
					AppendSpecial(OpenMarkdown.SpecialKind.CloseDoubleQuote,
								  doc, context, ref accum);
					AppendSentenceEnd(tokens, ref i, ref tok,
									  doc, context, ref accum);
					break;

				case Tokenizer.Token.Kind.SingleQuote:
					if (lastToken == null ||
						lastToken.TokenKind == Tokenizer.Token.Kind.Whitespace) {
						AppendSpecial(OpenMarkdown.SpecialKind.OpenSingleQuote,
									  doc, context, ref accum);
						break;
					}
					else if (i + 1 == tokens.Count) {
						AppendSpecial(OpenMarkdown.SpecialKind.CloseSingleQuote,
									  doc, context, ref accum);
						AppendSentenceEnd(tokens, ref i, ref tok,
										  doc, context, ref accum);
						break;
					}
					else {
						Tokenizer.Token.Kind kind = tokens[i + 1].TokenKind;
						switch (kind) {
						case Tokenizer.Token.Kind.QuestionMark:
						case Tokenizer.Token.Kind.ExclamationMark:
						case Tokenizer.Token.Kind.Comma:
						case Tokenizer.Token.Kind.Period:
						case Tokenizer.Token.Kind.Semicolon:
						case Tokenizer.Token.Kind.Colon:
						case Tokenizer.Token.Kind.CloseParen:
						case Tokenizer.Token.Kind.Whitespace:
							AppendSpecial(OpenMarkdown.SpecialKind.CloseSingleQuote,
										  doc, context, ref accum);
							AppendSentenceEnd(tokens, ref i, ref tok,
											  doc, context, ref accum);
							break;
						default:
							accum.Append(tok.Content);
							break;
						}
					}
					break;

				case Tokenizer.Token.Kind.DoubleQuote:
					if (lastToken == null ||
						lastToken.TokenKind == Tokenizer.Token.Kind.Whitespace) {
						AppendSpecial(OpenMarkdown.SpecialKind.OpenDoubleQuote,
									  doc, context, ref accum);
						break;
					}
					else if (lastToken != null &&
							 (lastToken.TokenKind == Tokenizer.Token.Kind.QuestionMark ||
							  lastToken.TokenKind == Tokenizer.Token.Kind.ExclamationMark ||
							  lastToken.TokenKind == Tokenizer.Token.Kind.Ellipsis ||
							  lastToken.TokenKind == Tokenizer.Token.Kind.Period)) {
						AppendSpecial(OpenMarkdown.SpecialKind.CloseDoubleQuote,
									  doc, context, ref accum);
						AppendSentenceEnd(tokens, ref i, ref tok,
										  doc, context, ref accum);
						break;
					}
					else if (i + 1 == tokens.Count) {
						AppendSpecial(OpenMarkdown.SpecialKind.CloseDoubleQuote,
									  doc, context, ref accum);
						AppendSentenceEnd(tokens, ref i, ref tok,
										  doc, context, ref accum);
						break;
					}
					else {
						Tokenizer.Token.Kind kind = tokens[i + 1].TokenKind;
						switch (kind) {
						case Tokenizer.Token.Kind.Whitespace:
						case Tokenizer.Token.Kind.UnbreakableSpace:
						case Tokenizer.Token.Kind.QuestionMark:
						case Tokenizer.Token.Kind.ExclamationMark:
						case Tokenizer.Token.Kind.Comma:
						case Tokenizer.Token.Kind.Period:
						case Tokenizer.Token.Kind.Semicolon:
						case Tokenizer.Token.Kind.Colon:
						case Tokenizer.Token.Kind.CloseParen:
						case Tokenizer.Token.Kind.SingleQuote:
						case Tokenizer.Token.Kind.SingleDash:
						case Tokenizer.Token.Kind.DoubleDash:
						case Tokenizer.Token.Kind.TripleDash:
						case Tokenizer.Token.Kind.Ellipsis:
						case Tokenizer.Token.Kind.Referral:
							AppendSpecial(OpenMarkdown.SpecialKind.CloseDoubleQuote,
										  doc, context, ref accum);
							AppendSentenceEnd(tokens, ref i, ref tok,
											  doc, context, ref accum);
							break;
						default:
							accum.Append(tok.Content);
							break;
						}
					}
					break;

				case Tokenizer.Token.Kind.SingleDash:
				case Tokenizer.Token.Kind.BackQuote:
				case Tokenizer.Token.Kind.Comma:
				case Tokenizer.Token.Kind.Semicolon:
				case Tokenizer.Token.Kind.Colon:
				case Tokenizer.Token.Kind.OpenParen:
					accum.Append(tok.Content);
					break;

				case Tokenizer.Token.Kind.QuestionMark:
				case Tokenizer.Token.Kind.ExclamationMark:
				case Tokenizer.Token.Kind.CloseParen:
				case Tokenizer.Token.Kind.Period:
					accum.Append(tok.Content);
					AppendSentenceEnd(tokens, ref i, ref tok,
									  doc, context, ref accum);
					break;

				case Tokenizer.Token.Kind.Text:
					if (doc.Config.UseWikiLinks) {
						Match m = wikiLinkRe.Match(tok.Content);
						if (m.Success) {
							AppendText(doc, context, ref accum);

							XmlElement elem = doc.CreateElement("wikilink");
							XmlText value = doc.Document.CreateTextNode(tok.Content);
							elem.AppendChild(value);
							context.AppendChild(elem);
							break;
						}
					}
					accum.Append(tok.Content);
					break;

				case Tokenizer.Token.Kind.Whitespace:
					bool append = true;
					if (doc.Config.SpacesAroundDashes && i + 1 < tokens.Count) {
						Tokenizer.Token.Kind kind = tokens[i + 1].TokenKind;
						switch (kind) {
						case Tokenizer.Token.Kind.DoubleDash:
							switch (doc.Config.DashesStyle) {
							case Configuration.SmartyDashes.DoubleEmdashNoEndash:
							case Configuration.SmartyDashes.DoubleEmdashTripleEndash:
								AppendSpecial(OpenMarkdown.SpecialKind.Emdash,
											  doc, context, ref accum);
								break;
							case Configuration.SmartyDashes.TripleEmdashDoubleEndash:
								AppendSpecial(OpenMarkdown.SpecialKind.Endash,
											  doc, context, ref accum);
								break;
							}
							append = false;
							break;

						case Tokenizer.Token.Kind.TripleDash:
							switch (doc.Config.DashesStyle) {
							case Configuration.SmartyDashes.DoubleEmdashTripleEndash:
								AppendSpecial(OpenMarkdown.SpecialKind.Endash,
											  doc, context, ref accum);
								break;
							case Configuration.SmartyDashes.TripleEmdashDoubleEndash:
								AppendSpecial(OpenMarkdown.SpecialKind.Emdash,
											  doc, context, ref accum);
								break;
							}
							append = false;
							break;
						}

						if (! append) {
							i++;
							if (i + 1 < tokens.Count &&
								tokens[i + 1].TokenKind == Tokenizer.Token.Kind.Whitespace)
								i++;
						}
					}

					if (append)
						accum.Append(tok.Content);
					break;
				}

				lastToken = tok;
			}

			AppendText(doc, context, ref accum);
		}

		public static void Transform(OpenMarkdown doc, XmlNode context)
		{
			List<Tokenizer.Token> tokens = new List<Tokenizer.Token>();
			
			foreach (XmlNode elem in context.ChildNodes) {
				XmlText textElem = elem as XmlText;
				if (textElem != null) {
					Tokenizer engine = new Tokenizer(textElem.Value);

					for (Tokenizer.Token tok = engine.NextToken();
						 tok != null;
						 tok = engine.NextToken())
						tokens.Add(tok);
				} else {
					tokens.Add(new Tokenizer.Token(elem));
					if (OpenMarkdown.KindOfInline(elem) != OpenMarkdown.InlineKind.Literal)
						Transform(doc, elem);
				}
			}

			ProcessTokens(tokens, doc, context);
		}
	}
}
