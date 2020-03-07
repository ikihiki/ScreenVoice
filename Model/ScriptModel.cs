using System.Collections.Generic;

namespace Model
{
    public class ScriptModel
    {
        public IReadOnlyList<string> Old { get; }
        public IReadOnlyList<string> New { get; }

        public ScriptModel( List<string> old, List<string> @new)
        {
            Old = old;
            New = @new;
        }

        public ScriptResult CreateResult (string text, bool isRead)
        {
            return new ScriptResult { Text = text, IsRead = isRead };
        }
    }

}
