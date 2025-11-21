using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace it_beacon_systray.Helpers
{
    public static class TextBlockFormatter
    {
        public static void SetFormattedText(TextBlock textBlock, string formattedText)
        {
            if (textBlock == null) return;

            textBlock.Inlines.Clear();
            if (string.IsNullOrEmpty(formattedText)) return;

            // Replace <br/> with single newlines and <Paragraph> tags with double newlines for more separation
            string processedText = Regex.Replace(formattedText, "<br/?>", Environment.NewLine, RegexOptions.IgnoreCase);
            processedText = Regex.Replace(processedText, "<paragraph>", "", RegexOptions.IgnoreCase);
            processedText = Regex.Replace(processedText, "</paragraph>", Environment.NewLine + Environment.NewLine, RegexOptions.IgnoreCase);


            // Regex to find <bold>...</bold> tags, ignoring case
            string pattern = @"<bold>(.*?)</bold>";
            var matches = Regex.Matches(processedText, pattern, RegexOptions.IgnoreCase);
            int lastIndex = 0;

            foreach (Match match in matches)
            {
                // Add any text before the current bold tag
                if (match.Index > lastIndex)
                {
                    textBlock.Inlines.Add(new Run(processedText.Substring(lastIndex, match.Index - lastIndex)));
                }

                // Add the bolded text
                textBlock.Inlines.Add(new Bold(new Run(match.Groups[1].Value)));

                lastIndex = match.Index + match.Length;
            }

            // Add any remaining text after the last bold tag
            if (lastIndex < processedText.Length)
            {
                textBlock.Inlines.Add(new Run(processedText.Substring(lastIndex)));
            }
        }
    }
}