namespace AlienDefense.Core
{
    /// <summary>Implemented by exactly one root-level component per scene. ApplicationRuntime injects services
    /// into the receiver when each content scene loads.</summary>
    public interface IApplicationServicesReceiver
    {
        void ReceiveApplicationServices(ApplicationServices services);
    }
}
