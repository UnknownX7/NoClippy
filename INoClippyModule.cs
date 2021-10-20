namespace NoClippy
{
    public interface INoClippyModule
    {
        public bool IsEnabled { get; set; }
        public int DrawOrder { get; }
        public void DrawConfig();
        public void Enable();
        public void Disable();
    }
}
