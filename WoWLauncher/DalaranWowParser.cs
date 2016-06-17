using System;
using System.Collections.Generic;

namespace WoWLauncher
{
    public class Post
    {
        public string Title { get; set; }
        public string Author { get; set; }
        public DateTime Date { get; set; }
        public string Url { get; set; }
    }

    public class NewsParser
    {
        private readonly Func<string, IEnumerable<Post>> _parse;
        public string PostFormat { get; }
        public NewsParser(Func<string, IEnumerable<Post>> parse, string format)
        {
            _parse = parse;
            PostFormat = format;
        }

        public IEnumerable<Post> Parse(string html)
        {
            return _parse(html);
        }
    }
}
