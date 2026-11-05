using System.Text;

namespace MarkdownHelper.Core
{
    /// <summary>
    /// Encodes the Windows "HTML Format" clipboard payload. Its header holds byte offsets
    /// into the UTF-8 data, so characters outside ASCII must be counted as bytes.
    /// </summary>
    public static class ClipboardHtml
    {
        private const string HeaderTemplate =
            "Version:0.9\r\nStartHTML:{0:D10}\r\nEndHTML:{1:D10}\r\nStartFragment:{2:D10}\r\nEndFragment:{3:D10}\r\n";

        private const string Before = "<html><head><meta charset=\"utf-8\"></head><body><!--StartFragment-->";
        private const string After = "<!--EndFragment--></body></html>";

        public static byte[] Encode(string fragment)
        {
            var utf8 = new UTF8Encoding(false);
            int headerLength = string.Format(HeaderTemplate, 0, 0, 0, 0).Length;
            int startFragment = headerLength + utf8.GetByteCount(Before);
            int endFragment = startFragment + utf8.GetByteCount(fragment);
            int endHtml = endFragment + utf8.GetByteCount(After);

            string header = string.Format(HeaderTemplate, headerLength, endHtml, startFragment, endFragment);
            return utf8.GetBytes(header + Before + fragment + After);
        }
    }
}
