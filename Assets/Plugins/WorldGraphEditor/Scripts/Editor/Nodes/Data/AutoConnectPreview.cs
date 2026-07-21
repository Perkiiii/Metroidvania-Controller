namespace WorldGraphEditor.Editor
{
    internal readonly struct AutoConnectPreview
    {
        public readonly AutoConnectKind Kind;
        public readonly PortPreview? PortA;
        public readonly PortPreview? PortB;

        public AutoConnectPreview(AutoConnectKind kind, PortPreview? portA = null, PortPreview? portB = null)
        {
            Kind = kind;
            PortA = portA;
            PortB = portB;
        }
    }
}