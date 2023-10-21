namespace Rip2p.Session.Data
{
    public class ReceivedInitialDataFromServerMessage
    {
        public static ReceivedInitialDataFromServerMessage Instance { get; } = new();
            
        private ReceivedInitialDataFromServerMessage () { }
    }
}