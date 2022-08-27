namespace PacketStudioLight
{
    public static class PslcCommentsEncoder
    {
        const string MAGIC = "PacketStudioLightComment";
        const string VER_1 = "Version=1";

        private static string BuildHeader() => $"{MAGIC}\n{VER_1}\n";

        public static string Encode(string rawTextboxContent)
        {
            return BuildHeader() + rawTextboxContent;

        }

        public static bool TryDecode(string comment, out string parsedTextboxContent)
        {
            parsedTextboxContent = null;
            if (string.IsNullOrWhiteSpace(comment))
                return false;
            string[] lines = comment.Split('\n');

            // Magic + Version + 1 line of Content
            if (lines.Length < 3)
                return false;
            if (lines[0].Trim() != MAGIC)
                return false;
            if (lines[1].Trim() != VER_1)
                return false;

            parsedTextboxContent = comment.Substring(BuildHeader().Length);
            return true;
        }

    }
}
