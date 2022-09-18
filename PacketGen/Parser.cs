using System.Text;
using System.Text.RegularExpressions;

namespace PacketGen
{
    public static class Parser
    {
        public static (string type, Dictionary<string, string> variables) Parse(string[] lines)
        {
            int index = lines[0].IndexOf("Generate:");
            if (lines.Length == 0 || index == -1)
            {
                throw new Exception("Can't parse packet definition");
            }
            string type = lines[0].Substring(index + "Generate:".Length).Trim();

            Dictionary<string, string> variables = new Dictionary<string, string>();

            int nextLineIndx = 1;
            bool aggregating = false;
            string aggregatingVarName = null;
            StringBuilder sb = new StringBuilder();
            while (nextLineIndx < lines.Length)
            {
                string nextLine = lines[nextLineIndx].Trim();
                if (!nextLine.StartsWith("@"))
                {
                    if (aggregating)
                    {
                        // Aggergating all NON COMMENT lines
                        if (nextLine.StartsWith("//") || nextLine.StartsWith("/*"))
                        {
                        }
                        else if (nextLine.StartsWith('"'))
                        {
                            // Encode a string
                            Regex stringEncodeExpressionRegex = new Regex("\"(.*)\"\\.Encode\\(\"(.*)\"\\)");
                            var match = stringEncodeExpressionRegex.Match(nextLine);
                            if (match.Success)
                            {
                                var toEncode = match.Groups[1].ToString();
                                var encodingName = match.Groups[2].ToString();
                                var encoding = Encoding.GetEncoding(encodingName);
                                byte[] encodedString = encoding.GetBytes(toEncode);
                                sb.Append(encodedString.GetHex());
                            }
                            else
                            {
                                throw new Exception($"Can't Parse string encode expression '{nextLine}'");
                            }
                        }
                        else
                        {
                            sb.Append(nextLine);
                        }
                    }
                    else
                        throw new Exception($"Did not expect line with @ at line index {nextLineIndx}");
                }
                else
                {
                    string prefixless = nextLine.Substring(2).Trim();
                    string[] split = prefixless.Split('=', StringSplitOptions.TrimEntries);
                    if (split.Length != 2)
                    {
                        if (split.Length == 1 && split[0] == "}" && aggregating)
                        {
                            variables[aggregatingVarName] = sb.ToString();
                            aggregating = false;
                            aggregatingVarName = null;
                            sb.Clear();
                        }
                        else
                            throw new Exception($"Unexpected number of splitted parts: {split.Length} at line #{nextLineIndx}");
                    }
                    else
                    {
                        string varName = split[0];
                        string value = split[1];
                        if (value == "{")
                        {
                            aggregating = true;
                            aggregatingVarName = varName;
                        }
                        else
                        {
                            variables[varName] = value;
                        }
                    }
                }
                nextLineIndx++;
            }

            if (aggregating)
                throw new Exception($"Reached end of text but the var '{aggregatingVarName}' never ended (still aggregating)");

            return (type, variables);
        }


    }
}
