namespace WoWLauncher
{
    public class ActionProgress
    {
        public string Text { get; }
        public int Percent { get; }

        public ActionProgress(string text, int percent = -1)
        {
            Text = text;
            Percent = percent;
        }
    }
}