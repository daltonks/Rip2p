namespace Rip2p
{
    public class ReceivedInitialDataFromServerMessage
    {
        public static ReceivedInitialDataFromServerMessage Instance { get; } = new();
            
        private ReceivedInitialDataFromServerMessage () { }
    }
}