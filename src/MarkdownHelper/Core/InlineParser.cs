using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MarkdownHelper.Core
{
    /// <summary>Inline parser using the CommonMark delimiter-run algorithm.</summary>
    public static class InlineParser
    {
        private const string AsciiPunctuation = "!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~";

        private static readonly Regex AngleAutolink = new Regex(@"\G<([A-Za-z][A-Za-z0-9+.-]{1,31}:[^\s<>]*|[\w.+-]+@[\w-]+(?:\.[\w-]+)+)>", RegexOptions.Compiled);
        private static readonly Regex BareUrl = new Regex(@"\Ghttps?://[^\s<]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex LinkTail = new Regex(@"\G\(\s*(?:<([^<>\n]*)>|([^\s()]*(?:\([^\s()]*\)[^\s()]*)*))(?:\s+(?:""([^""]*)""|'([^']*)'))?\s*\)", RegexOptions.Compiled);

        private sealed class Token
        {
            public Inline Node;
            public char Delimiter;
            public int Count;
            public bool CanOpen;
            public bool CanClose;
            public bool IsOpenBracket;
            public bool IsImage;
            public bool Active = true;
            public int Position;
        }

        public static List<Inline> Parse(string text)
        {
            var tokens = new List<Token>();
            var buffer = new StringBuilder();
            int i = 0;

            void Flush()
            {
                if (buffer.Length == 0) return;
                tokens.Add(new Token { Node = new Text(buffer.ToString()) });
                buffer.Clear();
            }

            while (i < text.Length)
            {
                char c = text[i];
                switch (c)
                {
                    case '\\':
                        if (i + 1 < text.Length && text[i + 1] == '\n')
                        {
                            Flush();
                            tokens.Add(new Token { Node = new HardBreak() });
                            i += 2;
                        }
                        else if (i + 1 < text.Length && AsciiPunctuation.IndexOf(text[i + 1]) >= 0)
                        {
                            buffer.Append(text[i + 1]);
                            i += 2;
                        }
                        else
                        {
                            buffer.Append(c);
                            i++;
                        }
                        break;

                    case '\n':
                        bool hard = buffer.Length >= 2 && buffer[buffer.Length - 1] == ' ' && buffer[buffer.Length - 2] == ' ';
                        while (buffer.Length > 0 && buffer[buffer.Length - 1] == ' ') buffer.Length--;
                        Flush();
                        tokens.Add(new Token { Node = hard ? (Inline)new HardBreak() : new SoftBreak() });
                        i++;
                        while (i < text.Length && text[i] == ' ') i++;
                        break;

                    case '`':
                        int ticks = RunLength(text, i, '`');
                        int close = FindClosingTicks(text, i + ticks, ticks);
                        if (close < 0)
                        {
                            buffer.Append('`', ticks);
                            i += ticks;
                        }
                        else
                        {
                            Flush();
                            tokens.Add(new Token { Node = new CodeSpan(CleanCode(text.Substring(i + ticks, close - i - ticks))) });
                            i = close + ticks;
                        }
                        break;

                    case '<':
                        var angle = AngleAutolink.Match(text, i);
                        if (angle.Success)
                        {
                            Flush();
                            string target = angle.Groups[1].Value;
                            string url = target.Contains("@") && !target.Contains(":") ? "mailto:" + target : target;
                            var link = new Link { Url = url };
                            link.Children.Add(new Text(target));
                            tokens.Add(new Token { Node = link });
                            i += angle.Length;
                        }
                        else
                        {
                            buffer.Append(c);
                            i++;
                        }
                        break;

                    case '!':
                        if (i + 1 < text.Length && text[i + 1] == '[')
                        {
                            Flush();
                            tokens.Add(new Token { Node = new Text("!["), IsOpenBracket = true, IsImage = true, Position = i });
                            i += 2;
                        }
                        else
                        {
                            buffer.Append(c);
                            i++;
                        }
                        break;

                    case '[':
                        Flush();
                        tokens.Add(new Token { Node = new Text("["), IsOpenBracket = true, Position = i });
                        i++;
                        break;

                    case ']':
                        Flush();
                        if (!TryCloseBracket(text, ref i, tokens))
                        {
                            buffer.Append(']');
                            i++;
                        }
                        break;

                    case '*':
                    case '_':
                    case '~':
                        int run = RunLength(text, i, c);
                        if (c == '~' && run != 2)
                        {
                            buffer.Append(c, run);
                            i += run;
                            break;
                        }
                        Flush();
                        tokens.Add(MakeDelimiter(text, i, run, c));
                        i += run;
                        break;

                    case 'h':
                    case 'H':
                        var bare = IsWordBoundary(text, i) ? BareUrl.Match(text, i) : Match.Empty;
                        if (bare.Success)
                        {
                            string url = TrimUrl(bare.Value);
                            Flush();
                            var link = new Link { Url = url };
                            link.Children.Add(new Text(url));
                            tokens.Add(new Token { Node = link });
                            i += url.Length;
                        }
                        else
                        {
                            buffer.Append(c);
                            i++;
                        }
                        break;

                    default:
                        buffer.Append(c);
                        i++;
                        break;
                }
            }

            Flush();
            ProcessEmphasis(tokens, 0);
            return Collapse(tokens);
        }

        private static int RunLength(string text, int start, char c)
        {
            int i = start;
            while (i < text.Length && text[i] == c) i++;
            return i - start;
        }

        private static int FindClosingTicks(string text, int from, int length)
        {
            int i = from;
            while (i < text.Length)
            {
                if (text[i] != '`') { i++; continue; }
                int run = RunLength(text, i, '`');
                if (run == length) return i;
                i += run;
            }
            return -1;
        }

        private static string CleanCode(string code)
        {
            string text = code.Replace('\n', ' ');
            bool padded = text.Length > 2 && text[0] == ' ' && text[text.Length - 1] == ' ' && text.Trim().Length > 0;
            return padded ? text.Substring(1, text.Length - 2) : text;
        }

        private static bool IsWordBoundary(string text, int i)
        {
            return i == 0 || !char.IsLetterOrDigit(text[i - 1]);
        }

        private static string TrimUrl(string url)
        {
            string trimmed = url;
            while (trimmed.Length > 0)
            {
                char last = trimmed[trimmed.Length - 1];
                if ("?!.,:;*_~'\"".IndexOf(last) >= 0) { trimmed = trimmed.Substring(0, trimmed.Length - 1); continue; }
                if (last == ')' && trimmed.Count(ch => ch == ')') > trimmed.Count(ch => ch == '('))
                {
                    trimmed = trimmed.Substring(0, trimmed.Length - 1);
                    continue;
                }
                break;
            }
            return trimmed;
        }

        private static bool IsPunctuation(char c)
        {
            if (char.IsPunctuation(c) || char.IsSymbol(c)) return true;
            return AsciiPunctuation.IndexOf(c) >= 0;
        }

        private static Token MakeDelimiter(string text, int start, int run, char c)
        {
            char before = start > 0 ? text[start - 1] : '\n';
            char after = start + run < text.Length ? text[start + run] : '\n';

            bool beforeSpace = char.IsWhiteSpace(before);
            bool afterSpace = char.IsWhiteSpace(after);
            bool beforePunct = IsPunctuation(before);
            bool afterPunct = IsPunctuation(after);

            bool leftFlanking = !afterSpace && (!afterPunct || beforeSpace || beforePunct);
            bool rightFlanking = !beforeSpace && (!beforePunct || afterSpace || afterPunct);

            bool canOpen, canClose;
            if (c == '_')
            {
                canOpen = leftFlanking && (!rightFlanking || beforePunct);
                canClose = rightFlanking && (!leftFlanking || afterPunct);
            }
            else
            {
                // Looser than CommonMark on purpose: AI output often writes **Note:**text,
                // which strict flanking rules would leave as literal asterisks.
                canOpen = !afterSpace;
                canClose = !beforeSpace;
            }

            return new Token
            {
                Node = new Text(new string(c, run)),
                Delimiter = c,
                Count = run,
                CanOpen = canOpen,
                CanClose = canClose,
                Position = start
            };
        }

        private static bool TryCloseBracket(string text, ref int i, List<Token> tokens)
        {
            int openIndex = -1;
            for (int k = tokens.Count - 1; k >= 0; k--)
            {
                if (tokens[k].IsOpenBracket) { openIndex = k; break; }
            }
            if (openIndex < 0) return false;

            var opener = tokens[openIndex];
            if (!opener.Active)
            {
                opener.IsOpenBracket = false;
                return false;
            }

            var tail = i + 1 < text.Length && text[i + 1] == '(' ? LinkTail.Match(text, i + 1) : Match.Empty;
            if (!tail.Success)
            {
                opener.IsOpenBracket = false;
                return false;
            }

            string url = tail.Groups[1].Success ? tail.Groups[1].Value : tail.Groups[2].Value;
            string title = tail.Groups[3].Success ? tail.Groups[3].Value : tail.Groups[4].Value;

            ProcessEmphasis(tokens, openIndex + 1);
            var children = Collapse(tokens.Skip(openIndex + 1).ToList());
            tokens.RemoveRange(openIndex, tokens.Count - openIndex);

            if (opener.IsImage)
            {
                tokens.Add(new Token { Node = new Image { Url = url, Title = title ?? "", Alt = PlainText(children) } });
            }
            else
            {
                tokens.Add(new Token { Node = new Link { Url = url, Title = title ?? "", Children = children } });

                // Links cannot contain links, so earlier brackets can no longer form one.
                foreach (var token in tokens.Where(t => t.IsOpenBracket && !t.IsImage)) token.Active = false;
            }

            i += 1 + tail.Length;
            return true;
        }

        /// <summary>Flattens inline content to its visible text.</summary>
        public static string PlainText(IEnumerable<Inline> inlines)
        {
            var builder = new StringBuilder();
            foreach (var inline in inlines)
            {
                switch (inline)
                {
                    case Text text: builder.Append(text.Value); break;
                    case CodeSpan code: builder.Append(code.Code); break;
                    case Image image: builder.Append(image.Alt); break;
                    case SoftBreak _:
                    case HardBreak _: builder.Append(' '); break;
                    case ContainerInline container: builder.Append(PlainText(container.Children)); break;
                }
            }
            return builder.ToString();
        }

        private static void ProcessEmphasis(List<Token> tokens, int bottom)
        {
            // Remembers where a search for an opener came up empty, so text with thousands of
            // unmatched delimiters stays fast instead of rescanning from the start each time.
            var floors = new Dictionary<int, int>();

            int closer = bottom;
            while (closer < tokens.Count)
            {
                var close = tokens[closer];
                if (close.Delimiter == '\0' || !close.CanClose || close.Count == 0) { closer++; continue; }

                int floorKey = close.Delimiter * 8 + (close.CanOpen ? 4 : 0) + close.Count % 3;
                int floor = floors.TryGetValue(floorKey, out int known) ? Math.Max(known, bottom) : bottom;

                int opener = -1;
                for (int k = closer - 1; k >= floor; k--)
                {
                    var open = tokens[k];
                    if (open.Delimiter != close.Delimiter || !open.CanOpen || open.Count == 0) continue;
                    bool oddMatch = (open.CanClose || close.CanOpen)
                        && (open.Count + close.Count) % 3 == 0
                        && !(open.Count % 3 == 0 && close.Count % 3 == 0);
                    if (oddMatch) continue;
                    opener = k;
                    break;
                }

                if (opener < 0)
                {
                    floors[floorKey] = closer;
                    closer++;
                    continue;
                }

                // Removing tokens below shifts later indexes, so no floor may sit above the opener.
                foreach (int key in floors.Keys.ToList()) floors[key] = Math.Min(floors[key], opener);

                var left = tokens[opener];
                int used = close.Delimiter == '~' ? 2 : (left.Count >= 2 && close.Count >= 2 ? 2 : 1);

                ContainerInline wrapper;
                if (close.Delimiter == '~') wrapper = new Strike();
                else if (used == 2) wrapper = new Strong();
                else wrapper = new Emphasis();

                wrapper.Children = Collapse(tokens.GetRange(opener + 1, closer - opener - 1));
                tokens.RemoveRange(opener + 1, closer - opener - 1);
                closer = opener + 1;

                tokens.Insert(closer, new Token { Node = wrapper });
                closer++;

                left.Count -= used;
                close.Count -= used;
                left.Node = new Text(new string(left.Delimiter, left.Count));
                close.Node = new Text(new string(close.Delimiter, close.Count));

                if (close.Count == 0)
                {
                    tokens.RemoveAt(closer);
                }
                if (left.Count == 0)
                {
                    tokens.RemoveAt(opener);
                    closer--;
                }
            }
        }

        private static List<Inline> Collapse(List<Token> tokens)
        {
            var result = new List<Inline>();
            var run = new StringBuilder();

            void FlushRun()
            {
                if (run.Length == 0) return;
                result.Add(new Text(run.ToString()));
                run.Clear();
            }

            foreach (var token in tokens)
            {
                if (token.Node is Text text)
                {
                    run.Append(text.Value);
                    continue;
                }
                FlushRun();
                result.Add(token.Node);
            }
            FlushRun();
            return result;
        }
    }
}
