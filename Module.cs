namespace NoClippy
{
    public abstract class Module
    {
        public virtual bool IsEnabled
        {
            get => true;
            set => _ = value;
        }
        public virtual int DrawOrder => 0;

        public virtual void DrawConfig() { }
        public virtual void Enable() { }
        public virtual void Disable() { }
    }
}
