namespace NoClippy
{
    public interface INoClippyModule
    {
        public bool IsEnabled { get; set; }
        public void Enable();
        public void Disable();
    }
}
